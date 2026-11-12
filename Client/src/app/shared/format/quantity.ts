import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { UnitCode } from '../../core/api/models';

/**
 * Quantities travel as INTEGER thousandths of the unit ("Milli"): 2.5 lb = 2500, 4 units = 4000.
 * Same rule as money.ts: parsing never goes through floating point.
 *   "2.5" → 2500    "100" → 100000    "0.125" → 125    "1,000" → 1000000
 */

const QUANTITY_TEXT = /^\d{1,9}(\.\d{1,3})?$/;

export const ONE_UNIT_MILLI = 1000;

/** Text → thousandths. Null if it is not a valid quantity. */
export function parseThousandths(text: string | null | undefined): number | null {
  const clean = (text ?? '').trim().replaceAll(',', '').replaceAll(' ', '');
  if (!QUANTITY_TEXT.test(clean)) {
    return null;
  }

  const [units, fraction = ''] = clean.split('.');
  return Number(units) * ONE_UNIT_MILLI + Number(fraction.padEnd(3, '0'));
}

/** Thousandths → text for an input: 2500 → "2.5", 100000 → "100". */
export function toQuantityInput(milli: number): string {
  const units = Math.trunc(milli / ONE_UNIT_MILLI);
  const fraction = milli % ONE_UNIT_MILLI;
  return fraction === 0 ? String(units) : `${units}.${String(fraction).padStart(3, '0').replace(/0+$/, '')}`;
}

const QUANTITY = new Intl.NumberFormat('en-US', { maximumFractionDigits: 3 });

/** Thousandths → text to READ: 1500000 → "1,500", 2500 → "2.5". Display only. */
export function formatQuantity(milli: number): string {
  return QUANTITY.format(milli / ONE_UNIT_MILLI);
}

export function isWhole(milli: number): boolean {
  return milli % ONE_UNIT_MILLI === 0;
}

/** Validates a quantity text input. Errors: { quantity: true }, { min }. */
export function quantityValidator(options: { min?: number } = {}): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const value = parseThousandths(control.value);
    if (value === null) {
      return { quantity: true };
    }
    if (options.min !== undefined && value < options.min) {
      return { min: { min: options.min, actual: value } };
    }
    return null;
  };
}

/** Lets an optional field stay blank; otherwise applies the validator. */
export function optional(validator: ValidatorFn): ValidatorFn {
  return (control) => ((control.value ?? '').toString().trim() === '' ? null : validator(control));
}

// ---- Units (labels live in the UI; the list and its decimal rule come from GET /api/units) ----

interface UnitLabel {
  one: string;
  many: string;
  /** Written after a number instead of the word: "2.5 lb". */
  short?: string;
}

export const UNIT_LABELS: Record<UnitCode, UnitLabel> = {
  Each: { one: 'unidad', many: 'unidades' },
  Pound: { one: 'libra', many: 'libras', short: 'lb' },
  Kilogram: { one: 'kilogramo', many: 'kilogramos', short: 'kg' },
  Liter: { one: 'litro', many: 'litros', short: 'L' },
  Sack: { one: 'saco', many: 'sacos' },
  Box: { one: 'caja', many: 'cajas' },
  Pack: { one: 'paquete', many: 'paquetes' },
  Dozen: { one: 'docena', many: 'docenas' },
  Bag: { one: 'bolsa', many: 'bolsas' },
};

/** "Libra", "Unidad": for selects. */
export function unitName(unit: UnitCode): string {
  const one = UNIT_LABELS[unit].one;
  return one.charAt(0).toUpperCase() + one.slice(1);
}

/** "/ lb", "/ unidad": next to a price. */
export function perUnit(unit: UnitCode): string {
  return `/ ${UNIT_LABELS[unit].short ?? UNIT_LABELS[unit].one}`;
}

/** 2500 lb → "2.5 lb"; 1000 units → "1 unidad"; 4000 → "4 unidades". */
export function formatQuantityWithUnit(milli: number, unit: UnitCode): string {
  const label = UNIT_LABELS[unit];
  const word = label.short ?? (milli === ONE_UNIT_MILLI ? label.one : label.many);
  return `${formatQuantity(milli)} ${word}`;
}
