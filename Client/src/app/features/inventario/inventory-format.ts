import {
  ExitReasonCode,
  StockMovementResponse,
  StockOperationState,
  StockOperationType,
  UnitCode,
} from '../../core/api/models';
import { ONE_UNIT_MILLI, UNIT_LABELS, formatQuantity, formatQuantityWithUnit } from '../../shared/format/quantity';

/** Labels and display-only helpers of the Inventario screens. The server owns every real number. */

export const EXIT_REASONS: { code: ExitReasonCode; label: string; hint: string }[] = [
  { code: 'Shrinkage', label: 'Merma', hint: 'Se perdió en el pesaje o se echó a perder. Es pérdida.' },
  { code: 'Expired', label: 'Vencido', hint: 'Pasó la fecha. Es pérdida.' },
  { code: 'Damaged', label: 'Roto o dañado', hint: 'Se rompió el envase o se mojó. Es pérdida.' },
  {
    code: 'HouseConsumption',
    label: 'Consumo de la casa',
    hint: 'Lo llevaste para tu casa. Es un retiro del dueño a costo, no una pérdida.',
  },
  { code: 'Adjustment', label: 'Ajuste', hint: 'Corrige un error de carga. No es pérdida.' },
];

export function exitReasonLabel(reason: ExitReasonCode | null): string {
  return EXIT_REASONS.find((r) => r.code === reason)?.label ?? '';
}

export function operationKindLabel(kind: StockOperationType): string {
  switch (kind) {
    case 'Transfer':
      return 'Traslado';
    case 'Exit':
      return 'Salida';
    case 'Count':
      return 'Conteo';
  }
}

export function operationStateLabel(state: StockOperationState): { label: string; tone: 'ok' | 'muted' } {
  return state === 'Confirmed' ? { label: 'Confirmada', tone: 'ok' } : { label: 'Anulada', tone: 'muted' };
}

/** What a ledger row means, for people: "Compra #12", "Salida #4 · Merma", "Traslado #3 (sale)". */
export function movementLabel(movement: StockMovementResponse): string {
  const op = movement.operationNumber !== null ? ` #${movement.operationNumber}` : '';
  switch (movement.kind) {
    case 'Purchase':
      return `Compra #${movement.purchaseNumber}`;
    case 'PurchaseVoid':
      return `Anulación de la compra #${movement.purchaseNumber}`;
    case 'TransferOut':
      return `Traslado${op} (sale)`;
    case 'TransferIn':
      return `Traslado${op} (entra)`;
    case 'Exit':
      return `Salida${op} · ${exitReasonLabel(movement.exitReason)}`;
    case 'CountAdjustment':
      return `Ajuste de conteo${op}`;
    case 'OperationVoid':
      return `Anulación de la operación${op}`;
  }
}

/** Where the row's document lives, to link it. */
export function movementLink(movement: StockMovementResponse): string[] | null {
  if (movement.purchaseId) {
    return ['/compras', movement.purchaseId];
  }
  if (movement.operationId) {
    return ['/inventario/operaciones', movement.operationId];
  }
  return null;
}

/** "+10 lb" / "−5 lb": signed, with the real minus sign. */
export function formatSignedQuantity(milli: number, unit: UnitCode): string {
  const sign = milli > 0 ? '+' : milli < 0 ? '−' : '';
  return `${sign}${formatQuantityWithUnit(Math.abs(milli), unit)}`;
}

/**
 * A stock quantity also read in purchase units when it is an exact number of them:
 * 200 lb in 100 lb sacks → "2 sacos". Null when it is not exact, the units are the same, or it is not positive.
 */
export function inPurchaseUnits(milli: number, purchaseUnit: UnitCode, stockUnit: UnitCode, factorMilli: number): string | null {
  if (purchaseUnit === stockUnit || milli <= 0 || factorMilli <= 0) {
    return null;
  }
  const scaled = milli * ONE_UNIT_MILLI;
  if (scaled % factorMilli !== 0) {
    return null;
  }
  const units = scaled / factorMilli;
  if (units % ONE_UNIT_MILLI !== 0) {
    return null;
  }
  const label = UNIT_LABELS[purchaseUnit];
  return `${formatQuantity(units)} ${units === ONE_UNIT_MILLI ? label.one : label.many}`;
}

/** Whole days between two business dates ('yyyy-MM-dd'), no time zone involved. */
export function daysBetween(from: string, to: string): number {
  const toUtc = (date: string) => {
    const [y, m, d] = date.split('-').map(Number);
    return Date.UTC(y, m - 1, d);
  };
  return Math.round((toUtc(to) - toUtc(from)) / 86_400_000);
}

/** The storage should be counted at least this often (design: "Contar el almacén"). */
export const COUNT_REMINDER_DAYS = 14;
