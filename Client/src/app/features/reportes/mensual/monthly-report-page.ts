import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { MonthlyReportResponse } from '../../../core/api/report-models';
import { ReportsApi } from '../../../core/api/reports-api';
import { formatShortDate } from '../../../shared/format/date';
import { formatMoney, formatPercent } from '../../../shared/format/money';
import { StackedColumns } from '../../../shared/ui/charts/stacked-columns';
import { ENVELOPE_SWATCHES } from '../../sobres/envelope-format';
import {
  dayColumns,
  expensesLabel,
  growth,
  monthColumns,
  monthName,
  nextMonth,
  previousMonth,
} from '../report-format';

/**
 * RF-REP-05 and RF-REP-07: the closing of a month. A1, A2, A3 against the previous month, every day, the last 12
 * months, the split in envelopes, the expenses, the cash differences and the shrinkage.
 */
@Component({
  imports: [RouterLink, StackedColumns],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './monthly-report-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../report-page.css', './monthly-report-page.css'],
})
export default class MonthlyReportPage implements OnInit {
  private readonly api = inject(ReportsApi);

  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatShortDate = formatShortDate;
  protected readonly expensesLabel = expensesLabel;
  protected readonly monthName = monthName;
  protected readonly swatches = ENVELOPE_SWATCHES;

  protected readonly report = signal<MonthlyReportResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);

  /** The current month (learned from the first, default answer): no going beyond it. */
  private readonly current = signal<{ year: number; month: number } | null>(null);

  protected readonly isCurrent = computed(() => {
    const r = this.report();
    const c = this.current();
    return !!r && !!c && r.year === c.year && r.month === c.month;
  });

  /** The month before, from the trend (the server always sends 12, this one last). */
  protected readonly previous = computed(() => {
    const trend = this.report()?.trend ?? [];
    return trend.length >= 2 ? trend[trend.length - 2] : null;
  });

  protected readonly previousName = computed(() => {
    const p = this.previous();
    return p ? monthName(p.year, p.month) : '';
  });

  /** A1, A2, A3 against the previous month. */
  protected readonly changes = computed(() => {
    const r = this.report();
    const p = this.previous();
    if (!r || !p) {
      return { collected: null, cost: null, income: null };
    }
    const e = r.evaluation;
    return {
      collected: growth(e.collectedCents, p.collectedCents),
      cost: growth(e.costOfSalesCents, p.costOfSalesCents),
      income: growth(e.realIncomeCents, p.realIncomeCents),
    };
  });

  protected readonly dayColumns = computed(() => dayColumns(this.report()?.days ?? []));
  protected readonly monthColumns = computed(() => monthColumns(this.report()?.trend ?? []));
  protected readonly hasTrend = computed(() => (this.report()?.trend ?? []).some((m) => m.closedDays > 0));

  protected readonly percentEnvelopes = computed(
    () => this.report()?.evaluation.envelopes.filter((e) => e.role !== 'Replenishment') ?? [],
  );

  protected readonly replenishment = computed(
    () => this.report()?.evaluation.envelopes.find((e) => e.role === 'Replenishment') ?? null,
  );

  ngOnInit(): void {
    this.load();
  }

  protected go(step: -1 | 1): void {
    const r = this.report();
    if (!r) {
      return;
    }
    const target = step < 0 ? previousMonth(r.year, r.month) : nextMonth(r.year, r.month);
    this.load(target.year, target.month);
  }

  protected load(year?: number, month?: number): void {
    this.loading.set(true);
    this.api.monthly(year, month).subscribe({
      next: (report) => {
        if (!this.current() && year === undefined) {
          this.current.set({ year: report.year, month: report.month });
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
