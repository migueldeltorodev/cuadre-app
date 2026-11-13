import {
  CashMovementResponse,
  CurrencyCode,
  EnvelopeBoxBalanceResponse,
  EnvelopeCashBoxResponse,
  EnvelopeOperationState,
  EnvelopeOperationType,
  EnvelopeType,
} from '../../core/api/models';
import { formatShortDate } from '../../shared/format/date';
import { formatMoney } from '../../shared/format/money';

/** Display helpers for Sobres (slice 5). No arithmetic the server should own: only sums for reading. */

/** The swatch of each envelope (design: Sobres). The user's own envelopes share one. */
export const ENVELOPE_SWATCHES: Record<EnvelopeType, string> = {
  Profit: '#4F6B7A',
  OwnerPay: '#B85A36',
  Tax: '#7D6F62',
  Operations: '#C9A57A',
  Replenishment: '#D6CFC1',
  Custom: '#8E9B7A',
};

/** What each envelope is for, in one line (Configuración › Profit First). */
export const ENVELOPE_NOTES: Record<EnvelopeType, string> = {
  Profit: 'Se aparta y no se toca. Es la ganancia del negocio.',
  OwnerPay: 'Tu sueldo. De acá salen tus retiros y el consumo de la casa.',
  Tax: 'Para pagar los impuestos cuando toque.',
  Operations: 'Para los gastos del negocio y el pago del dependiente.',
  Replenishment: 'Recibe el costo de lo vendido (A2) para volver a comprar. No lleva porcentaje.',
  Custom: 'Un sobre propio.',
};

const OPERATION_KIND_LABELS: Record<EnvelopeOperationType, string> = {
  Opening: 'Saldo inicial',
  Transfer: 'Movimiento entre sobres',
  Withdrawal: 'Retiro',
  TaxPayment: 'Pago de impuestos',
};

export function envelopeOperationKindLabel(kind: EnvelopeOperationType): string {
  return OPERATION_KIND_LABELS[kind];
}

export function envelopeOperationStateLabel(state: EnvelopeOperationState): { label: string; tone: 'ok' | 'muted' } {
  return state === 'Confirmed' ? { label: 'Confirmada', tone: 'ok' } : { label: 'Anulada', tone: 'muted' };
}

/** "+1,000 CUP" / "−62 USD": signed, with the real minus sign. */
export function formatSignedMoney(cents: number, currency: CurrencyCode): string {
  const sign = cents > 0 ? '+' : cents < 0 ? '−' : '';
  return `${sign}${formatMoney(Math.abs(cents), currency)}`;
}

/** "−1,500 CUP" with the real minus sign, no "+" (balances). */
export function formatBalance(cents: number, currency: CurrencyCode): string {
  return cents < 0 ? `−${formatMoney(-cents, currency)}` : formatMoney(cents, currency);
}

/** What a ledger row means, for people. The server already writes a good description for most of them. */
export function cashMovementLabel(movement: CashMovementResponse): string {
  switch (movement.kind) {
    case 'PurchaseCharge':
      return `${movement.description} · compra #${movement.purchaseNumber}`;
    case 'Opening':
      return `Saldo inicial #${movement.envelopeOperationNumber}`;
    default:
      return movement.description;
  }
}

/** Where the row's document lives, to link it. */
export function cashMovementLink(movement: CashMovementResponse): string[] | null {
  if (movement.purchaseId) {
    return ['/compras', movement.purchaseId];
  }
  if (movement.envelopeOperationId) {
    return ['/sobres/operaciones', movement.envelopeOperationId];
  }
  if (movement.stockOperationId) {
    return ['/inventario/operaciones', movement.stockOperationId];
  }
  if (movement.closingId) {
    return ['/cierre', movement.closingId];
  }
  if (movement.expenseId) {
    return ['/gastos', movement.expenseId];
  }
  if (movement.recoveryClosingId) {
    // A recovered shortage: its closing shows the shortage and links to it.
    return ['/cierre', movement.recoveryClosingId];
  }
  return null;
}

/** "desde el principio" for the seeded plan (2000-01-01), else "desde el 1 nov 2026". */
export function planStartLabel(effectiveFrom: string): string {
  return effectiveFrom <= '2000-12-31' ? 'desde el principio' : `desde el ${formatShortDate(effectiveFrom)}`;
}

export interface CurrencyAmount {
  currency: CurrencyCode;
  cents: number;
}

/**
 * Balances added up PER CURRENCY (CUP first): pesos and dollars are never summed together.
 * Only currencies with something in them.
 */
export function totalsByCurrency(
  balances: EnvelopeBoxBalanceResponse[],
  boxes: EnvelopeCashBoxResponse[],
): CurrencyAmount[] {
  const currencyOf = new Map(boxes.map((box) => [box.id, box.currency] as const));
  const totals = new Map<CurrencyCode, number>();
  for (const balance of balances) {
    const currency = currencyOf.get(balance.cashBoxId);
    if (currency) {
      totals.set(currency, (totals.get(currency) ?? 0) + balance.amountCents);
    }
  }
  return (['CUP', 'USD'] as CurrencyCode[])
    .filter((currency) => (totals.get(currency) ?? 0) !== 0)
    .map((currency) => ({ currency, cents: totals.get(currency)! }));
}

/** 'yyyy-MM-dd' of the first day of that date's month. */
export function firstOfMonth(date: string): string {
  return `${date.slice(0, 7)}-01`;
}

/** 'yyyy-MM-dd' moved by whole days, without time zones. */
export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number);
  const moved = new Date(Date.UTC(y, m - 1, d + days));
  return moved.toISOString().slice(0, 10);
}
