import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

const MIN_LENGTH = 8;
const LETTER_RE = /[A-Za-z]/;
const DIGIT_RE = /\d/;

/**
 * Password policy aligned with backend RegisterCommandValidator:
 *   - length >= 8
 *   - contains at least one letter (a-z or A-Z)
 *   - contains at least one digit (0-9)
 *
 * Multiple failures can coexist in the returned error object. Empty / null
 * values pass so that Validators.required retains sole responsibility for
 * presence.
 */
export function passwordPolicyValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw: unknown = control.value;
    if (raw === null || raw === undefined || raw === '') return null;
    const value = String(raw);

    const errors: ValidationErrors = {};
    if (value.length < MIN_LENGTH) {
      errors['minlength'] = { requiredLength: MIN_LENGTH, actualLength: value.length };
    }
    if (!LETTER_RE.test(value)) {
      errors['missingLetter'] = true;
    }
    if (!DIGIT_RE.test(value)) {
      errors['missingDigit'] = true;
    }
    return Object.keys(errors).length > 0 ? errors : null;
  };
}
