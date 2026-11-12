import { formatQuantityWithUnit, parseThousandths, toQuantityInput } from './quantity';

describe('quantity format', () => {
  it('parses text into thousandths without floating point', () => {
    expect(parseThousandths('2.5')).toBe(2500);
    expect(parseThousandths('100')).toBe(100_000);
    expect(parseThousandths('0.125')).toBe(125);
    expect(parseThousandths('1,000')).toBe(1_000_000);
    expect(parseThousandths(' 3 ')).toBe(3000);
  });

  it('rejects what is not a quantity', () => {
    expect(parseThousandths('')).toBeNull();
    expect(parseThousandths('-1')).toBeNull();
    expect(parseThousandths('1.2345')).toBeNull();
    expect(parseThousandths('abc')).toBeNull();
  });

  it('round-trips through the input text', () => {
    for (const milli of [0, 1, 125, 2500, 100_000, 1_234_567]) {
      expect(parseThousandths(toQuantityInput(milli))).toBe(milli);
    }
    expect(toQuantityInput(2500)).toBe('2.5');
  });

  it('writes the unit after the number', () => {
    expect(formatQuantityWithUnit(2500, 'Pound')).toBe('2.5 lb');
    expect(formatQuantityWithUnit(1000, 'Each')).toBe('1 unidad');
    expect(formatQuantityWithUnit(4000, 'Sack')).toBe('4 sacos');
  });
});
