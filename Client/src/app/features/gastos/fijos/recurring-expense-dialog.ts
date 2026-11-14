import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { RecurringExpensesApi } from '../../../core/api/expenses-api';
import {
  EnvelopeOverviewResponse,
  ExpenseCategoryResponse,
  ExpenseFrequency,
  RecurringExpenseResponse,
} from '../../../core/api/models';
import { parseHundredths, toInputText } from '../../../shared/format/money';
import { amountValidator } from '../../../shared/format/money-validators';
import { DAY_OF_MONTH_OPTIONS, WEEKDAY_OPTIONS } from '../expense-format';

export interface RecurringExpenseDialogData {
  /** Absent = create. */
  recurring?: RecurringExpenseResponse;
  overview: EnvelopeOverviewResponse;
  categories: ExpenseCategoryResponse[];
}

/**
 * Create or edit a gasto fijo (RF-GAS-01). Editing never touches the expenses already paid. Deactivated = no longer
 * proposed (never deleted: its payments point to it).
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ editing ? 'Editar gasto fijo' : 'Nuevo gasto fijo' }}</h2>
        <p class="card__lead">
          Algo que se paga siempre: el gestor, el alquiler, el transporte de los lunes. Cuando vence, Cuadre te lo
          propone para que lo pagues o lo saltes. Nunca se paga solo.
        </p>
      </div>

      <div class="form__row">
        <label class="field">
          <span class="field__label">Nombre</span>
          <input class="input" formControlName="name" autocomplete="off" maxlength="120" placeholder="Gestor"
            cdkFocusInitial [class.input--invalid]="invalid('name')" />
          @if (invalid('name')) {
            <span class="field__error">Escribí el nombre.</span>
          } @else if (error()?.status === 409) {
            <span class="field__error">{{ error()!.message }}</span>
          }
        </label>
        <label class="field">
          <span class="field__label">Categoría</span>
          <select class="input" formControlName="categoryId" [class.input--invalid]="invalid('categoryId')">
            <option value="" disabled>Elegí una</option>
            @for (category of categories; track category.id) {
              <option [value]="category.id">{{ category.name }}</option>
            }
          </select>
          @if (invalid('categoryId')) {
            <span class="field__error">Elegí la categoría.</span>
          }
        </label>
      </div>

      <div class="form__row">
        <label class="field">
          <span class="field__label">Monto</span>
          <span class="input input--affix" [class.input--invalid]="invalid('amount')">
            <input formControlName="amount" inputmode="decimal" autocomplete="off" class="num" />
            <span>{{ currency() }}</span>
          </span>
          @if (invalid('amount')) {
            <span class="field__error">Escribí un monto mayor que cero.</span>
          } @else {
            <span class="field__hint">Al pagarlo se puede cambiar.</span>
          }
        </label>
        <label class="field">
          <span class="field__label">Caja</span>
          <select class="input" formControlName="cashBoxId">
            @for (box of boxes; track box.id) {
              <option [value]="box.id">{{ box.name }}</option>
            }
          </select>
        </label>
      </div>

      <label class="field">
        <span class="field__label">Sale del sobre</span>
        <select class="input" formControlName="envelopeId">
          @for (envelope of envelopes; track envelope.id) {
            <option [value]="envelope.id">{{ envelope.name }}</option>
          }
        </select>
      </label>

      <div class="form__row">
        <label class="field">
          <span class="field__label">Se paga</span>
          <select class="input" formControlName="frequency" (change)="frequencyChanged()">
            <option value="Monthly">Todos los meses</option>
            <option value="Weekly">Todas las semanas</option>
          </select>
        </label>
        <label class="field">
          <span class="field__label">{{ value().frequency === 'Monthly' ? 'El día' : 'Los' }}</span>
          <select class="input num" formControlName="dueDay">
            @if (value().frequency === 'Monthly') {
              @for (day of daysOfMonth; track day) {
                <option [value]="day">{{ day }}</option>
              }
            } @else {
              @for (day of weekdays; track day.value) {
                <option [value]="day.value">{{ day.label }}</option>
              }
            }
          </select>
          @if (value().frequency === 'Monthly') {
            <span class="field__hint">Hasta el 28, así todos los meses lo tienen.</span>
          }
        </label>
        <label class="field">
          <span class="field__label">Desde</span>
          <input class="input num" type="date" formControlName="startsOn" [class.input--invalid]="invalid('startsOn')" />
          @if (invalid('startsOn')) {
            <span class="field__error">Elegí desde cuándo.</span>
          }
        </label>
      </div>

      <label class="field">
        <span class="field__label">Nota <span class="opt">(opcional)</span></span>
        <input class="input" formControlName="notes" autocomplete="off" maxlength="500" />
      </label>

      @if (editing) {
        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          <span>Se sigue pagando (si no, deja de proponerse)</span>
        </label>
      }

      @if (error(); as e) {
        @if (e.status !== 409) {
          <div class="alert alert--bad" role="alert">
            {{ e.message }}
            @for (message of fieldMessages(e); track message) {
              <div>· {{ message }}</div>
            }
          </div>
        }
      }

      <div class="form__actions">
        <button class="btn" type="button" (click)="ref.close()">Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          {{ saving() ? 'Guardando…' : 'Guardar' }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .opt { color: var(--c-muted); font-weight: 400; }
    .check { display: flex; align-items: center; gap: 10px; min-height: 44px; cursor: pointer; }
    .check input { width: 18px; height: 18px; accent-color: var(--c-accent); }
  `,
})
export class RecurringExpenseDialog {
  protected readonly data = inject<RecurringExpenseDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<RecurringExpenseResponse>>(DialogRef);
  private readonly api = inject(RecurringExpensesApi);

  protected readonly daysOfMonth = DAY_OF_MONTH_OPTIONS;
  protected readonly weekdays = WEEKDAY_OPTIONS;

  private readonly existing = this.data.recurring;
  protected readonly editing = this.existing !== undefined;

  /** Active ones, plus whatever the template already uses (so editing never loses it silently). */
  protected readonly categories = this.data.categories.filter(
    (category) => category.isActive || category.id === this.existing?.categoryId,
  );
  protected readonly boxes = this.data.overview.cashBoxes.filter(
    (box) => box.isActive || box.id === this.existing?.cashBoxId,
  );
  protected readonly envelopes = this.data.overview.envelopes
    .map((row) => row.envelope)
    .filter(
      (envelope) =>
        (envelope.isActive || envelope.id === this.existing?.envelopeId) && envelope.role !== 'Replenishment',
    );

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.existing?.name ?? '', [Validators.required, Validators.maxLength(120)]],
    categoryId: [this.existing?.categoryId ?? '', Validators.required],
    amount: [
      this.existing ? toInputText(this.existing.amountCents) : '',
      [Validators.required, amountValidator({ min: 1 })],
    ],
    cashBoxId: [this.existing?.cashBoxId ?? this.defaultBoxId()],
    envelopeId: [this.existing?.envelopeId ?? this.defaultEnvelopeId()],
    frequency: [this.existing?.frequency ?? ('Monthly' as ExpenseFrequency)],
    // Kept as text: a <select> gives strings back; converted when saving.
    dueDay: [String(this.existing?.dueDay ?? 1)],
    startsOn: [this.existing?.startsOn ?? this.data.overview.date, Validators.required],
    notes: [this.existing?.notes ?? '', Validators.maxLength(500)],
    isActive: [this.existing?.isActive ?? true],
  });

  protected readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly currency = computed(
    () => this.boxes.find((box) => box.id === this.value().cashBoxId)?.currency ?? 'CUP',
  );

  /** Day 1 / Monday: both exist in the new list, so the select never shows a stale choice. */
  protected frequencyChanged(): void {
    this.form.controls.dueDay.setValue('1');
  }

  protected invalid(name: 'name' | 'categoryId' | 'amount' | 'startsOn'): boolean {
    const control = this.form.controls[name];
    const blank = name === 'name' && this.submitted() && !control.value.trim();
    return (control.invalid || blank) && (control.touched || this.submitted());
  }

  protected fieldMessages(error: ApiError): string[] {
    return Object.values(error.fields);
  }

  protected save(): void {
    this.submitted.set(true);
    const value = this.form.getRawValue();
    const amountCents = parseHundredths(value.amount);
    if (this.form.invalid || !value.name.trim() || amountCents === null) {
      return;
    }

    const request = {
      name: value.name.trim(),
      categoryId: value.categoryId,
      amountCents,
      cashBoxId: value.cashBoxId,
      envelopeId: value.envelopeId || null,
      frequency: value.frequency,
      dueDay: Number(value.dueDay),
      startsOn: value.startsOn,
      notes: value.notes.trim() || null,
    };
    const request$: Observable<RecurringExpenseResponse> = this.existing
      ? this.api.update(this.existing.id, { ...request, isActive: value.isActive })
      : this.api.create(request);

    this.saving.set(true);
    this.error.set(null);
    request$.subscribe({
      next: (saved) => this.ref.close(saved),
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  private defaultBoxId(): string {
    const cashCup = this.boxes.find((box) => box.kind === 'Cash' && box.currency === 'CUP');
    return (cashCup ?? this.boxes[0])?.id ?? '';
  }

  private defaultEnvelopeId(): string {
    return (this.envelopes.find((envelope) => envelope.role === 'Operations') ?? this.envelopes[0])?.id ?? '';
  }
}
