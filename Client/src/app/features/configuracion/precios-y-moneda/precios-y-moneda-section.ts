import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { toApiError } from '../../../core/api/api-error';
import { ExchangeRatesApi } from '../../../core/api/configuration-api';
import { ExchangeRateResponse } from '../../../core/api/models';
import { ExchangeRateDialog, ExchangeRateDialogData } from '../../../core/exchange-rate/exchange-rate-dialog';
import { BusinessSettingsStore } from '../../../core/state/business-settings-store';
import { ExchangeRateStore } from '../../../core/state/exchange-rate-store';
import { amountValidator } from '../../../shared/format/money-validators';
import {
  formatHundredths,
  formatMoney,
  formatPercent,
  markupToMarginBps,
  parseHundredths,
  toInputText,
} from '../../../shared/format/money';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { createSaveFeedback } from '../save-feedback';

/**
 * Pricing parameters (RF-CFG-06/07, D-07/D-08) and the USD→CUP rate history (RF-MON-02/03).
 * Price = cost USD × (purchase rate + colchón) × (1 + recargo), rounded to the step.
 */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './precios-y-moneda-section.html',
  styleUrl: './precios-y-moneda-section.css',
})
export default class PreciosYMonedaSection {
  private readonly dialog = inject(Dialog);
  private readonly ratesApi = inject(ExchangeRatesApi);
  protected readonly settingsStore = inject(BusinessSettingsStore);
  protected readonly rateStore = inject(ExchangeRateStore);
  protected readonly feedback = createSaveFeedback();
  private readonly submitted = signal(false);

  protected readonly formatHundredths = formatHundredths;
  protected readonly formatMoney = formatMoney;

  protected readonly history = signal<ExchangeRateResponse[]>([]);

  protected readonly form = inject(NonNullableFormBuilder).group({
    buffer: ['', amountValidator({ min: 0, max: 10_000_00 })],
    markup: ['', amountValidator({ min: 0, max: 100_000 })],
    rounding: ['', amountValidator({ min: 1, max: 1_000_00 })],
  });

  /** Live "margin" next to the markup, like the design ("20% (margen 16.7%)"). */
  private readonly markupText = toSignal(this.form.controls.markup.valueChanges, { initialValue: '' });
  protected readonly marginText = computed(() => {
    const markup = parseHundredths(this.markupText());
    return markup === null ? '' : `margen ${formatPercent(markupToMarginBps(markup))}`;
  });

  constructor() {
    effect(() => {
      const settings = this.settingsStore.settings();
      if (settings && this.form.pristine) {
        this.form.reset({
          buffer: toInputText(settings.exchangeBufferCents),
          markup: toInputText(settings.defaultMarkupBps),
          rounding: toInputText(settings.priceRoundingStepCents),
        });
      }
    });

    // Loads the history on arrival AND every time a rate is saved (here or from the menu):
    // the dialog reloads the store, the store emits a new `current`, this effect runs again.
    effect(() => {
      if (this.rateStore.current()) {
        this.loadHistory();
      }
    });
  }

  protected invalid(name: 'buffer' | 'markup' | 'rounding'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted());
  }

  protected save(): void {
    this.submitted.set(true);
    const buffer = parseHundredths(this.form.controls.buffer.value);
    const markup = parseHundredths(this.form.controls.markup.value);
    const rounding = parseHundredths(this.form.controls.rounding.value);
    if (this.form.invalid || buffer === null || markup === null || rounding === null) {
      return;
    }

    this.feedback.start();
    this.settingsStore
      .save({ exchangeBufferCents: buffer, defaultMarkupBps: markup, priceRoundingStepCents: rounding })
      .subscribe({
        next: () => {
          this.form.markAsPristine();
          this.submitted.set(false);
          this.feedback.succeed();
        },
        error: (err: unknown) => this.feedback.fail(toApiError(err)),
      });
  }

  /** Load today's rate, or correct any day of the history. */
  protected openRate(existing?: ExchangeRateResponse): void {
    const current = this.rateStore.current();
    if (current === null) {
      return;
    }

    const todayRate = current.isMissing ? null : current.latest;
    this.dialog.open<unknown, ExchangeRateDialogData>(ExchangeRateDialog, {
      ...DIALOG_DEFAULTS,
      data: {
        date: existing?.date ?? current.today,
        today: current.today,
        existing: existing ?? todayRate,
        reference: current.latest,
      },
    });
  }

  private loadHistory(): void {
    this.ratesApi.getHistory(1, 14).subscribe({
      next: (page) => this.history.set(page.items),
      error: () => this.history.set([]),
    });
  }
}
