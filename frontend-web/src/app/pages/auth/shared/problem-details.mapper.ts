import type { FormGroup } from '@angular/forms';
import type { ProblemDetails } from '../../../core/auth/problem-details';

function toControlName(fieldName: string): string {
  if (fieldName.length === 0) return fieldName;
  return fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
}

/**
 * Walk `problem.errors` and attach each `field → messages[0]` onto the
 * matching FormControl as `{ server: msg }`. Returns true iff at least one
 * field matched; callers can fall back to a form-group-level banner when
 * false.
 */
export function mapProblemDetailsToForm(form: FormGroup, problem: ProblemDetails): boolean {
  if (!problem.errors) return false;
  let matched = false;
  for (const [field, messages] of Object.entries(problem.errors)) {
    if (!Array.isArray(messages) || messages.length === 0) continue;
    const controlName = toControlName(field);
    const control = form.get(controlName);
    if (!control) continue;
    control.setErrors({ ...(control.errors ?? {}), server: messages[0] });
    control.markAsTouched();
    matched = true;
  }
  return matched;
}
