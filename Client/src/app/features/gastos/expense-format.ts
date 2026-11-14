import { DueExpenseResponse, ExpenseState, RecurringExpenseResponse } from '../../core/api/models';
import { formatDayMonth } from '../../shared/format/date';

/** Display helpers for Gastos (slice 7). The server owns every rule: these only word things for people. */

/** Weekday names by .NET DayOfWeek (0 = Sunday), as the API sends DueDay for weekly expenses. */
export const WEEKDAYS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'] as const;

/** Monday first, the way a week reads here. */
export const WEEKDAY_OPTIONS: { value: number; label: string }[] = [1, 2, 3, 4, 5, 6, 0].map((value) => ({
  value,
  label: WEEKDAYS[value][0].toUpperCase() + WEEKDAYS[value].slice(1),
}));

export const MAX_DAY_OF_MONTH = 28;

export const DAY_OF_MONTH_OPTIONS = Array.from({ length: MAX_DAY_OF_MONTH }, (_, i) => i + 1);

const MONTH_YEAR = new Intl.DateTimeFormat('es', { month: 'long', year: 'numeric' });

export function expenseStateLabel(state: ExpenseState): { label: string; tone: 'ok' | 'muted' } {
  return state === 'Confirmed' ? { label: 'Pagado', tone: 'ok' } : { label: 'Anulado', tone: 'muted' };
}

/** "Todos los meses, el día 5" / "Todas las semanas, los lunes". */
export function scheduleLabel(recurring: Pick<RecurringExpenseResponse, 'frequency' | 'dueDay'>): string {
  return recurring.frequency === 'Monthly'
    ? `Todos los meses, el día ${recurring.dueDay}`
    : `Todas las semanas, los ${WEEKDAYS[recurring.dueDay] ?? ''}`;
}

/** The occurrence for people: "noviembre de 2026" or "semana del 9 de noviembre". */
export function periodLabel(frequency: RecurringExpenseResponse['frequency'], periodStart: string): string {
  if (frequency === 'Weekly') {
    return `semana del ${formatDayMonth(periodStart)}`;
  }
  const [year, month] = periodStart.split('-').map(Number);
  return MONTH_YEAR.format(new Date(year, month - 1, 1));
}

export function duePeriodLabel(due: DueExpenseResponse): string {
  return periodLabel(due.recurring.frequency, due.periodStart);
}
