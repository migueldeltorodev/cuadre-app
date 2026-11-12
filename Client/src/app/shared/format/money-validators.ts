import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { parseHundredths } from './money';

/**
 * Validates a text input holding an amount (or percentage) and its range, in hundredths.
 * Errors: { amount: true } when it is not a number; { min } / { max } with the limits in hundredths.
 */
export function amountValidator(options: { min?: number; max?: number } = {}): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const value = parseHundredths(control.value);
    if (value === null) {
      return { amount: true };
    }
    if (options.min !== undefined && value < options.min) {
      return { min: { min: options.min, actual: value } };
    }
    if (options.max !== undefined && value > options.max) {
      return { max: { max: options.max, actual: value } };
    }
    return null;
  };
}
