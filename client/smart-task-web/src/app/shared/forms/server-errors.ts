import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';
import { readApiError } from '../../core/http/api-url';

/**
 * Maps a 400 BaseResponse onto the form: each `errors` key that matches a control gets
 * `{ server: message }`. Returns a message for the form banner (errors with no matching control,
 * or the response message), or null when everything was placed on a field.
 */
export function applyServerErrors(form: FormGroup, error: unknown): string | null {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return null;
  }
  const body = readApiError(error);
  const unmatched: string[] = [];

  for (const [key, messages] of Object.entries(body?.errors ?? {})) {
    const control = key ? form.get(key.charAt(0).toLowerCase() + key.slice(1)) : null;
    const message = messages[0] ?? 'Invalid value.';
    if (control) {
      control.setErrors({ ...(control.errors ?? {}), server: message });
      control.markAsTouched();
    } else {
      unmatched.push(message);
    }
  }

  if (unmatched.length > 0) {
    return unmatched.join(' ');
  }
  return body?.errors ? null : (body?.message ?? 'The request was not valid.');
}
