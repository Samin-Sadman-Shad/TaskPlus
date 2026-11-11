import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { CHANGE_PASSWORD_URL } from '../../core/guards/password-change.guard';
import { UserConstraints } from '../../core/models/constraints';
import { ControlErrorComponent } from '../../shared/forms/control-error.component';
import { applyServerErrors } from '../../shared/forms/server-errors';

/** Only same-app paths are accepted as a return URL (no open redirect to another site). */
export function safeReturnUrl(url: string | undefined): string {
  return url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/login') ? url : '/dashboard';
}

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, ControlErrorComponent],
  template: `
    <div class="card">
      <div class="brand"><mat-icon aria-hidden="true">check_circle</mat-icon> Smart Task</div>
      <h1>Sign in</h1>
      <p class="lead">Welcome back. Sign in to see your projects and tasks.</p>

      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        @if (formError()) {
          <p class="form-error" role="alert">{{ formError() }}</p>
        }
        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="username" />
          <mat-error><app-control-error [control]="form.controls.email" label="Email" /></mat-error>
        </mat-form-field>

        <mat-form-field>
          <mat-label>Password</mat-label>
          <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" />
          <button
            mat-icon-button
            matSuffix
            type="button"
            [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'"
            [attr.aria-pressed]="showPassword()"
            (click)="showPassword.set(!showPassword())"
          >
            <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
          <mat-error><app-control-error [control]="form.controls.password" label="Password" /></mat-error>
        </mat-form-field>

        <button mat-flat-button class="submit" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
      <p class="switch">No account yet? <a routerLink="/register">Create one</a></p>
    </div>
  `,
  styleUrl: './auth-card.scss',
})
export default class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Bound from ?returnUrl= by withComponentInputBinding. */
  readonly returnUrl = input<string>();

  protected readonly showPassword = signal(false);
  protected readonly submitting = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(UserConstraints.emailMaxLength)]],
    password: ['', [Validators.required]],
  });

  protected submit(): void {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: (user) => {
        void this.router.navigateByUrl(user.mustChangePassword ? CHANGE_PASSWORD_URL : safeReturnUrl(this.returnUrl()));
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        if (error instanceof HttpErrorResponse && error.status === 401) {
          // Same message for unknown email, wrong password or inactive account: no account enumeration.
          this.formError.set('Invalid email or password.');
        } else {
          this.formError.set(applyServerErrors(this.form, error));
        }
      },
    });
  }
}
