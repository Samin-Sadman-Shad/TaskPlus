import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { UserConstraints } from '../../core/models/constraints';
import { NotificationService } from '../../core/notifications/notification.service';
import { ControlErrorComponent } from '../../shared/forms/control-error.component';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { matchingFields, notBlank, passwordNotContainingEmail, passwordRules, personName } from '../../shared/forms/validators';

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, ControlErrorComponent],
  template: `
    <div class="card">
      <div class="brand"><mat-icon aria-hidden="true">check_circle</mat-icon> Smart Task</div>
      <h1>Create account</h1>
      <p class="lead">New accounts start as team members. An admin can change your role.</p>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (formError()) {
          <p class="form-error" role="alert">{{ formError() }}</p>
        }
        <div class="form-grid">
          <mat-form-field>
            <mat-label>First name</mat-label>
            <input matInput formControlName="firstName" autocomplete="given-name" />
            <mat-error><app-control-error [control]="form.controls.firstName" label="First name" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Last name</mat-label>
            <input matInput formControlName="lastName" autocomplete="family-name" />
            <mat-error><app-control-error [control]="form.controls.lastName" label="Last name" /></mat-error>
          </mat-form-field>
        </div>

        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="email" />
          <mat-error><app-control-error [control]="form.controls.email" label="Email" /></mat-error>
        </mat-form-field>

        <mat-form-field>
          <mat-label>Password</mat-label>
          <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password" />
          <button
            mat-icon-button
            matSuffix
            type="button"
            [attr.aria-label]="showPassword() ? 'Hide passwords' : 'Show passwords'"
            [attr.aria-pressed]="showPassword()"
            (click)="showPassword.set(!showPassword())"
          >
            <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
          <mat-error><app-control-error [control]="form.controls.password" label="Password" /></mat-error>
        </mat-form-field>
        <ul class="hint-list">
          <li>{{ minLength }}+ characters with upper- and lower-case letters, a digit and a symbol</li>
          <li>Must not contain your email name</li>
        </ul>

        <mat-form-field>
          <mat-label>Confirm password</mat-label>
          <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="confirmPassword" autocomplete="new-password" />
          <mat-error><app-control-error [control]="form.controls.confirmPassword" label="Confirm password" /></mat-error>
        </mat-form-field>

        <button mat-flat-button class="submit" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Creating account…' : 'Create account' }}
        </button>
      </form>
      <p class="switch">Already registered? <a routerLink="/login">Sign in</a></p>
    </div>
  `,
  styleUrl: './auth-card.scss',
})
export default class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly minLength = UserConstraints.passwordMinLength;
  protected readonly showPassword = signal(false);
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      firstName: ['', [Validators.required, notBlank, Validators.maxLength(UserConstraints.firstNameMaxLength), personName]],
      lastName: ['', [Validators.required, notBlank, Validators.maxLength(UserConstraints.lastNameMaxLength), personName]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(UserConstraints.emailMaxLength)]],
      password: ['', [Validators.required, passwordRules]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchingFields('password', 'confirmPassword'), passwordNotContainingEmail('password', 'email')] },
  );

  protected submit(): void {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => {
        this.notifications.success('Account created. Please sign in.');
        void this.router.navigateByUrl('/login');
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.formError.set(applyServerErrors(this.form, error));
      },
    });
  }
}
