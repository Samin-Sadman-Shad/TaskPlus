import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { UserDto } from '../../core/auth/auth.models';
import { UserConstraints } from '../../core/models/constraints';
import { USER_ROLES, UserRole } from '../../core/models/enums';
import { ControlErrorComponent } from '../../shared/forms/control-error.component';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { matchingFields, notBlank, passwordNotContainingEmail, passwordRules, personName } from '../../shared/forms/validators';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { UsersApi } from './data-access/users.api';

@Component({
  selector: 'app-create-user-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    ControlErrorComponent,
    EnumLabelPipe,
  ],
  template: `
    <h2 mat-dialog-title>New user</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        <p class="muted">The password is temporary. The user must choose a new one at first sign-in.</p>
        @if (formError()) {
          <p class="form-error" role="alert">{{ formError() }}</p>
        }
        <div class="form-grid">
          <mat-form-field>
            <mat-label>First name</mat-label>
            <input matInput formControlName="firstName" cdkFocusInitial />
            <mat-error><app-control-error [control]="form.controls.firstName" label="First name" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Last name</mat-label>
            <input matInput formControlName="lastName" />
            <mat-error><app-control-error [control]="form.controls.lastName" label="Last name" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="off" />
            <mat-error><app-control-error [control]="form.controls.email" label="Email" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Role</mat-label>
            <mat-select formControlName="role">
              @for (r of roles; track r) {
                <mat-option [value]="r">{{ r | enumLabel }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Temporary password</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" />
            <mat-error><app-control-error [control]="form.controls.password" label="Password" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Confirm password</mat-label>
            <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
            <mat-error><app-control-error [control]="form.controls.confirmPassword" label="Confirm password" /></mat-error>
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ saving() ? 'Creating…' : 'Create user' }}</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class CreateUserDialogComponent {
  private readonly api = inject(UsersApi);
  private readonly dialogRef = inject(MatDialogRef<CreateUserDialogComponent, UserDto>);

  protected readonly roles = USER_ROLES;
  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      firstName: ['', [Validators.required, notBlank, Validators.maxLength(UserConstraints.firstNameMaxLength), personName]],
      lastName: ['', [Validators.required, notBlank, Validators.maxLength(UserConstraints.lastNameMaxLength), personName]],
      email: ['', [Validators.required, Validators.email, Validators.maxLength(UserConstraints.emailMaxLength)]],
      role: ['TeamMember' as UserRole, [Validators.required]],
      password: ['', [Validators.required, passwordRules]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: [matchingFields('password', 'confirmPassword'), passwordNotContainingEmail('password', 'email')] },
  );

  protected save(): void {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    this.saving.set(true);
    this.api
      .create({ ...value, firstName: value.firstName.trim(), lastName: value.lastName.trim(), email: value.email.trim() })
      .subscribe({
        next: (user) => this.dialogRef.close(user),
        error: (error: unknown) => {
          this.saving.set(false);
          this.formError.set(applyServerErrors(this.form, error));
        },
      });
  }
}
