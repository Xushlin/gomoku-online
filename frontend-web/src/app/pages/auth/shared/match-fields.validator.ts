import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Form-level validator that sets `{ mismatch: true }` on the `confirm` control
 * when the two named controls have different values. Useful for
 * newPassword / confirmPassword pairs.
 */
export function matchFieldsValidator(primary: string, confirm: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const a = group.get(primary);
    const b = group.get(confirm);
    if (!a || !b) return null;
    if (a.value === b.value) {
      if (b.errors && b.errors['mismatch']) {
        const rest = { ...b.errors };
        delete rest['mismatch'];
        b.setErrors(Object.keys(rest).length ? rest : null);
      }
      return null;
    }
    b.setErrors({ ...(b.errors ?? {}), mismatch: true });
    return null;
  };
}
