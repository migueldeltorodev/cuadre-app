import { CashDiscrepancyResponse, CashDiscrepancyState, DailyClosingState } from '../../core/api/models';
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
  return `${what} de ${formatMoney(discrepancy.amountCents)} · ${discrepancyStateLabel(discrepancy.status).label.toLowerCase()}`;
}

export function discrepancyStateLabel(state: CashDiscrepancyState): { label: string; tone: 'ok' | 'warn' | 'bad' | 'muted' } {
  switch (state) {
    case 'Pending':
      return { label: 'Pendiente de reclamar', tone: 'warn' };
    case 'Recovered':
      return { label: 'Recuperado', tone: 'ok' };
    case 'Lost':
      return { label: 'Perdido', tone: 'bad' };
    case 'Recorded':
      return { label: 'Registrado', tone: 'muted' };
    case 'Voided':
      return { label: 'Anulado', tone: 'muted' };
  }
}
