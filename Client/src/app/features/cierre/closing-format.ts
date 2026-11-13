import { CashDiscrepancyResponse, DailyClosingState } from '../../core/api/models';
import { formatMoney } from '../../shared/format/money';

/** Labels and display-only helpers of the daily closing. The server computes every real number. */

export function closingStateLabel(state: DailyClosingState): { label: string; tone: 'ok' | 'muted' } {
  return state === 'Confirmed' ? { label: 'Cerrado', tone: 'ok' } : { label: 'Reabierto', tone: 'muted' };
}

/** "+150 CUP" / "−80 CUP" / "0 CUP" (CUP only: the closing only collects in pesos). */
export function formatSignedCup(cents: number): string {
  const sign = cents > 0 ? '+' : cents < 0 ? '−' : '';
  return `${sign}${formatMoney(Math.abs(cents))}`;
}

/** "−1,500 CUP" with the real minus sign, no "+". */
export function formatCup(cents: number): string {
  return cents < 0 ? `−${formatMoney(-cents)}` : formatMoney(cents);
}

/** "Faltan 150 CUP" / "Sobran 80 CUP" / "La caja cuadra". */
export function differenceText(adjustedCents: number): string {
  if (adjustedCents < 0) {
    return `Faltan ${formatMoney(-adjustedCents)}`;
  }
  if (adjustedCents > 0) {
    return `Sobran ${formatMoney(adjustedCents)}`;
  }
  return 'La caja cuadra';
}

export function discrepancyText(discrepancy: CashDiscrepancyResponse): string {
  const what = discrepancy.kind === 'Shortage' ? 'Faltante' : 'Sobrante';
  const status =
    discrepancy.status === 'Pending' ? 'pendiente de reclamar' : discrepancy.status === 'Recorded' ? 'registrado' : 'anulado';
  return `${what} de ${formatMoney(discrepancy.amountCents)} · ${status}`;
}
