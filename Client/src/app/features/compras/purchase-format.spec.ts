import { convertCents, lineSubtotalCents, stockQuantityMilli } from './purchase-format';

describe('purchase-format', () => {
  it('multiplies quantity by unit cost like the server', () => {
    expect(lineSubtotalCents(2000, 6200)).toBe(12400); // 2 sacos × 62.00
    expect(lineSubtotalCents(2500, 333)).toBe(833); // 2.5 × 3.33 = 8.325 → 8.33
  });

  it('turns purchase units into stock units only when exact', () => {
    expect(stockQuantityMilli(2500, 100000)).toBe(250000); // 2.5 sacos × 100 lb
    expect(stockQuantityMilli(1, 1500)).toBeNull();
  });

  it('converts between currencies at the rate, for reading', () => {
    expect(convertCents(150000, 'CUP', 'USD', 33800)).toBe(444); // 1,500 CUP ≈ 4.44 USD
    expect(convertCents(1000, 'USD', 'CUP', 32500)).toBe(325000);
    expect(convertCents(1000, 'USD', 'USD', 32500)).toBe(1000);
  });
});
