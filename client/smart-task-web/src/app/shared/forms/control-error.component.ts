import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, ValidationErrors } from '@angular/forms';
import { startWith, switchMap } from 'rxjs';
import { UserConstraints } from '../../core/models/constraints';

/** Messages for client validators; `server` carries the API's own text. */
export function errorMessage(errors: ValidationErrors | null, label: string): string | null {
  if (!errors) {
    return null;
  }
  if (errors['server']) return String(errors['server']);
  if (errors['required']) return `${label} is required.`;
  if (errors['email']) return 'Enter a valid email address.';
  if (errors['minlength']) return `${label} must be at least ${errors['minlength'].requiredLength} characters.`;
  if (errors['maxlength']) return `${label} must not exceed ${errors['maxlength'].requiredLength} characters.`;
  if (errors['personName']) return `${label} may only contain letters, spaces, hyphens, apostrophes and periods.`;
  if (errors['passwordLength'])
    return `Password must be between ${UserConstraints.passwordMinLength} and ${UserConstraints.passwordMaxLength} characters.`;
  if (errors['passwordUpper']) return 'Password must contain an upper-case letter.';
  if (errors['passwordLower']) return 'Password must contain a lower-case letter.';
  if (errors['passwordDigit']) return 'Password must contain a digit.';
  if (errors['passwordSymbol']) return 'Password must contain a symbol (not a letter or digit).';
  if (errors['passwordContainsEmail']) return 'Password must not contain the email address.';
  if (errors['mismatch']) return 'Passwords do not match.';
  if (errors['sameAsCurrent']) return 'New password must be different from the current password.';
  if (errors['dateRange']) return 'End date must be on or after the start date.';
  if (errors['minDate']) return `${label} cannot be in the past.`;
  if (errors['matDatepickerParse']) return 'Enter a valid date.';
  return `${label} is not valid.`;
}

/**
 * Shows the first error of a control. Place it inside &lt;mat-error&gt;. Re-renders on every control
 * event, so server errors set with setErrors() appear even under OnPush.
 */
@Component({
  selector: 'app-control-error',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `{{ message() }}`,
})
export class ControlErrorComponent {
  readonly control = input.required<AbstractControl>();
  readonly label = input('This field');

  private readonly events = toSignal(
    toObservable(this.control).pipe(switchMap((control) => control.events.pipe(startWith(null)))),
  );

  protected readonly message = computed(() => {
    this.events();
    return errorMessage(this.control().errors, this.label());
  });
}
