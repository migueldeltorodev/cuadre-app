/**
 * Dates for people, in Spanish, without registering Angular locale data (Intl ships it).
 * Business dates come as 'yyyy-MM-dd' and are read as LOCAL calendar days (no time zone shift).
 */

const DAY_MONTH = new Intl.DateTimeFormat('es', { day: 'numeric', month: 'long' });
const DAY_MONTH_YEAR = new Intl.DateTimeFormat('es', { day: 'numeric', month: 'short', year: 'numeric' });

function fromBusinessDate(date: string): Date {
  const [year, month, day] = date.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** '2026-11-02' → "2 de noviembre". */
export function formatDayMonth(date: string): string {
  return DAY_MONTH.format(fromBusinessDate(date));
}

/** '2026-11-02' → "2 nov 2026". */
export function formatShortDate(date: string): string {
  return DAY_MONTH_YEAR.format(fromBusinessDate(date)).replace('.', '');
}

/** ISO UTC timestamp → "2 de noviembre" in the PC's local day. */
export function formatTimestampDay(isoUtc: string): string {
  return DAY_MONTH.format(new Date(isoUtc));
}
