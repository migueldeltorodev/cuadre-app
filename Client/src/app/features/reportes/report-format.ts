import { CategoryResponse, ExpenseTotalResponse } from '../../core/api/models';
import { DailyFiguresResponse, LossType, MonthTotalsResponse } from '../../core/api/report-models';
import { formatDayMonth, formatShortDate } from '../../shared/format/date';
import { formatMoney, formatPercent } from '../../shared/format/money';
import { StackedColumn } from '../../shared/ui/charts/stacked-columns';

/** Display helpers for Inicio and Reportes (slice 8). Only reading: every sum comes from the server. */

const MONTHS = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
];

/** (2026, 11) → "noviembre 2026". */
export function monthName(year: number, month: number): string {
  return `${MONTHS[month - 1]} ${year}`;
}

/** (11) → "nov". */
export function shortMonth(month: number): string {
  return MONTHS[month - 1].slice(0, 3);
}

/** The cash difference of a closing, in words. */
export function differenceLabel(cents: number): { text: string; tone: 'ok' | 'bad' | 'warn' } {
  if (cents === 0) {
    return { text: 'Cuadró', tone: 'ok' };
  }
  return cents < 0
    ? { text: `Faltaron ${formatMoney(-cents)}`, tone: 'bad' }
    : { text: `Sobraron ${formatMoney(cents)}`, tone: 'warn' };
}

/** Each closing as a column: A1 split into A2 and A3. */
export function dayColumns(days: DailyFiguresResponse[]): StackedColumn[] {
  return days.map((day) => {
    const difference = differenceLabel(day.differenceCents);
    return {
      key: day.closingId,
      label: day.date.slice(8, 10).replace(/^0/, ''),
      title: formatShortDate(day.date),
      totalCents: day.collectedCents,
      costCents: day.costOfSalesCents,
      incomeCents: day.realIncomeCents,
      note: day.differenceCents === 0 ? undefined : difference.text,
      noteTone: day.differenceCents < 0 ? 'bad' : 'muted',
    };
  });
}

/** Each month of the trend as a column. */
export function monthColumns(months: MonthTotalsResponse[]): StackedColumn[] {
  return months.map((month) => ({
    key: `${month.year}-${month.month}`,
    label: shortMonth(month.month),
    title: monthName(month.year, month.month),
    totalCents: month.collectedCents,
    costCents: month.costOfSalesCents,
    incomeCents: month.realIncomeCents,
    note: month.closedDays === 0 ? 'Sin días cerrados' : `${month.closedDays} días cerrados`,
    noteTone: 'muted',
  }));
}

/** Change against a previous figure: "+12%" / "−3%". Null when there is nothing to compare with. */
export function growth(current: number, previous: number): { text: string; tone: 'up' | 'down' | 'flat' } | null {
  if (previous <= 0) {
    return null;
  }
  const bps = Math.round(((current - previous) * 10_000) / previous);
  if (Math.abs(bps) < 50) {
    return { text: 'igual', tone: 'flat' };
  }
  const sign = bps > 0 ? '+' : '−';
  return { text: `${sign}${formatPercent(Math.round(Math.abs(bps) / 100) * 100)}`, tone: bps > 0 ? 'up' : 'down' };
}

/** "del 1 al 12 de octubre". */
export function spanLabel(from: string, to: string): string {
  return `del ${Number(from.slice(8, 10))} al ${formatDayMonth(to)}`;
}

/** Expenses per currency, never added up: "3,000 CUP · 20 USD". Zero when there are none. */
export function expensesLabel(expenses: ExpenseTotalResponse[]): string {
  return expenses.length === 0
    ? formatMoney(0)
    : expenses.map((total) => formatMoney(total.amountCents, total.currency)).join(' · ');
}

export const LOSS_LABELS: Record<LossType, string> = {
  Shrinkage: 'Merma',
  Expired: 'Vencido',
  Damaged: 'Roto o dañado',
  CountDifference: 'Faltó en un conteo',
};

/** The month before / after, crossing the year. */
export function previousMonth(year: number, month: number): { year: number; month: number } {
  return month === 1 ? { year: year - 1, month: 12 } : { year, month: month - 1 };
}

export function nextMonth(year: number, month: number): { year: number; month: number } {
  return month === 12 ? { year: year + 1, month: 1 } : { year, month: month + 1 };
}

/** Top-level categories with their subcategories right under them. */
export function categoryOptions(categories: CategoryResponse[]): { id: string; label: string }[] {
  const byName = (a: CategoryResponse, b: CategoryResponse) => a.name.localeCompare(b.name, 'es');
  return categories
    .filter((c) => c.parentId === null)
    .sort(byName)
    .flatMap((top) => [
      { id: top.id, label: top.name },
      ...categories
        .filter((c) => c.parentId === top.id)
        .sort(byName)
        .map((sub) => ({ id: sub.id, label: `${top.name} › ${sub.name}` })),
    ]);
}
