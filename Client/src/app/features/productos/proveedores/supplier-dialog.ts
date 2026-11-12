import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { SuppliersApi } from '../../../core/api/catalog-api';
import { SupplierResponse } from '../../../core/api/models';

export interface SupplierDialogData {
  /** Absent = create. */
  supplier?: SupplierResponse;
}

/** Create or edit a supplier (RF-PRV-01). Never deleted: deactivated instead. */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <h2 class="dialog__title">{{ editing ? 'Editar proveedor' : 'Nuevo proveedor' }}</h2>

      <label class="field">
        <span class="field__label">Nombre</span>
        <input class="input" formControlName="name" [class.input--invalid]="nameInvalid()" autocomplete="off"
          placeholder="Mayorista El Puerto" cdkFocusInitial />
        @if (nameInvalid()) {
          <span class="field__error">El nombre es obligatorio (hasta 80 letras).</span>
        } @else if (error()?.status === 409) {
          <span class="field__error">{{ error()!.message }}</span>
        }
      </label>

      <div class="form__row">
        <label class="field">
          <span class="field__label">Contacto <span class="opt">(opcional)</span></span>
          <input class="input" formControlName="contactName" autocomplete="off" />
        </label>
        <label class="field">
          <span class="field__label">Teléfono <span class="opt">(opcional)</span></span>
          <input class="input" formControlName="phone" autocomplete="off" inputmode="tel" />
        </label>
      </div>

      <label class="field">
        <span class="field__label">Notas <span class="opt">(opcional)</span></span>
        <textarea class="input textarea" formControlName="notes" rows="3"
          placeholder="Qué días entrega, cómo se le paga…"></textarea>
      </label>

      @if (editing) {
        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          <span>Proveedor activo</span>
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
    .opt { font-weight: 400; color: var(--c-muted); }
    .textarea { min-height: 84px; padding: 10px 12px; resize: vertical; }
    .check { display: flex; align-items: center; gap: 10px; min-height: 44px; cursor: pointer; }
    .check input { width: 18px; height: 18px; accent-color: var(--c-accent); }
  `,
})
export class SupplierDialog {
  protected readonly data = inject<SupplierDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<SupplierResponse>>(DialogRef);
  private readonly api = inject(SuppliersApi);

  protected readonly editing = this.data.supplier !== undefined;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.supplier?.name ?? '', [Validators.required, Validators.maxLength(80)]],
    contactName: [this.data.supplier?.contactName ?? '', Validators.maxLength(80)],
    phone: [this.data.supplier?.phone ?? '', Validators.maxLength(40)],
    notes: [this.data.supplier?.notes ?? '', Validators.maxLength(500)],
    isActive: [this.data.supplier?.isActive ?? true],
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

    const v = this.form.getRawValue();
    const fields = {
      name: v.name.trim(),
      contactName: v.contactName.trim() || null,
      phone: v.phone.trim() || null,
      notes: v.notes.trim() || null,
    };
    const request$: Observable<SupplierResponse> = this.editing
      ? this.api.update(this.data.supplier!.id, { ...fields, isActive: v.isActive })
      : this.api.create(fields);

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
