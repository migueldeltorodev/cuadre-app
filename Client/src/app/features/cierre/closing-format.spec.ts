import { closingStateLabel, differenceText, discrepancyText, formatCup, formatSignedCup } from './closing-format';

describe('closing-format', () => {
  it('says whether the cash matches, is short or has extra', () => {
    expect(differenceText(-15_000)).toBe('Faltan 150 CUP');
    expect(differenceText(8_050)).toBe('Sobran 80.50 CUP');
    expect(differenceText(0)).toBe('La caja cuadra');
  });

  it('formats signed and plain CUP with the real minus sign', () => {
    expect(formatSignedCup(15_000)).toBe('+150 CUP');
    expect(formatSignedCup(-15_000)).toBe('−150 CUP');
    expect(formatSignedCup(0)).toBe('0 CUP');
    expect(formatCup(-100_000)).toBe('−1,000 CUP');
    expect(formatCup(100_000)).toBe('1,000 CUP');
  });

  it('labels the state of a closing', () => {
    expect(closingStateLabel('Confirmed').label).toBe('Cerrado');
    expect(closingStateLabel('Voided').label).toBe('Reabierto');
  });

  it('describes a shortage to claim', () => {
    expect(
      discrepancyText({ id: 'd', kind: 'Shortage', amountCents: 20_000, note: null, status: 'Pending' }),
    ).toBe('Faltante de 200 CUP · pendiente de reclamar');
  });
});
