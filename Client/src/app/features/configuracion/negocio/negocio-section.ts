import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toApiError } from '../../../core/api/api-error';
import { Weekday } from '../../../core/api/models';
import { BusinessSettingsStore } from '../../../core/state/business-settings-store';
import { amountValidator } from '../../../shared/format/money-validators';
import { parseHundredths, toInputText } from '../../../shared/format/money';
import { createSaveFeedback } from '../save-feedback';

/** Business data and close rules (RF-CFG-01, RF-CFG-02, RF-CFG-05). */
@Component({
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './negocio-section.html',
  styleUrl: './negocio-section.css',
})
export default class NegocioSection {
  protected readonly store = inject(BusinessSettingsStore);
  protected readonly feedback = createSaveFeedback();
  private readonly submitted = signal(false);

  /** Week starts on Monday on screen; the API uses .NET names. */
  protected readonly weekdays: { value: Weekday; short: string; long: string }[] = [
    { value: 'Monday', short: 'Lu', long: 'Lunes' },
    { value: 'Tuesday', short: 'Ma', long: 'Martes' },
    { value: 'Wednesday', short: 'Mi', long: 'Miércoles' },
    { value: 'Thursday', short: 'Ju', long: 'Jueves' },
    { value: 'Friday', short: 'Vi', long: 'Viernes' },
    { value: 'Saturday', short: 'Sá', long: 'Sábado' },
    { value: 'Sunday', short: 'Do', long: 'Domingo' },
  ];

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    address: ['', Validators.maxLength(300)],
    closedDays: [[] as Weekday[]],
    cashThreshold: ['', amountValidator({ min: 0, max: 1_000_000_00 })],
  });

  constructor() {
    // Fill the form when the settings arrive (or change elsewhere), unless the user is mid-edit.
    effect(() => {
      const settings = this.store.settings();
      if (settings && this.form.pristine) {
        this.form.reset({
          name: settings.name,
          address: settings.address ?? '',
          closedDays: settings.closedDays,
          cashThreshold: toInputText(settings.cashDifferenceThresholdCents),
        });
      }
    });
  }

  protected isClosed(day: Weekday): boolean {
    return this.form.controls.closedDays.value.includes(day);
  }

  protected toggleDay(day: Weekday): void {
    const control = this.form.controls.closedDays;
    const days = control.value;
    control.setValue(days.includes(day) ? days.filter((d) => d !== day) : [...days, day]);
    control.markAsDirty();
  }

  protected invalid(name: 'name' | 'address' | 'cashThreshold'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted());
  }

  protected save(): void {
    this.submitted.set(true);
    const threshold = parseHundredths(this.form.controls.cashThreshold.value);
    if (this.form.invalid || threshold === null) {
      return;
    }

    if (this.form.controls.closedDays.value.length === 7) {
      return; // the template already explains it; the API would answer 400 anyway
    }

    const value = this.form.getRawValue();
    this.feedback.start();
    this.store
      .save({
        name: value.name.trim(),
        address: value.address.trim() || null,
        closedDays: value.closedDays,
        cashDifferenceThresholdCents: threshold,
      })
      .subscribe({
        next: () => {
          this.form.markAsPristine();
          this.submitted.set(false);
          this.feedback.succeed();
        },
        error: (err: unknown) => this.feedback.fail(toApiError(err)),
      });
  }
}
