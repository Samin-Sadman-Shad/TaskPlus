import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { UserConstraints } from '../../core/models/constraints';

/** Required after trimming (the API trims strings before checking them). */
export const notBlank: ValidatorFn = (control) =>
  typeof control.value === 'string' && control.value.trim().length === 0 ? { required: true } : null;

/** Letters, spaces, hyphens, apostrophes and periods, with at least one letter (PersonNamePattern). */
export const personName: ValidatorFn = (control) => {
  const value = typeof control.value === 'string' ? control.value.trim() : '';
  return value.length === 0 || /^(?=.*\p{L})[\p{L}\p{M} .'-]+$/u.test(value) ? null : { personName: true };
};

/** Mirrors RuleBuilderExtensions.ValidPassword. */
export const passwordRules: ValidatorFn = (control) => {
  const value = typeof control.value === 'string' ? control.value : '';
  if (value.length === 0) {
    return null;
  }
  if (value.length < UserConstraints.passwordMinLength || value.length > UserConstraints.passwordMaxLength) {
    return { passwordLength: true };
  }
  if (!/\p{Lu}/u.test(value)) return { passwordUpper: true };
  if (!/\p{Ll}/u.test(value)) return { passwordLower: true };
  if (!/\p{Nd}/u.test(value)) return { passwordDigit: true };
  if (!/[^\p{L}\p{Nd}]/u.test(value)) return { passwordSymbol: true };
  return null;
};

/** Group validator: the password must not contain the email's local part (3+ chars). */
export function passwordNotContainingEmail(passwordKey: string, emailKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const password = group.get(passwordKey);
    const email = String(group.get(emailKey)?.value ?? '').trim();
    const localPart = email.split('@')[0] ?? '';
    const contains =
      localPart.length >= 3 && String(password?.value ?? '').toLowerCase().includes(localPart.toLowerCase());
    toggleError(password, 'passwordContainsEmail', contains);
    return null;
  };
}

/** Group validator: `confirmKey` must equal `key`. The error is put on the confirm control. */
export function matchingFields(key: string, confirmKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const confirm = group.get(confirmKey);
    const mismatch = !!confirm?.value && group.get(key)?.value !== confirm.value;
    toggleError(confirm, 'mismatch', mismatch);
    return null;
  };
}

/** Group validator: end date on or after start date. The error is put on the end control. */
export function dateRange(startKey: string, endKey: string): ValidatorFn {
  return (group: AbstractControl): ValidationErrors | null => {
    const start = group.get(startKey)?.value as Date | null | undefined;
    const end = group.get(endKey);
    const endValue = end?.value as Date | null | undefined;
    toggleError(end, 'dateRange', !!start && !!endValue && endValue.getTime() < start.getTime());
    return null;
  };
}

/** The date must not be before `min` (a local-midnight Date). */
export function minDate(min: () => Date): ValidatorFn {
  return (control) => {
    const value = control.value as Date | null;
    return value && value.getTime() < min().getTime() ? { minDate: true } : null;
  };
}

/** Adds or removes one error key without touching the control's other errors. */
function toggleError(control: AbstractControl | null | undefined, key: string, on: boolean): void {
  if (!control) {
    return;
  }
  const errors = { ...(control.errors ?? {}) };
  if (on === key in errors) {
    return;
  }
  if (on) {
    errors[key] = true;
  } else {
    delete errors[key];
  }
  control.setErrors(Object.keys(errors).length > 0 ? errors : null);
}
