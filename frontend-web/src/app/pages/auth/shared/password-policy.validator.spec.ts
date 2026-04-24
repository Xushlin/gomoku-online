import { FormControl } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { passwordPolicyValidator } from './password-policy.validator';

function validate(value: string | null): ReturnType<ReturnType<typeof passwordPolicyValidator>> {
  const validator = passwordPolicyValidator();
  return validator(new FormControl(value));
}

describe('passwordPolicyValidator', () => {
  it('accepts valid passwords', () => {
    expect(validate('Password1')).toBeNull();
    expect(validate('abc12345')).toBeNull();
    expect(validate('Zzz99999')).toBeNull();
  });

  it('rejects short passwords with minlength error', () => {
    const errors = validate('abc1');
    expect(errors).not.toBeNull();
    expect(errors?.['minlength']).toEqual({ requiredLength: 8, actualLength: 4 });
  });

  it('rejects digit-only passwords with missingLetter', () => {
    const errors = validate('12345678');
    expect(errors?.['missingLetter']).toBe(true);
    expect(errors?.['missingDigit']).toBeUndefined();
  });

  it('rejects letter-only passwords with missingDigit', () => {
    const errors = validate('abcdefgh');
    expect(errors?.['missingDigit']).toBe(true);
    expect(errors?.['missingLetter']).toBeUndefined();
  });

  it('can surface multiple failures at once', () => {
    const errors = validate('abc');
    expect(errors?.['minlength']).toBeTruthy();
    expect(errors?.['missingDigit']).toBe(true);
    expect(errors?.['missingLetter']).toBeUndefined();
  });

  it('returns null for empty / null values (required handles presence)', () => {
    expect(validate('')).toBeNull();
    expect(validate(null)).toBeNull();
  });
});
