import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { ExpensesApi, RecurringExpensesApi } from '../../core/api/expenses-api';
import {
  DueExpenseResponse,
  EnvelopeOverviewResponse,
  ExpenseCategoryResponse,
  ExpenseResponse,
} from '../../core/api/models';
import { formatShortDate } from '../../shared/format/date';
import { formatMoney, parseHundredths, toInputText } from '../../shared/format/money';
import { amountValidator } from '../../shared/format/money-validators';
import { formatBalance } from '../sobres/envelope-format';
import { duePeriodLabel } from './expense-format';

interface FormValue {
  description: string;
  categoryId: string;
  cashBoxId: string;
  envelopeId: string;
  date: string;
  amount: string;
  notes: string;
}

export interface ExpenseDialogData {
  /** Today's balances: the boxes, the envelopes and what each holds (display only). */
  overview: EnvelopeOverviewResponse;
  categories: ExpenseCategoryResponse[];
  /** Paying an occurrence of a recurring expense: its category, concept and envelope are fixed. */
  due?: DueExpenseResponse;
}

/**
 * Register a gasto (RF-GAS-02) or pay a recurring one (RF-GAS-01). Saved = confirmed: the money leaves the box and
 * the envelope. Leaving the envelope below zero is allowed; the page shows the server's warning afterwards.
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        @if (due; as d) {
          <h2 class="dialog__title">Pagar {{ d.recurring.name }}</h2>
          <p class="card__lead">
            {{ periodLabel }} · vencía el {{ formatShortDate(d.dueDate) }}. Se registra como gasto de
            {{ d.recurring.categoryName }}.
          </p>
        } @else {
          <h2 class="dialog__title">Registrar un gasto</h2>
          <p class="card__lead">Plata que sale del negocio para que funcione: el gestor, un flete, la luz. La mercadería va por Compras.</p>
        }
      </div>

      @if (!due) {
        <div class="form__row">
          <label class="field">
            <span class="field__label">Qué se pagó</span>
            <input class="input" formControlName="description" autocomplete="off" maxlength="200"
              placeholder="Flete del arroz" cdkFocusInitial [class.input--invalid]="invalid('description')" />
            @if (invalid('description')) {
              <span class="field__error">Escribí qué se pagó.</span>
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
      }

      <div class="form__row">
        <label class="field">
          <span class="field__label">Caja</span>
          <select class="input" formControlName="cashBoxId">
            @for (box of boxes; track box.id) {
              <option [value]="box.id">{{ box.name }}</option>
            }
          </select>
        </label>
        <label class="field">
          <span class="field__label">Fecha</span>
          <input class="input num" type="date" formControlName="date" [max]="today"
            [class.input--invalid]="invalid('date')" />
          @if (due) {
            <span class="field__hint">El día en que salió la plata.</span>
          }
        </label>
      </div>

      @if (!due) {
        <label class="field">
          <span class="field__label">Sale del sobre</span>
          <select class="input" formControlName="envelopeId">
            @for (envelope of envelopes; track envelope.id) {
              <option [value]="envelope.id">{{ envelope.name }}</option>
            }
          </select>
          <span class="field__hint">Reposición no paga gastos: es solo para volver a comprar mercadería.</span>
        </label>
      }

      <p class="available" [class.available--low]="leftAfter() !== null && leftAfter()! < 0">
        Hay en {{ envelopeName() }} · {{ boxName() }}: <strong class="num">{{ formatBalance(available(), currency()) }}</strong>
        @if (leftAfter() !== null && leftAfter()! < 0) {
          <span>· quedaría en {{ formatBalance(leftAfter()!, currency()) }}</span>
        }
      </p>

      <label class="field">
        <span class="field__label">Monto</span>
        <span class="input input--affix" [class.input--invalid]="invalid('amount')">
          <input formControlName="amount" inputmode="decimal" autocomplete="off" class="num"
            [attr.cdkFocusInitial]="due ? '' : null" />
          <span>{{ currency() }}</span>
        </span>
        @if (invalid('amount')) {
          <span class="field__error">Escribí un monto mayor que cero.</span>
        } @else if (due && amountChanged()) {
          <span class="field__hint">El gasto fijo dice {{ formatMoney(due.recurring.amountCents, due.recurring.currency) }}: se paga lo que escribas acá.</span>
        }
      </label>

      <label class="field">
        <span class="field__label">Nota <span class="opt">(opcional)</span></span>
        <input class="input" formControlName="notes" autocomplete="off" maxlength="500" />
      </label>

      @if (error(); as e) {
        <div class="alert alert--bad" role="alert">
          {{ e.message }}
          @for (message of fieldMessages(e); track message) {
            <div>· {{ message }}</div>
          }
        </div>
      }

      <div class="form__actions">
        <button class="btn" type="button" (click)="ref.close()">Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          {{ saving() ? 'Guardando…' : due ? 'Pagar' : 'Registrar el gasto' }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .available { margin: 0; font-size: 14px; color: var(--c-ink-2); }
    .available--low { color: var(--c-bad-ink); }
    .opt { color: var(--c-muted); font-weight: 400; }
  `,
})
export class ExpenseDialog {
  protected readonly data = inject<ExpenseDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<ExpenseResponse>>(DialogRef);
  private readonly expensesApi = inject(ExpensesApi);
  private readonly recurringApi = inject(RecurringExpensesApi);

  protected readonly formatBalance = formatBalance;
  protected readonly formatMoney = formatMoney;
  protected readonly formatShortDate = formatShortDate;

  protected readonly due = this.data.due;
  protected readonly periodLabel = this.due ? duePeriodLabel(this.due) : '';
  protected readonly today = this.data.overview.date;

  protected readonly categories = this.data.categories.filter((category) => category.isActive);
  protected readonly boxes = this.data.overview.cashBoxes.filter(
    (box) => box.isActive || box.id === this.due?.recurring.cashBoxId,
  );
  protected readonly envelopes = this.data.overview.envelopes
    .map((row) => row.envelope)
    .filter((envelope) => envelope.isActive && envelope.role !== 'Replenishment');

  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    description: ['', this.due ? [] : [Validators.required, Validators.maxLength(200)]],
    categoryId: [this.defaultCategoryId(), this.due ? [] : [Validators.required]],
    cashBoxId: [this.due?.recurring.cashBoxId ?? this.defaultBoxId()],
    envelopeId: [this.due?.recurring.envelopeId ?? this.defaultEnvelopeId()],
    date: [this.today, Validators.required],
    amount: [this.due ? toInputText(this.due.recurring.amountCents) : '', [Validators.required, amountValidator({ min: 1 })]],
    notes: ['', Validators.maxLength(500)],
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  private readonly box = computed(() => this.boxes.find((box) => box.id === this.value().cashBoxId));
  protected readonly boxName = computed(() => this.box()?.name ?? '');
  protected readonly currency = computed(() => this.box()?.currency ?? 'CUP');

  protected readonly envelopeName = computed(
    () =>
      this.data.overview.envelopes.find((row) => row.envelope.id === this.value().envelopeId)?.envelope.name ??
      this.due?.recurring.envelopeName ??
      '',
  );

  /** Today's balance of the envelope in the chosen box (display only: the server decides). */
  protected readonly available = computed(() => {
    const row = this.data.overview.envelopes.find((r) => r.envelope.id === this.value().envelopeId);
    return row?.balances.find((b) => b.cashBoxId === this.value().cashBoxId)?.amountCents ?? 0;
  });

  protected readonly leftAfter = computed(() => {
    const amount = parseHundredths(this.value().amount);
    return amount === null ? null : this.available() - amount;
  });

  protected readonly amountChanged = computed(() => {
    const amount = parseHundredths(this.value().amount);
    return this.due !== undefined && amount !== null && amount !== this.due.recurring.amountCents;
  });

  protected invalid(name: 'description' | 'categoryId' | 'date' | 'amount'): boolean {
    const control = this.form.controls[name];
    const blank = name === 'description' && !this.due && this.submitted() && !control.value.trim();
    return (control.invalid || blank) && (control.touched || this.submitted());
  }

  protected fieldMessages(error: ApiError): string[] {
    return Object.values(error.fields);
  }

  protected save(): void {
    this.submitted.set(true);
    const value = this.form.getRawValue();
    if (this.form.invalid || (!this.due && !value.description.trim())) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.request(value).subscribe({
      next: (saved) => this.ref.close(saved),
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }

  private request(value: FormValue): Observable<ExpenseResponse> {
    const amountCents = parseHundredths(value.amount)!;
    const notes = value.notes.trim() || null;
    if (this.due) {
      return this.recurringApi.confirm(this.due.recurring.id, {
        periodStart: this.due.periodStart,
        date: value.date,
        amountCents,
        cashBoxId: value.cashBoxId,
        notes,
      });
    }
    return this.expensesApi.create({
      date: value.date,
      categoryId: value.categoryId,
      description: value.description.trim(),
      amountCents,
      cashBoxId: value.cashBoxId,
      envelopeId: value.envelopeId || null,
      notes,
    });
  }

  private defaultCategoryId(): string {
    return this.due?.recurring.categoryId ?? '';
  }

  /** Cash in CUP first: most expenses are paid in cash. */
  private defaultBoxId(): string {
    const cashCup = this.boxes.find((box) => box.kind === 'Cash' && box.currency === 'CUP');
    return (cashCup ?? this.boxes[0])?.id ?? '';
  }

  private defaultEnvelopeId(): string {
    return (this.envelopes.find((envelope) => envelope.role === 'Operations') ?? this.envelopes[0])?.id ?? '';
  }
}
