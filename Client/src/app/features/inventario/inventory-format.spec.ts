import { StockMovementResponse } from '../../core/api/models';
import { daysBetween, formatSignedQuantity, inPurchaseUnits, movementLabel } from './inventory-format';

function movement(partial: Partial<StockMovementResponse>): StockMovementResponse {
  return {
    id: '1',
    productId: 'p',
    productName: 'Arroz',
    stockUnit: 'Pound',
    locationId: 'l',
    locationName: 'Almacén',
    date: '2026-11-12',
    kind: 'Purchase',
    quantityMilli: 1000,
    unitCostUsdMicros: null,
    unitCostCupMicros: null,
    purchaseId: null,
    purchaseNumber: null,
    operationId: null,
    operationNumber: null,
    exitReason: null,
    closingId: null,
    closingDate: null,
    isVoided: false,
    createdAt: '2026-11-12T15:00:00Z',
    ...partial,
  };
}

describe('inventory-format', () => {
  it('reads a stock quantity in sacks only when exact', () => {
    expect(inPurchaseUnits(200000, 'Sack', 'Pound', 100000)).toBe('2 sacos');
    expect(inPurchaseUnits(100000, 'Sack', 'Pound', 100000)).toBe('1 saco');
    expect(inPurchaseUnits(150000, 'Sack', 'Pound', 100000)).toBeNull(); // 1.5 sacos: shown in lb only
    expect(inPurchaseUnits(4000, 'Each', 'Each', 1000)).toBeNull();
    expect(inPurchaseUnits(0, 'Sack', 'Pound', 100000)).toBeNull();
  });

  it('signs quantities with a real minus', () => {
    expect(formatSignedQuantity(10000, 'Pound')).toBe('+10 lb');
    expect(formatSignedQuantity(-5500, 'Pound')).toBe('−5.5 lb');
  });

  it('names each ledger row by its document', () => {
    expect(movementLabel(movement({ kind: 'Purchase', purchaseNumber: 12 }))).toBe('Compra #12');
    expect(movementLabel(movement({ kind: 'Exit', operationNumber: 4, exitReason: 'Shrinkage' }))).toBe(
      'Salida #4 · Merma',
    );
    expect(movementLabel(movement({ kind: 'TransferIn', operationNumber: 3 }))).toBe('Traslado #3 (entra)');
    expect(movementLabel(movement({ kind: 'Sale', closingId: 'c1', closingDate: '2026-11-12' }))).toBe(
      'Venta del día · 12 nov 2026',
    );
  });

  it('counts whole days between business dates', () => {
    expect(daysBetween('2026-10-25', '2026-11-12')).toBe(18);
    expect(daysBetween('2026-11-12', '2026-11-12')).toBe(0);
  });
});
