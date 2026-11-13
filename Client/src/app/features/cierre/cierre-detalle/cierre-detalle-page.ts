import { ChangeDetectionStrategy, Component, OnInit, computed, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { DailyClosingsApi } from '../../../core/api/closings-api';
import { DailyClosingResponse } from '../../../core/api/models';
import { formatShortDate, formatTimestampDay } from '../../../shared/format/date';
import { formatMoney } from '../../../shared/format/money';
import { formatQuantityWithUnit } from '../../../shared/format/quantity';
import { closingStateLabel, discrepancyText, formatCup } from '../closing-format';
import { ClosingSummary } from '../closing-summary';
import { Breadcrumbs } from '../../../core/layout/breadcrumbs';

/**
 * One closed day: its figures, what to put in each envelope and the count. Never edited. Reopening (only the
 * latest closed day, RF-CIE-10) asks for a reason and reverses its movements on that same day.
 */
@Component({
  imports: [ReactiveFormsModule, RouterLink, ClosingSummary],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cierre-detalle-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', './cierre-detalle-page.css'],
})
export default class CierreDetallePage implements OnInit {
  /** Route param (withComponentInputBinding). */
  readonly id = input.required<string>();

  private readonly api = inject(DailyClosingsApi);

  protected readonly formatShortDate = formatShortDate;
  protected readonly formatTimestampDay = formatTimestampDay;
  protected readonly formatMoney = formatMoney;
  protected readonly formatCup = formatCup;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly discrepancyText = discrepancyText;

  /** Set by the wizard when it navigates here right after closing. */
  protected readonly justClosed = !!inject(Router).currentNavigation()?.extras.state?.['justClosed'];

  protected readonly closing = signal<DailyClosingResponse | null>(null);

  /** The trail names this page once it is loaded (the shell draws it). */
  private readonly crumbs = inject(Breadcrumbs);
  protected readonly nameInTrail = effect(() => {
    const closing = this.closing();
    if (closing) {
      this.crumbs.set(formatShortDate(closing.date), [{ label: 'Cierres anteriores', link: '/cierre/historial' }]);
    }
  });
  protected readonly loadError = signal<ApiError | null>(null);

  protected readonly reopening = signal(false);
  protected readonly savingReopen = signal(false);
  protected readonly reopenError = signal<ApiError | null>(null);
  protected readonly reason = inject(NonNullableFormBuilder).control('', [
    Validators.required,
    Validators.maxLength(300),
  ]);

  protected readonly state = computed(() => {
    const closing = this.closing();
    return closing ? closingStateLabel(closing.status) : null;
  });

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: (closing) => this.closing.set(closing),
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
  }

  protected startReopen(): void {
    this.reason.reset('');
    this.reopenError.set(null);
    this.reopening.set(true);
  }

  protected cancelReopen(): void {
    this.reopening.set(false);
  }

  protected confirmReopen(): void {
    const closing = this.closing();
    this.reason.markAsTouched();
    if (!closing || this.reason.invalid || !this.reason.value.trim()) {
      return;
    }
    this.savingReopen.set(true);
    this.reopenError.set(null);
    this.api.reopen(closing.id, this.reason.value.trim()).subscribe({
      next: (reopened) => {
        this.closing.set(reopened);
        this.reopening.set(false);
        this.savingReopen.set(false);
      },
      error: (err: unknown) => {
        this.reopenError.set(toApiError(err));
        this.savingReopen.set(false);
      },
    });
  }
}
