import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/notifications/notification.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { ControlErrorComponent } from '../../shared/forms/control-error.component';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { matchingFields, passwordRules } from '../../shared/forms/validators';

function differentFromCurrent(group: AbstractControl): ValidationErrors | null {
  const next = group.get('newPassword');
  const same = !!next?.value && next.value === group.get('currentPassword')?.value;
  if (same) {
    next?.setErrors({ ...(next.errors ?? {}), sameAsCurrent: true });
  } else if (next?.hasError('sameAsCurrent')) {
    const rest = { ...(next.errors ?? {}) };
    delete rest['sameAsCurrent'];
    next.setErrors(Object.keys(rest).length ? rest : null);
  }
  return null;
}

@Component({
  selector: 'app-change-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    PageHeaderComponent,
    ControlErrorComponent,
  ],
  template: `
    <app-page-header title="Change password" />
    <mat-card appearance="outlined" class="card">
      <mat-card-content>
        @if (mustChange()) {
          <p class="notice" role="status">
            <mat-icon aria-hidden="true">info</mat-icon>
            Your password was set by an administrator. Choose your own password to continue.
          </p>
        }
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (formError()) {
            <p class="form-error" role="alert">{{ formError() }}</p>
          }
          <mat-form-field class="full-width">
            <mat-label>Current password</mat-label>
            <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
            <mat-error><app-control-error [control]="form.controls.currentPassword" label="Current password" /></mat-error>
          </mat-form-field>
          <mat-form-field class="full-width">
            <mat-label>New password</mat-label>
            <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
            <mat-hint>8+ characters, upper- and lower-case, a digit and a symbol</mat-hint>
            <mat-error><app-control-error [control]="form.controls.newPassword" label="New password" /></mat-error>
          </mat-form-field>
          <mat-form-field class="full-width confirm">
            <mat-label>Confirm new password</mat-label>
            <input matInput type="password" formControlName="confirmNewPassword" autocomplete="new-password" />
            <mat-error><app-control-error [control]="form.controls.confirmNewPassword" label="Confirm new password" /></mat-error>
          </mat-form-field>
          <p class="muted">Changing your password signs you out on every other device.</p>
          <button mat-flat-button type="submit" [disabled]="submitting()">
            {{ submitting() ? 'Saving…' : 'Change password' }}
          </button>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .card { max-width: 480px; }
    .notice { display: flex; gap: 8px; align-items: flex-start; padding: 12px; border: 1px solid #bfdbfe; border-radius: 8px; background: #eff6ff; margin: 0 0 16px; }
    .confirm { margin-top: 8px; }
  `,
})
export default class ChangePasswordComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly mustChange = this.auth.mustChangePassword;
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, passwordRules]],
      confirmNewPassword: ['', [Validators.required]],
    },
    { validators: [matchingFields('newPassword', 'confirmNewPassword'), differentFromCurrent] },
  );

  protected submit(): void {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    // AuthService stores the new access token returned by the API.
    this.auth.changePassword(this.form.getRawValue()).subscribe({
      next: () => {
        this.notifications.success('Password changed.');
        void this.router.navigateByUrl('/dashboard');
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.formError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
