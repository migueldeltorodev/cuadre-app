/**
 * Axis helpers shared by the hand-made charts. Display only: they never feed a calculation the server owns.
 * Series colors (validated with the dataviz palette checker, light mode): cost = blue, income/price = the accent.
 */
export const SERIES_COLORS = {
  cost: '#2F6DB5',
  income: '#B85A36',
} as const;

/** A "nice" top for an axis and its step: 23,400 → { max: 25,000, step: 5,000 }. Always > 0. */
export function niceScale(maxValue: number, ticks = 4): { max: number; step: number } {
  if (!(maxValue > 0)) {
    return { max: 1, step: 1 / ticks };
  }
  const rough = maxValue / ticks;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const residual = rough / magnitude;
  const nice = residual <= 1 ? 1 : residual <= 2 ? 2 : residual <= 2.5 ? 2.5 : residual <= 5 ? 5 : 10;
  const step = nice * magnitude;
  return { max: step * Math.ceil(maxValue / step), step };
}

const COMPACT = new Intl.NumberFormat('es', { notation: 'compact', maximumFractionDigits: 1 });

/** Whole units for an axis label: 25000 → "25 mil". */
export function compactNumber(value: number): string {
  return COMPACT.format(value);
}
