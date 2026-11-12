import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { EnvelopesApi } from '../../../core/api/envelopes-api';
import { EnvelopeResponse } from '../../../core/api/models';

export interface EnvelopeDialogData {
  /** Absent = create. */
  envelope?: EnvelopeResponse;
}

/**
 * Create or rename an envelope. Only the user's own envelopes can be turned off, and the server refuses it
 * while they still hold money or have a % in the latest plan (its message says which).
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ editing ? 'Editar sobre' : 'Nuevo sobre' }}</h2>
        <p class="card__lead">
          @if (editing) {
            El nombre es solo cómo lo ves. Lo que ya pasó en el sobre no cambia.
          } @else {
            Nace con 0%. Para que reciba plata, dale un porcentaje en el reparto y guardalo.
          }
        </p>
      </div>

      <label class="field">
        <span class="field__label">Nombre</span>
        <input class="input" formControlName="name" autocomplete="off" cdkFocusInitial
          placeholder="Ahorro para el refrigerador" [class.input--invalid]="nameInvalid()" />
        @if (nameInvalid()) {
          <span class="field__error">El nombre es obligatorio (hasta 60 letras).</span>
        } @else if (error()?.fields?.['name']) {
          <span class="field__error">{{ error()!.fields['name'] }}</span>
        }
      </label>

      @if (editing && data.envelope!.role === 'Custom') {
        <label class="check">
          <input type="checkbox" formControlName="isActive" />
          <span>Sobre activo</span>
        </label>
        <span class="field__hint">
          Para desactivarlo tiene que estar vacío y con 0% en el último reparto.
        </span>
      } @else if (editing) {
        <span class="field__hint">Los sobres de Profit First no se desactivan: si no lo usás, dejalo en 0%.</span>
      }

      @if (error(); as e) {
        @if (e.status !== 400) {
          <p class="alert alert--bad" role="alert">{{ e.message }}</p>
        }
      }

      <div class="form__actions">
        <button class="btn" type="button" (click)="ref.close()">Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          {{ saving() ? 'Guardando…' : editing ? 'Guardar' : 'Crear sobre' }}
        </button>
      </div>
    </form>
  `,
  styles: `
    .check { display: flex; align-items: center; gap: 8px; min-height: 44px; cursor: pointer; }
    .check input { width: 18px; height: 18px; accent-color: var(--c-accent); }
  `,
})
export class EnvelopeDialog {
  protected readonly data = inject<EnvelopeDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<EnvelopeResponse>>(DialogRef);
  private readonly api = inject(EnvelopesApi);

  protected readonly editing = !!this.data.envelope;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.envelope?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    isActive: [this.data.envelope?.isActive ?? true],
  });

  protected nameInvalid(): boolean {
    const name = this.form.controls.name;
    return (name.invalid || !name.value.trim()) && (name.touched || this.submitted());
  }

  protected save(): void {
    this.submitted.set(true);
    const { name, isActive } = this.form.getRawValue();
    if (this.form.invalid || !name.trim()) {
      return;
    }
    const request$: Observable<EnvelopeResponse> = this.editing
      ? this.api.update(this.data.envelope!.id, { name: name.trim(), isActive })
      : this.api.create({ name: name.trim() });

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
