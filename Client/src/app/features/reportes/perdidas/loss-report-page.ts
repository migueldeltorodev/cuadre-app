import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { LossReportResponse } from '../../../core/api/report-models';
import { ReportsApi } from '../../../core/api/reports-api';
import { formatShortDate } from '../../../shared/format/date';
import { formatMicros, formatMoney } from '../../../shared/format/money';
import { formatQuantityWithUnit, perUnit } from '../../../shared/format/quantity';
import { Period, PeriodFilter } from '../period-filter';
import { LOSS_LABELS } from '../report-format';

/**
 * RF-REP-08 (D-47, D-48): the merchandise lost (shrinkage, expired, damaged, short in a count) at cost, and the
 * shortages that were written off. House consumption and adjustments are not losses.
 */
@Component({
  imports: [RouterLink, PeriodFilter],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './loss-report-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../report-page.css', './loss-report-page.css'],
})
export default class LossReportPage implements OnInit {
  private readonly api = inject(ReportsApi);

  protected readonly formatMoney = formatMoney;
  protected readonly formatMicros = formatMicros;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly perUnit = perUnit;
  protected readonly lossLabels = LOSS_LABELS;

  protected readonly report = signal<LossReportResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);
  protected readonly today = signal<string | null>(null);

  /** Each kind with its share of the merchandise lost, biggest first. */
  protected readonly kinds = computed(() => {
    const r = this.report();
    if (!r || r.merchandiseCents <= 0) {
      return [];
    }
    return [...r.byKind]
      .sort((a, b) => b.costCents - a.costCents)
      .map((k) => ({ ...k, share: (k.costCents / r.merchandiseCents) * 100 }));
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(period?: Period): void {
    this.loading.set(true);
    this.api.losses(period).subscribe({
      next: (report) => {
        if (!this.today() && !period) {
          this.today.set(report.to);
        }
        this.report.set(report);
        this.loadError.set(null);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loadError.set(toApiError(err));
        this.loading.set(false);
      },
    });
  }
}
