import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Observable } from 'rxjs';
import { ProjectConstraints } from '../../core/models/constraints';
import { ControlErrorComponent } from '../../shared/forms/control-error.component';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { dateRange, minDate, notBlank } from '../../shared/forms/validators';
import { fromDateOnly, toDateOnly, today } from '../../shared/utils/date-only';
import { ProjectsApi } from './data-access/projects.api';
import { ProjectDto } from './data-access/projects.models';

export interface ProjectFormDialogData {
  /** Present when editing. */
  project?: ProjectDto;
}

@Component({
  selector: 'app-project-form-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    ControlErrorComponent,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit project' : 'New project' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (formError()) {
          <p class="form-error" role="alert">{{ formError() }}</p>
        }
        <div class="form-grid">
          <mat-form-field class="full">
            <mat-label>Name</mat-label>
            <input matInput formControlName="name" [maxlength]="limits.nameMaxLength" cdkFocusInitial />
            <mat-error><app-control-error [control]="form.controls.name" label="Name" /></mat-error>
          </mat-form-field>
          <mat-form-field class="full">
            <mat-label>Description</mat-label>
            <textarea matInput formControlName="description" rows="4" [maxlength]="limits.descriptionMaxLength"></textarea>
            <mat-error><app-control-error [control]="form.controls.description" label="Description" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Start date</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="startDate" [min]="isEdit ? null : earliestStart" />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" aria-label="Open start date calendar" />
            <mat-datepicker #startPicker />
            <mat-error><app-control-error [control]="form.controls.startDate" label="Start date" /></mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>End date (optional)</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="endDate" [min]="form.controls.startDate.value" />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" aria-label="Open end date calendar" />
            <mat-datepicker #endPicker />
            <mat-error><app-control-error [control]="form.controls.endDate" label="End date" /></mat-error>
          </mat-form-field>
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save' }}</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class ProjectFormDialogComponent {
  protected readonly data = inject<ProjectFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<ProjectFormDialogComponent, ProjectDto>);
  private readonly api = inject(ProjectsApi);

  protected readonly isEdit = !!this.data.project;
  protected readonly limits = ProjectConstraints;
  /** CreateProjectDtoValidator: start date at most one day in the past. */
  protected readonly earliestStart = today(-ProjectConstraints.startDateToleranceDays);

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      name: [
        this.data.project?.name ?? '',
        [
          Validators.required,
          notBlank,
          Validators.minLength(ProjectConstraints.nameMinLength),
          Validators.maxLength(ProjectConstraints.nameMaxLength),
        ],
      ],
      description: [this.data.project?.description ?? '', [Validators.maxLength(ProjectConstraints.descriptionMaxLength)]],
      startDate: [
        fromDateOnly(this.data.project?.startDate) ?? (this.isEdit ? null : today()),
        this.isEdit
          ? [Validators.required]
          : [Validators.required, minDate(() => today(-ProjectConstraints.startDateToleranceDays))],
      ],
      endDate: [fromDateOnly(this.data.project?.endDate)],
    },
    { validators: [dateRange('startDate', 'endDate')] },
  );

  protected save(): void {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const dto = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      startDate: toDateOnly(value.startDate) ?? '',
      endDate: toDateOnly(value.endDate),
    };
    const request = this.data.project ? this.api.update(this.data.project.id, dto) : this.api.create(dto);

    this.saving.set(true);
    request.subscribe({
      next: (project) => this.dialogRef.close(project),
      error: (error: unknown) => {
        this.saving.set(false);
        this.formError.set(applyServerErrors(this.form, error));
      },
    });
  }
}

/** Opens the dialog; emits the saved project (or undefined if cancelled). */
export function openProjectForm(dialog: MatDialog, data: ProjectFormDialogData): Observable<ProjectDto | undefined> {
  return dialog
    .open<ProjectFormDialogComponent, ProjectFormDialogData, ProjectDto>(ProjectFormDialogComponent, {
      data,
      width: '640px',
      maxWidth: '95vw',
    })
    .afterClosed();
}
