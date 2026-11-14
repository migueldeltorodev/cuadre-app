import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { TaxBaseCode, TaxResponse } from '../../../core/api/models';
import { TaxesApi } from '../../../core/api/taxes-api';
import { parseHundredths, toInputText } from '../../../shared/format/money';

export interface TaxDialogData {
  /** Absent = create. */
  tax?: TaxResponse;
}

/** Add or change a tax (D-62). Never deleted: deactivated. */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ editing ? 'Editar impuesto' : 'Nuevo impuesto' }}</h2>
        <p class="card__lead">
          Solo sugiere cuánto apartar. El reparto de los sobres no cambia: lo ves en el resumen para el contador y
          junto al sobre Impuestos.
        </p>
      </div>

      <label class="field">
        <span class="field__label">Nombre</span>
        <input class="input" formControlName="name" [class.input--invalid]="nameInvalid()" autocomplete="off"
          maxlength="60" placeholder="Ej.: Impuesto sobre las ventas" cdkFocusInitial />
        @if (nameInvalid()) {
          <span class="field__error">El nombre es obligatorio (hasta 60 letras).</span>
        } @else if (error()?.status === 409) {
          <span class="field__error">{{ error()!.message }}</span>
        }
      </label>

      <label class="field">
        <span class="field__label">Tasa</span>
        <span class="input input--affix" [class.input--invalid]="rateInvalid()">
          <input formControlName="rate" inputmode="decimal" autocomplete="off" aria-label="Tasa en por ciento" />
          <span class="input__affix">%</span>
        </span>
        @if (rateInvalid()) {
          <span class="field__error">Escribí un porcentaje mayor que 0 y hasta 100 (ej.: 10 o 2.5).</span>
        }
      </label>

      <fieldset class="field choices">
        <legend class="field__label">Se calcula sobre</legend>
        <label class="choice">
          <input type="radio" formControlName="base" value="Sales" />
          <span><strong>Las ventas</strong> <span class="field__hint">· lo cobrado en el mes (A1)</span></span>
        </label>
        <label class="choice">
          <input type="radio" formControlName="base" value="Profit" />
          <span>
            <strong>La ganancia</strong>
            <span class="field__hint">· la ganancia antes de impuestos del resumen; si hubo pérdida, nada</span>
          </span>
        </label>
      </fieldset>

      @if (editing) {
        <label class="choice">
          <input type="checkbox" formControlName="isActive" />
          <span>Activo (si no, deja de sugerirse)</span>
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
    .choices { margin: 0; padding: 0; border: 0; }
    .choice { display: flex; align-items: center; gap: 10px; min-height: 44px; cursor: pointer; }
    .choice input { flex: none; width: 18px; height: 18px; accent-color: var(--c-accent); }
  `,
})
export class TaxDialog {
  protected readonly data = inject<TaxDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<TaxResponse>>(DialogRef);
  private readonly api = inject(TaxesApi);

  protected readonly editing = this.data.tax !== undefined;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.tax?.name ?? '', [Validators.required, Validators.maxLength(60)]],
    rate: [this.data.tax ? toInputText(this.data.tax.rateBps) : ''],
    base: [this.data.tax?.base ?? ('Sales' as TaxBaseCode)],
    isActive: [this.data.tax?.isActive ?? true],
  });

  protected nameInvalid(): boolean {
    const name = this.form.controls.name;
    return (name.invalid || (this.submitted() && !name.value.trim())) && (name.touched || this.submitted());
  }

  protected rateInvalid(): boolean {
    return (this.form.controls.rate.touched || this.submitted()) && this.rateBps() === null;
  }

  /** "10" → 1000 basis points; null when it is not a percentage above 0 and up to 100. */
  private rateBps(): number | null {
    const bps = parseHundredths(this.form.controls.rate.value);
    return bps !== null && bps > 0 && bps <= 10_000 ? bps : null;
  }

  protected save(): void {
    this.submitted.set(true);
    const value = this.form.getRawValue();
    const name = value.name.trim();
    const rateBps = this.rateBps();
    if (this.form.invalid || !name || rateBps === null) {
      return;
    }

    const request$: Observable<TaxResponse> = this.editing
      ? this.api.update(this.data.tax!.id, { name, rateBps, base: value.base, isActive: value.isActive })
      : this.api.create({ name, rateBps, base: value.base });

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
