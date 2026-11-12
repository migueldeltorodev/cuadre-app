import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiError, toApiError } from '../api/api-error';
import { ExchangeRatesApi } from '../api/configuration-api';
import { ExchangeRateResponse } from '../api/models';
import { ExchangeRateStore } from '../state/exchange-rate-store';
import { amountValidator } from '../../shared/format/money-validators';
import { formatMoney, parseHundredths, toInputText } from '../../shared/format/money';

export interface ExchangeRateDialogData {
  /** Day to load or correct (yyyy-MM-dd). */
  date: string;
  /** Server "today": the date input cannot go past it. */
  today: string;
  /** Rate already stored for `date`, if any (correction mode). */
  existing?: ExchangeRateResponse | null;
  /** Latest known rate, shown as a reference when loading a new day. */
  reference?: ExchangeRateResponse | null;
}

/** Load or correct the informal USD→CUP rate of one day (RF-MON-02). Opened from the menu and from settings. */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="dialog" [formGroup]="form" (ngSubmit)="save()" novalidate>
      <div>
        <h2 class="dialog__title">{{ data.existing ? 'Corregir la tasa' : 'Cargar la tasa del día' }}</h2>
        <p class="card__lead">Referencia: elTOQUE. Un valor por día; si lo cargás de nuevo, se corrige.</p>
      </div>

      <div class="form__row">
        <label class="field">
          <span class="field__label">Día</span>
          <input class="input" type="date" formControlName="date" [max]="data.today" />
        </label>

        <label class="field">
          <span class="field__label">1 USD equivale a</span>
          <span class="input input--affix" [class.input--invalid]="showError()">
            <input formControlName="rate" inputmode="decimal" autocomplete="off" cdkFocusInitial
              aria-label="Pesos cubanos por 1 dólar" />
            <span class="input__affix">CUP</span>
          </span>
          @if (showError()) {
            <span class="field__error">Escribí la tasa, por ejemplo 410 o 410.50.</span>
          } @else if (data.reference && !data.existing) {
            <span class="field__hint">
              Última cargada: {{ formatMoney(data.reference.cupPerUsdCents) }} ({{ data.reference.date }})
            </span>
          }
        </label>
      </div>

      @if (error(); as e) {
        <p class="alert alert--bad" role="alert">{{ e.fields['cupPerUsdCents'] ?? e.message }}</p>
      }

      <div class="form__actions">
        <button class="btn" type="button" (click)="ref.close()">Cancelar</button>
        <button class="btn btn--primary" type="submit" [disabled]="saving()">
          {{ saving() ? 'Guardando…' : 'Guardar tasa' }}
        </button>
      </div>
    </form>
  `,
})
export class ExchangeRateDialog {
  protected readonly data = inject<ExchangeRateDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<ExchangeRateResponse>>(DialogRef);
  private readonly api = inject(ExchangeRatesApi);
  private readonly store = inject(ExchangeRateStore);

  protected readonly formatMoney = formatMoney;
  protected readonly saving = signal(false);
  protected readonly error = signal<ApiError | null>(null);
  private readonly submitted = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    date: [this.data.date, Validators.required],
    rate: [
      this.data.existing ? toInputText(this.data.existing.cupPerUsdCents) : '',
      amountValidator({ min: 1 }),
    ],
  });

  protected showError(): boolean {
    const rate = this.form.controls.rate;
    return rate.invalid && (rate.touched || this.submitted());
  }

  protected save(): void {
    this.submitted.set(true);
    const cents = parseHundredths(this.form.controls.rate.value);
    if (this.form.invalid || cents === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.api.upsert(this.form.controls.date.value, cents).subscribe({
      next: (saved) => {
        this.store.load();
        this.ref.close(saved);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err));
        this.saving.set(false);
      },
    });
  }
}
