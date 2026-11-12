import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { PaymentMethodsApi } from '../../../core/api/configuration-api';
import { CashBoxResponse, PaymentMethodResponse } from '../../../core/api/models';
import { CASH_BOX_TYPE_LABELS } from './labels';

export interface PaymentMethodDialogData {
  /** Absent = create. */
  paymentMethod?: PaymentMethodResponse;
  cashBoxes: CashBoxResponse[];
}

/** Create or edit a "forma de cobro" and choose the cash box its money lands in (RF-CFG-03, RF-CAJ-02). */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <h2 class="dialog__title">{{ editing ? 'Editar forma de cobro' : 'Nueva forma de cobro' }}</h2>

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

      <label class="field">
        <span class="field__label">La plata entra a la caja</span>
        <select class="input" formControlName="cashBoxId">
          @for (box of selectableBoxes; track box.id) {
            <option [value]="box.id">
              {{ box.name }} · {{ box.currency }} · {{ typeLabels[box.kind] }}{{ box.isActive ? '' : ' (inactiva)' }}
            </option>
          }
        </select>
      </label>

      @if (editing) {
        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          <span>Forma de cobro activa</span>
        </label>
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
export class PaymentMethodDialog {
  protected readonly data = inject<PaymentMethodDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<PaymentMethodResponse>>(DialogRef);
  private readonly api = inject(PaymentMethodsApi);

  protected readonly editing = this.data.paymentMethod !== undefined;
  protected readonly typeLabels = CASH_BOX_TYPE_LABELS;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  /** Active boxes, plus the current one even if inactive (so the select never shows an empty value). */
  protected readonly selectableBoxes = this.data.cashBoxes.filter(
    (box) => box.isActive || box.id === this.data.paymentMethod?.cashBoxId,
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.paymentMethod?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    cashBoxId: [this.data.paymentMethod?.cashBoxId ?? this.selectableBoxes[0]?.id ?? '', Validators.required],
    isActive: [this.data.paymentMethod?.isActive ?? true],
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
    const request$: Observable<PaymentMethodResponse> = this.editing
      ? this.api.update(this.data.paymentMethod!.id, { name, cashBoxId: value.cashBoxId, isActive: value.isActive })
      : this.api.create({ name, cashBoxId: value.cashBoxId });

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
