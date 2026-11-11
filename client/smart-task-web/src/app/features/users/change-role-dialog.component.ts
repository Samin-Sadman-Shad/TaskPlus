import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { UserDto } from '../../core/auth/auth.models';
import { USER_ROLES, UserRole } from '../../core/models/enums';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';

/** Select a new role and confirm. Closes with the chosen role, or undefined when cancelled. */
@Component({
  selector: 'app-change-role-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatSelectModule, MatButtonModule, EnumLabelPipe],
  template: `
    <h2 mat-dialog-title>Change role</h2>
    <mat-dialog-content>
      <p>
        {{ user.fullName }} is currently <strong>{{ user.role | enumLabel }}</strong>.
      </p>
      <mat-form-field class="full-width">
        <mat-label>New role</mat-label>
        <mat-select [formControl]="role">
          @for (r of roles; track r) {
            <mat-option [value]="r">{{ r | enumLabel }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <p class="muted">The change applies when their current access token expires (within 15 minutes) or at next sign-in.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button type="button" [disabled]="role.value === user.role" [mat-dialog-close]="role.value">
        Change role
      </button>
    </mat-dialog-actions>
  `,
})
export class ChangeRoleDialogComponent {
  protected readonly user = inject<UserDto>(MAT_DIALOG_DATA);
  protected readonly roles = USER_ROLES;
  protected readonly role = new FormControl<UserRole>(this.user.role, { nonNullable: true });
}
