import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiError, toApiError } from '../../../core/api/api-error';
import { AccountantSummaryResponse } from '../../../core/api/report-models';
import { ReportsApi } from '../../../core/api/reports-api';
import { formatShortDate } from '../../../shared/format/date';
import { formatMoney, formatPercent } from '../../../shared/format/money';
import { monthName, nextMonth, previousMonth } from '../report-format';

/** One line of the estado de rendimiento. */
interface StatementLine {
  label: string;
  note?: string;
  cents: number;
  /** '−' subtracts, '+' adds, '=' is a result. */
  sign: '+' | '−' | '=';
  strong?: boolean;
}

/**
 * D-63: the month as the accountant needs it. The estado de rendimiento (income − real cost − expenses − staff −
 * losses = profit before taxes; − suggested taxes = after) and the totals beside it: income by payment method,
 * expenses by category (USD apart and converted), purchases and the goods at cost at the start and at the end.
 * The server computes everything; this page only lays it out.
 */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './accountant-page.html',
  styleUrls: ['../../../shared/ui/list-page.css', '../report-page.css', './accountant-page.css'],
})
export default class AccountantPage implements OnInit {
  private readonly api = inject(ReportsApi);

  protected readonly formatMoney = formatMoney;
  protected readonly formatPercent = formatPercent;
  protected readonly formatShortDate = formatShortDate;
  protected readonly monthName = monthName;

  protected readonly summary = signal<AccountantSummaryResponse | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly loading = signal(false);

  /** The current month (learned from the first, default answer): no going beyond it. */
  private readonly current = signal<{ year: number; month: number } | null>(null);

  protected readonly isCurrent = computed(() => {
    const s = this.summary();
    const c = this.current();
    return !!s && !!c && s.year === c.year && s.month === c.month;
  });

  protected readonly statement = computed<StatementLine[]>(() => {
    const s = this.summary();
    if (!s) {
      return [];
    }
    const lines: StatementLine[] = [
      { label: 'Ingresos', note: 'Lo cobrado en los cierres', cents: s.incomeCents, sign: '+' },
      {
        label: 'Costo de lo vendido',
        note: 'Lo que costó de verdad, tanda por tanda, sin el colchón',
        cents: s.costOfSalesCents,
        sign: '−',
      },
      { label: 'Ganancia bruta', cents: s.grossProfitCents, sign: '=', strong: true },
      { label: 'Gastos', note: 'Lo pagado en USD, pasado a CUP con la tasa de su día', cents: s.expensesCents, sign: '−' },
      { label: 'Pago del personal', cents: s.staffPayCents, sign: '−' },
      { label: 'Mercadería perdida', note: 'Merma, vencida, rota y faltantes de conteo', cents: s.merchandiseLossCents, sign: '−' },
    ];
    if (s.countGainCents > 0) {
      lines.push({ label: 'Sobrantes de conteo', cents: s.countGainCents, sign: '+' });
    }
    lines.push({ label: 'Ganancia antes de impuestos', cents: s.profitBeforeTaxesCents, sign: '=', strong: true });
    if (s.taxes.length > 0) {
      lines.push({ label: 'Impuestos sugeridos', note: 'Lo que convendría apartar', cents: s.taxesCents, sign: '−' });
      lines.push({ label: 'Ganancia después de impuestos', cents: s.profitAfterTaxesCents, sign: '=', strong: true });
    }
    return lines;
  });

  protected readonly hasUsdExpenses = computed(() => (this.summary()?.expenses ?? []).some((e) => e.usdCents !== 0));

  ngOnInit(): void {
    this.load();
  }

  protected go(step: -1 | 1): void {
    const s = this.summary();
    if (!s) {
      return;
    }
    const target = step < 0 ? previousMonth(s.year, s.month) : nextMonth(s.year, s.month);
    this.load(target.year, target.month);
  }

  /** "−1,250 CUP" for the lines that subtract; results keep their own sign. */
  protected lineAmount(line: StatementLine): string {
    return line.sign === '−' && line.cents !== 0 ? `−${formatMoney(line.cents)}` : formatMoney(line.cents);
  }

  protected load(year?: number, month?: number): void {
    this.loading.set(true);
    this.api.accountant(year, month).subscribe({
      next: (summary) => {
        if (!this.current() && year === undefined) {
          this.current.set({ year: summary.year, month: summary.month });
        }
        this.summary.set(summary);
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
