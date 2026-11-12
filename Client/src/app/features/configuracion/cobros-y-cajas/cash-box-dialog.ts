import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { CashBoxesApi } from '../../../core/api/configuration-api';
import { CashBoxResponse, CashBoxType, CurrencyCode } from '../../../core/api/models';
import { CASH_BOX_TYPE_LABELS } from './labels';

export interface CashBoxDialogData {
  /** Absent = create. */
  cashBox?: CashBoxResponse;
}

/**
 * Create or edit a "caja". Currency and type can only be chosen on creation:
 * changing them later would corrupt every balance recorded in the box.
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ editing ? 'Editar caja' : 'Nueva caja' }}</h2>
        <p class="card__lead">Una caja es <strong>dónde</strong> está la plata. Para qué es, lo dicen los sobres.</p>
      </div>

      <label class="field">
        <span class="field__label">Nombre</span>
        <input class="input" formControlName="name" [class.input--invalid]="nameInvalid()" autocomplete="off"
          cdkFocusInitial />
        @if (nameInvalid()) {
          <span class="field__error">El nombre es obligatorio (hasta 60 letras).</span>
        } @else if (error()?.status === 409) {
          <span class="field__error">{{ error()!.message }}</span>
        }
      </label>

      @if (editing) {
        <div class="field">
          <span class="field__label">Moneda y tipo</span>
          <span>{{ data.cashBox!.currency }} · {{ typeLabels[data.cashBox!.kind] }}</span>
          <span class="field__hint">No se pueden cambiar: si te equivocaste, desactivá esta caja y creá otra.</span>
        </div>

        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          <span>Caja activa</span>
        </label>
      } @else {
        <div class="form__row">
          <label class="field">
            <span class="field__label">Moneda</span>
            <select class="input" formControlName="currency">
              <option value="CUP">CUP</option>
              <option value="USD">USD</option>
            </select>
          </label>
          <label class="field">
            <span class="field__label">Tipo</span>
            <select class="input" formControlName="kind">
              <option value="Cash">{{ typeLabels.Cash }}</option>
              <option value="Bank">{{ typeLabels.Bank }}</option>
            </select>
          </label>
        </div>
        <span class="field__hint">
          Banco = plata que no se puede usar en el momento (transferencias). Va primero al sobre de Reposición.
        </span>
      }

      @if (error(); as e) {
        @if (e.status !== 409) {
          <p class="alert alert--bad" role="alert">{{ e.message }}</p>
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
    .check { display: flex; align-items: center; gap: 10px; min-height: 44px; cursor: pointer; }
    .check input { width: 18px; height: 18px; accent-color: var(--c-accent); }
  `,
})
export class CashBoxDialog {
  protected readonly data = inject<CashBoxDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<CashBoxResponse>>(DialogRef);
  private readonly api = inject(CashBoxesApi);

  protected readonly editing = this.data.cashBox !== undefined;
  protected readonly typeLabels = CASH_BOX_TYPE_LABELS;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.cashBox?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    currency: ['CUP' as CurrencyCode],
    kind: ['Cash' as CashBoxType],
    isActive: [this.data.cashBox?.isActive ?? true],
  });

  protected nameInvalid(): boolean {
    const name = this.form.controls.name;
    return name.invalid && (name.touched || this.submitted());
  }

  protected save(): void {
    this.submitted.set(true);
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();
    const name = value.name.trim();
    const request$: Observable<CashBoxResponse> = this.editing
      ? this.api.update(this.data.cashBox!.id, { name, isActive: value.isActive })
      : this.api.create({ name, currency: value.currency, kind: value.kind });

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
}
