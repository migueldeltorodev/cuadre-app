import { formatHundredths, formatPercent, markupToMarginBps, parseHundredths, toInputText } from './money';

describe('money', () => {
  describe('parseHundredths', () => {
    it.each([
      ['410.50', 41050],
      ['410.5', 41050],
      ['1,000', 100000],
      ['1,000.25', 100025],
      ['0.29', 29], // parseFloat('0.29') * 100 would give 28.999999999999996
      ['  30 ', 3000],
      ['0', 0],
    ])('parses %s as %i', (text, expected) => {
      expect(parseHundredths(text)).toBe(expected);
    });

    it.each(['', '   ', 'abc', '-5', '1.234', '1..2', '12a', '.5', null, undefined])(
      'rejects %s',
      (text) => {
        expect(parseHundredths(text)).toBeNull();
      },
    );
  });

  describe('toInputText', () => {
    it('drops decimals when there are no cents', () => {
      expect(toInputText(3000)).toBe('30');
    });

    it('keeps two decimals when there are cents', () => {
      expect(toInputText(41050)).toBe('410.50');
      expect(toInputText(5)).toBe('0.05');
    });

    it('round-trips with parseHundredths', () => {
      for (const value of [0, 1, 99, 100, 41050, 123456789]) {
        expect(parseHundredths(toInputText(value))).toBe(value);
      }
    });
  });

  describe('formatting', () => {
    it('adds thousands separators to read', () => {
      expect(formatHundredths(100000)).toBe('1,000');
      expect(formatHundredths(41050)).toBe('410.50');
    });

    it('formats percentages from basis points', () => {
      expect(formatPercent(3000)).toBe('30%');
      expect(formatPercent(1667)).toBe('16.67%');
    });
  });

  describe('markupToMarginBps', () => {
    it('turns a 20% markup into a 16.67% margin', () => {
      expect(markupToMarginBps(2000)).toBe(1667);
    });

    it('is zero for zero markup', () => {
      expect(markupToMarginBps(0)).toBe(0);
    });
  });
});
