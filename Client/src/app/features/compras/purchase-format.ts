import { CurrencyCode, PurchaseState } from '../../core/api/models';
import { ONE_UNIT_MILLI } from '../../shared/format/quantity';

/**
 * Display-only arithmetic for the purchase form, so the person sees subtotals while typing.
 * The SERVER computes the real totals and landed costs (PurchaseCalculator); these numbers are never sent.
 */

/** Quantity (thousandths) × unit cost (cents), rounded like the server: 2 sacos × 62.00 = 124.00. */
export function lineSubtotalCents(quantityMilli: number, unitCostCents: number): number {
  return Math.max(1, Math.round((quantityMilli * unitCostCents) / ONE_UNIT_MILLI));
}

/** Purchase units × factor, in stock-unit thousandths. Null when it is not exact (the server rejects it). */
export function stockQuantityMilli(quantityMilli: number, factorMilli: number): number | null {
  const product = quantityMilli * factorMilli;
  return product % ONE_UNIT_MILLI === 0 ? product / ONE_UNIT_MILLI : null;
}

/** "≈" conversion of an amount to another currency at the purchase rate, for reading only. */
export function convertCents(cents: number, from: CurrencyCode, to: CurrencyCode, rateCents: number): number {
  if (from === to || rateCents <= 0) {
    return cents;
  }
  return from === 'CUP' ? Math.round((cents * 100) / rateCents) : Math.round((cents * rateCents) / 100);
}

export function purchaseStateLabel(state: PurchaseState): { label: string; tone: 'ok' | 'muted' } {
  return state === 'Confirmed' ? { label: 'Confirmada', tone: 'ok' } : { label: 'Anulada', tone: 'muted' };
}

/** Today as yyyy-MM-dd in the PC's local day (fallback when the server has not answered yet). */
export function localToday(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}
