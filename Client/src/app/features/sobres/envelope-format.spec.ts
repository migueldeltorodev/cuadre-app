import { CashMovementResponse, EnvelopeCashBoxResponse } from '../../core/api/models';
import {
  addDays,
  cashMovementLabel,
  cashMovementLink,
  firstOfMonth,
  formatBalance,
  formatSignedMoney,
  planStartLabel,
  totalsByCurrency,
} from './envelope-format';

const box = (id: string, currency: 'CUP' | 'USD'): EnvelopeCashBoxResponse => ({
  id,
  name: id,
  currency,
  kind: 'Cash',
  isActive: true,
  totalCents: 0,
});

const movement = (overrides: Partial<CashMovementResponse>): CashMovementResponse => ({
  id: 'm1',
  cashBoxId: 'b1',
  cashBoxName: 'Efectivo CUP',
  currency: 'CUP',
  envelopeId: 'e1',
  envelopeName: 'Reposición',
  date: '2026-11-12',
  kind: 'Purchase',
  amountCents: -6200,
  description: 'Compra #3',
  purchaseId: null,
  purchaseNumber: null,
  envelopeOperationId: null,
  envelopeOperationNumber: null,
  stockOperationId: null,
  stockOperationNumber: null,
  closingId: null,
  closingDate: null,
  isVoided: false,
  createdAt: '2026-11-12T15:00:00Z',
  ...overrides,
});

describe('envelope-format', () => {
  it('signs money with the real minus sign', () => {
    expect(formatSignedMoney(100000, 'CUP')).toBe('+1,000 CUP');
    expect(formatSignedMoney(-6200, 'USD')).toBe('−62 USD');
    expect(formatBalance(-150000, 'CUP')).toBe('−1,500 CUP');
    expect(formatBalance(150000, 'CUP')).toBe('1,500 CUP');
  });

  it('never adds pesos and dollars together', () => {
    const boxes = [box('cup', 'CUP'), box('bank', 'CUP'), box('usd', 'USD')];
    const totals = totalsByCurrency(
      [
        { cashBoxId: 'usd', amountCents: 62000 },
        { cashBoxId: 'cup', amountCents: 9640000 },
        { cashBoxId: 'bank', amountCents: 24340000 },
      ],
      boxes,
    );
    expect(totals).toEqual([
      { currency: 'CUP', cents: 33980000 },
      { currency: 'USD', cents: 62000 },
    ]);
  });

  it('labels and links the rows by their document', () => {
    const charge = movement({ kind: 'PurchaseCharge', description: 'Flete', purchaseNumber: 3, purchaseId: 'p3' });
    expect(cashMovementLabel(charge)).toBe('Flete · compra #3');
    expect(cashMovementLink(charge)).toEqual(['/compras', 'p3']);
    expect(cashMovementLink(movement({ stockOperationId: 's1' }))).toEqual(['/inventario/operaciones', 's1']);
    expect(cashMovementLink(movement({ closingId: 'c1', closingDate: '2026-11-12' }))).toEqual(['/cierre', 'c1']);
    expect(cashMovementLink(movement({ envelopeOperationId: 'o1' }))).toEqual(['/sobres/operaciones', 'o1']);
  });

  it('reads the seeded plan as "since always"', () => {
    expect(planStartLabel('2000-01-01')).toBe('desde el principio');
    expect(planStartLabel('2026-11-13')).toBe('desde el 13 nov 2026');
  });

  it('moves business dates without time zones', () => {
    expect(firstOfMonth('2026-11-12')).toBe('2026-11-01');
    expect(addDays('2026-11-01', -1)).toBe('2026-10-31');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
  });
});
