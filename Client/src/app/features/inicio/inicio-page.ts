import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { AppInfoService } from '../../core/api/app-info.service';
import { EnvelopesApi } from '../../core/api/envelopes-api';
import { EnvelopeOverviewResponse } from '../../core/api/models';
import { DashboardResponse } from '../../core/api/report-models';
import { DashboardApi } from '../../core/api/reports-api';
import { ClosingAlertsStore } from '../../core/state/closing-alerts-store';
import { formatShortDate } from '../../shared/format/date';
import { formatHundredths, formatMoney, formatPercent } from '../../shared/format/money';
import { formatQuantityWithUnit } from '../../shared/format/quantity';
import { StackedColumns } from '../../shared/ui/charts/stacked-columns';
import { dayColumns, differenceLabel, expensesLabel, growth, spanLabel } from '../reportes/report-format';
import { ENVELOPE_SWATCHES, addDays, formatBalance, totalsByCurrency } from '../sobres/envelope-format';

const WEEKDAY_DATE = new Intl.DateTimeFormat('es', { weekday: 'long', day: 'numeric', month: 'long' });

/**
 * RF-REP-01: the home screen. What the closing still asks for, how the month goes against the same days of the
 * last one, the last 30 closings, the envelopes, and what needs a look (low stock, prices beyond the colchón).
 * The server sums everything; this page only compares and draws.
 */
@Component({
  imports: [RouterLink, StackedColumns],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './inicio-page.html',
  styleUrl: './inicio-page.css',
})
export default class InicioPage implements OnInit {
  private readonly dashboardApi = inject(DashboardApi);
  private readonly envelopesApi = inject(EnvelopesApi);
  private readonly appInfo = inject(AppInfoService);
  protected readonly alerts = inject(ClosingAlertsStore);

  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatShortDate = formatShortDate;
  protected readonly formatHundredths = formatHundredths;
  protected readonly formatQuantityWithUnit = formatQuantityWithUnit;
  protected readonly formatBalance = formatBalance;
  protected readonly differenceLabel = differenceLabel;
  protected readonly expensesLabel = expensesLabel;
  protected readonly swatches = ENVELOPE_SWATCHES;

  protected readonly dashboard = signal<DashboardResponse | null>(null);
  protected readonly overview = signal<EnvelopeOverviewResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);

  /** The version in the footer; null when the server does not answer. */
  protected readonly version = toSignal(
    this.appInfo.getInfo().pipe(
      map((info) => `v${info.version}`),
      catchError(() => of(null)),
    ),
    { initialValue: null },
  );

  protected readonly greeting = computed(() => {
    const hour = new Date().getHours();
    return hour < 12 ? 'Buenos días' : hour < 20 ? 'Buenas tardes' : 'Buenas noches';
  });

  /** "Jueves, 12 de noviembre", from the server's today. */
  protected readonly todayLabel = computed(() => {
    const today = this.dashboard()?.today;
    if (!today) {
      return '';
    }
    const [y, m, d] = today.split('-').map(Number);
    const text = WEEKDAY_DATE.format(new Date(y, m - 1, d));
    return text.charAt(0).toUpperCase() + text.slice(1);
  });

  protected readonly closedToday = computed(() => {
    const d = this.dashboard();
    return !!d?.lastClosing && d.lastClosing.date === d.today;
  });

  /** "Hoy", "Ayer" or the date. */
  protected readonly lastClosingWhen = computed(() => {
    const d = this.dashboard();
    if (!d?.lastClosing) {
      return '';
    }
    if (d.lastClosing.date === d.today) {
      return 'Hoy';
    }
    return d.lastClosing.date === addDays(d.today, -1) ? 'Ayer' : formatShortDate(d.lastClosing.date);
  });

  protected readonly collectedGrowth = computed(() => {
    const d = this.dashboard();
    return d ? growth(d.month.collectedCents, d.previousMonth.collectedCents) : null;
  });

  protected readonly realIncomeGrowth = computed(() => {
    const d = this.dashboard();
    return d ? growth(d.month.realIncomeCents, d.previousMonth.realIncomeCents) : null;
  });

  protected readonly previousSpan = computed(() => {
    const d = this.dashboard();
    return d ? spanLabel(d.previousMonth.from, d.previousMonth.to) : '';
  });

  protected readonly columns = computed(() => dayColumns(this.dashboard()?.days ?? []));

  /** Active envelopes (or ones still holding money) with their total per currency; the negative ones flagged. */
  protected readonly envelopes = computed(() => {
    const overview = this.overview();
    if (!overview) {
      return [];
    }
    return overview.envelopes
      .filter((e) => e.envelope.isActive || e.balances.some((b) => b.amountCents !== 0))
      .map((e) => ({
        id: e.envelope.id,
        name: e.envelope.name,
        role: e.envelope.role,
        totals: totalsByCurrency(e.balances, overview.cashBoxes),
        negative: e.balances.some((b) => b.amountCents < 0),
      }));
  });

  protected readonly negativeEnvelopes = computed(() => this.envelopes().filter((e) => e.negative));

  ngOnInit(): void {
    this.dashboardApi.get().subscribe({
      next: (dashboard) => {
        this.dashboard.set(dashboard);
        this.loadError.set(null);
      },
      error: (err: unknown) => this.loadError.set(toApiError(err)),
    });
    this.envelopesApi.balances().subscribe({
      next: (overview) => this.overview.set(overview),
      error: () => this.overview.set(null),
    });
  }
}
