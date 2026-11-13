/**
 * Money and percentages travel as INTEGERS (cents, basis points). This file is the only bridge
 * between those integers and what a person types or reads.
 *
 * Parsing NEVER uses floating point: "0.29" is split into "0" and "29" and assembled as 0 * 100 + 29.
 * (parseFloat("0.29") * 100 === 28.999999999999996 — the phantom cent we refuse to have.)
 *
 * Accepted input: digits, optional thousands commas, optional "." with up to 2 decimals.
 *   "410.50" → 41050    "1,000" → 100000    "30" → 3000    "0.5" → 50
 */

const NUMBER_TEXT = /^\d{1,10}(\.\d{1,2})?$/;

/** Text → hundredths (cents, or basis points for a percentage). Null if it is not a valid amount. */
export function parseHundredths(text: string | null | undefined): number | null {
  const clean = (text ?? '').trim().replaceAll(',', '').replaceAll(' ', '');
  if (!NUMBER_TEXT.test(clean)) {
    return null;
  }

  const [units, fraction = ''] = clean.split('.');
  return Number(units) * 100 + Number(fraction.padEnd(2, '0'));
}

/** Hundredths → text for an input (no thousands separator): 41050 → "410.50", 3000 → "30". */
export function toInputText(hundredths: number): string {
  const units = Math.trunc(hundredths / 100);
  const fraction = Math.abs(hundredths % 100);
  return fraction === 0 ? String(units) : `${units}.${String(fraction).padStart(2, '0')}`;
}

const WHOLE = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const TWO_DECIMALS = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

/**
 * Hundredths → text to READ: 100000 → "1,000", 41050 → "410.50".
 * Decimals only when there are cents. Display-only division: never fed back into a calculation.
 */
export function formatHundredths(hundredths: number): string {
  return hundredths % 100 === 0 ? WHOLE.format(hundredths / 100) : TWO_DECIMALS.format(hundredths / 100);
}

/** "1,000 CUP" */
export function formatMoney(cents: number, currency = 'CUP'): string {
  return `${formatHundredths(cents)} ${currency}`;
}

/** Basis points → "30%", "16.67%". */
export function formatPercent(bps: number): string {
  return `${formatHundredths(bps)}%`;
}

const UNIT_COST = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 4 });

/**
 * Unit costs travel in MILLIONTHS (a pound of rice can cost 0.638235 USD).
 * To READ: 620000 → "0.62", 9166667 → "9.1667", 201500000 → "201.50". Display only.
 */
export function formatMicros(micros: number): string {
  return UNIT_COST.format(micros / 1_000_000);
}

/**
 * Markup ("recargo", over cost) → margin (over price), in basis points.
 * 20% markup = 16.67% margin: price 120, profit 20, 20 / 120.
 */
export function markupToMarginBps(markupBps: number): number {
  return Math.round((markupBps * 10_000) / (10_000 + markupBps));
}
