import { Dialog } from '@angular/cdk/dialog';
import { KeyValuePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { StaffApi } from '../../../core/api/closings-api';
import { EmployeeResponse, StaffPaySchemeResponse } from '../../../core/api/models';
import { ExchangeRateStore } from '../../../core/state/exchange-rate-store';
import { formatMoney, formatPercent, parseHundredths, toInputText } from '../../../shared/format/money';
import { amountValidator } from '../../../shared/format/money-validators';
import { DIALOG_DEFAULTS } from '../../../shared/ui/dialog-config';
import { planStartLabel } from '../../sobres/envelope-format';
import { createSaveFeedback } from '../save-feedback';
import { EmployeeDialog, EmployeeDialogData } from './employee-dialog';

/** 'yyyy-MM-dd' of the browser's day: only a fallback until the server's "today" arrives. */
function localToday(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

/**
 * Configuración › Personal (RF-PER-01/02, RF-CIE-14): who works the counter and how the post is paid.
 * A new pay scheme starts on a date; the days already closed keep what they paid.
 */
@Component({
  imports: [KeyValuePipe, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './personal-section.html',
  styles: `
    :host { display: flex; flex-direction: column; gap: 20px; }
    .card { display: flex; flex-direction: column; gap: 12px; }
    .current { margin: 0; font-size: 18px; font-weight: 600; }
    .scheme-form { display: flex; flex-direction: column; gap: 16px; padding-top: 8px; border-top: 1px solid var(--c-line-soft); }
    .history { margin: 0; padding: 0; list-style: none; display: flex; flex-direction: column; gap: 4px; font-size: 13.5px; color: var(--c-muted); }
    .ok { color: var(--c-ok-ink); }
  `,
})
export default class PersonalSection implements OnInit {
  private readonly dialog = inject(Dialog);
  private readonly api = inject(StaffApi);
  private readonly rate = inject(ExchangeRateStore);

  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly planStartLabel = planStartLabel;

  protected readonly employees = signal<EmployeeResponse[] | null>(null);
  protected readonly schemes = signal<StaffPaySchemeResponse[]>([]);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly feedback = createSaveFeedback();
  private readonly submitted = signal(false);

  protected readonly current = computed(() => this.schemes()[0] ?? null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    effectiveFrom: ['', Validators.required],
    fixed: ['', amountValidator({ min: 0 })],
    percent: ['', amountValidator({ min: 0, max: 10_000 })],
    notes: ['', Validators.maxLength(300)],
  });

  ngOnInit(): void {
    this.reload();
  }

  protected openEmployee(employee?: EmployeeResponse): void {
    this.dialog
      .open<EmployeeResponse, EmployeeDialogData>(EmployeeDialog, { ...DIALOG_DEFAULTS, data: { employee } })
      .closed.subscribe((saved) => saved && this.reload());
  }

  protected showError(control: 'effectiveFrom' | 'fixed' | 'percent'): boolean {
    const c = this.form.controls[control];
    return c.invalid && (c.touched || this.submitted());
  }

  protected saveScheme(): void {
    this.submitted.set(true);
    const value = this.form.getRawValue();
    const fixed = parseHundredths(value.fixed);
    const percent = parseHundredths(value.percent);
    if (this.form.invalid || fixed === null || percent === null) {
      return;
    }

    this.feedback.start();
    this.api
      .createPayScheme({
        effectiveFrom: value.effectiveFrom,
        dailyFixedCents: fixed,
        percentOfSalesBps: percent,
        notes: value.notes.trim() || null,
      })
      .subscribe({
        next: () => {
          this.feedback.succeed();
          this.submitted.set(false);
          this.reload();
        },
        error: (err: unknown) => this.feedback.fail(toApiError(err)),
      });
  }

  private reload(): void {
    forkJoin({ employees: this.api.employees(), schemes: this.api.paySchemes() }).subscribe({
      next: ({ employees, schemes }) => {
        this.employees.set(employees.items);
        this.schemes.set(schemes.items);
        this.loadError.set(null);
        const current = schemes.items[0];
        this.form.reset({
          effectiveFrom: this.rate.current()?.today ?? localToday(),
          fixed: current ? toInputText(current.dailyFixedCents) : '',
          percent: current ? toInputText(current.percentOfSalesBps) : '',
          notes: '',
        });
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }
}
