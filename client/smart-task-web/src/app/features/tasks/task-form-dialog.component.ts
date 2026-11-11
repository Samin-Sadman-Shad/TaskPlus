import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { readApiError } from '../../core/http/api-url';
import { AiConstraints, TaskItemConstraints } from '../../core/models/constraints';
import { TASK_PRIORITIES, TaskPriority } from '../../core/models/enums';
import { UserOption } from '../../shared/components/user-picker.component';
import { ControlErrorComponent } from '../../shared/forms/control-error.component';
import { applyServerErrors } from '../../shared/forms/server-errors';
import { minDate, notBlank } from '../../shared/forms/validators';
import { fromDateOnly, toDateOnly, today } from '../../shared/utils/date-only';
import { AiApi, TasksApi } from './data-access/tasks.api';
import { ImproveDescriptionResponseDto, TaskDto } from './data-access/tasks.models';

export interface TaskFormDialogData {
  projectId: string;
  /** Present when editing. */
  task?: TaskDto;
  /** Owner + members; only used when creating (assignment has its own endpoint afterwards). */
  assignees: UserOption[];
}

@Component({
  selector: 'app-task-form-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    ControlErrorComponent,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit task' : 'New task' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <mat-dialog-content>
        @if (formError()) {
          <p class="form-error" role="alert">{{ formError() }}</p>
        }
        <div class="form-grid">
          <mat-form-field class="full">
            <mat-label>Title</mat-label>
            <input matInput formControlName="title" [maxlength]="limits.titleMaxLength" cdkFocusInitial />
            <mat-error><app-control-error [control]="form.controls.title" label="Title" /></mat-error>
          </mat-form-field>

          <mat-form-field class="full">
            <mat-label>Description</mat-label>
            <textarea matInput formControlName="description" rows="5" [maxlength]="limits.descriptionMaxLength"></textarea>
            <mat-hint align="end">{{ descriptionLength() }} / {{ limits.descriptionMaxLength }}</mat-hint>
            <mat-error><app-control-error [control]="form.controls.description" label="Description" /></mat-error>
          </mat-form-field>

          <div class="full ai-row">
            <button mat-stroked-button type="button" [disabled]="!canImprove() || aiLoading()" (click)="improve()">
              @if (aiLoading()) {
                <mat-spinner diameter="18" aria-hidden="true" />
              } @else {
                <mat-icon>auto_awesome</mat-icon>
              }
              Improve with AI
            </button>
            @if (!canImprove() && !aiLoading()) {
              <span class="muted small">Write at least {{ ai.inputMinLength }} characters (up to {{ ai.inputMaxLength }}) first.</span>
            }
            <span class="muted small privacy">
              The title and description are sent to an external AI service. Don't include confidential information.
            </span>
          </div>
          @if (aiError()) {
            <p class="full form-error" role="alert">{{ aiError() }}</p>
          }

          @if (suggestion(); as s) {
            <section class="full ai-review" aria-label="AI suggestion">
              <div class="compare">
                <div>
                  <h3>Original</h3>
                  <p class="text">{{ s.originalDescription }}</p>
                </div>
                <div>
                  <h3>Suggestion</h3>
                  <p class="text">{{ s.improvedDescription }}</p>
                </div>
              </div>
              @if (s.suggestions.length > 0) {
                <h3>Tips</h3>
                <ul>
                  @for (tip of s.suggestions; track $index) {
                    <li>{{ tip }}</li>
                  }
                </ul>
              }
              <div class="ai-actions">
                <button mat-button type="button" (click)="discardSuggestion()">Discard</button>
                <button mat-flat-button type="button" (click)="acceptSuggestion(s)">Accept</button>
              </div>
            </section>
          }

          <mat-form-field>
            <mat-label>Priority</mat-label>
            <mat-select formControlName="priority">
              @for (p of priorities; track p) {
                <mat-option [value]="p">{{ p }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field>
            <mat-label>Due date</mat-label>
            <input matInput [matDatepicker]="duePicker" formControlName="dueDate" [min]="isEdit ? null : todayDate" />
            <mat-datepicker-toggle matIconSuffix [for]="duePicker" aria-label="Open due date calendar" />
            <mat-datepicker #duePicker />
            <mat-error><app-control-error [control]="form.controls.dueDate" label="Due date" /></mat-error>
          </mat-form-field>

          @if (!isEdit) {
            <mat-form-field class="full">
              <mat-label>Assignee</mat-label>
              <mat-select formControlName="assigneeId">
                <mat-option [value]="null">Unassigned</mat-option>
                @for (u of data.assignees; track u.id) {
                  <mat-option [value]="u.id">{{ u.fullName }}</mat-option>
                }
              </mat-select>
              <mat-error><app-control-error [control]="form.controls.assigneeId" label="Assignee" /></mat-error>
            </mat-form-field>
          }
        </div>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .ai-row { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; margin: 4px 0 16px; }
    .ai-row mat-spinner { display: inline-block; margin-right: 6px; }
    .small { font-size: 12px; }
    .privacy { flex-basis: 100%; }
    .ai-review { border: 1px solid var(--app-border); border-radius: 8px; padding: 12px; margin-bottom: 16px; }
    .compare { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .compare > div { min-width: 0; }
    h3 { margin: 0 0 6px; font-size: 13px; font-weight: 500; color: var(--app-muted); text-transform: uppercase; letter-spacing: 0.04em; }
    .text { white-space: pre-wrap; margin: 0; padding: 8px; background: #f9fafb; border-radius: 6px; max-height: 220px; overflow: auto; }
    ul { margin: 4px 0 0; padding-left: 18px; }
    .ai-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 12px; }
    @media (max-width: 599.98px) { .compare { grid-template-columns: 1fr; } }
  `,
})
export class TaskFormDialogComponent {
  protected readonly data = inject<TaskFormDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<TaskFormDialogComponent, TaskDto>);
  private readonly tasksApi = inject(TasksApi);
  private readonly aiApi = inject(AiApi);

  protected readonly isEdit = !!this.data.task;
  protected readonly limits = TaskItemConstraints;
  protected readonly ai = AiConstraints;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly todayDate = today();

  protected readonly saving = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly aiLoading = signal(false);
  protected readonly aiError = signal<string | null>(null);
  protected readonly suggestion = signal<ImproveDescriptionResponseDto | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: [
      this.data.task?.title ?? '',
      [
        Validators.required,
        notBlank,
        Validators.minLength(TaskItemConstraints.titleMinLength),
        Validators.maxLength(TaskItemConstraints.titleMaxLength),
      ],
    ],
    description: [this.data.task?.description ?? '', [Validators.maxLength(TaskItemConstraints.descriptionMaxLength)]],
    priority: [this.data.task?.priority ?? ('Medium' as TaskPriority)],
    // Only a new task's due date must not be in the past (CreateTaskDtoValidator); editing keeps old dates.
    dueDate: [fromDateOnly(this.data.task?.dueDate), this.isEdit ? [] : [minDate(() => today())]],
    assigneeId: [null as string | null],
  });

  private readonly description = toSignal(this.form.controls.description.valueChanges, {
    initialValue: this.form.controls.description.value,
  });
  protected readonly descriptionLength = computed(() => this.description().length);
  protected readonly canImprove = computed(() => {
    const length = this.description().trim().length;
    return length >= AiConstraints.inputMinLength && length <= AiConstraints.inputMaxLength;
  });

  /** Asks the AI for a better description. The result is only shown; the user must accept it. */
  protected improve(): void {
    this.aiError.set(null);
    this.suggestion.set(null);
    this.aiLoading.set(true);
    const title = this.form.controls.title.value.trim();
    this.aiApi.improveDescription({ title: title || null, description: this.description().trim() }).subscribe({
      next: (result) => {
        this.aiLoading.set(false);
        this.suggestion.set(result);
      },
      error: (error: unknown) => {
        this.aiLoading.set(false);
        this.aiError.set(aiErrorMessage(error));
      },
    });
  }

  protected acceptSuggestion(result: ImproveDescriptionResponseDto): void {
    this.form.controls.description.setValue(result.improvedDescription);
    this.form.controls.description.markAsDirty();
    this.suggestion.set(null);
  }

  protected discardSuggestion(): void {
    this.suggestion.set(null);
  }

  protected save(): void {
    this.formError.set(null);
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const details = {
      title: value.title.trim(),
      description: value.description.trim() || null,
      priority: value.priority,
      dueDate: toDateOnly(value.dueDate),
    };
    const request: Observable<TaskDto> = this.data.task
      ? this.tasksApi.update(this.data.task.id, details)
      : this.tasksApi.create(this.data.projectId, { ...details, assigneeId: value.assigneeId });

    this.saving.set(true);
    request.subscribe({
      next: (task) => this.dialogRef.close(task),
      error: (error: unknown) => {
        this.saving.set(false);
        this.formError.set(applyServerErrors(this.form, error));
      },
    });
  }
}

function aiErrorMessage(error: unknown): string {
  const body = readApiError(error);
  if (error instanceof HttpErrorResponse && error.status === 429) {
    return 'Too many AI requests. Please wait a minute and try again.';
  }
  return body?.message ?? 'The AI service is not available right now. Please try again later.';
}

/** Opens the dialog; emits the saved task (or undefined if cancelled). */
export function openTaskForm(dialog: MatDialog, data: TaskFormDialogData): Observable<TaskDto | undefined> {
  return dialog
    .open<TaskFormDialogComponent, TaskFormDialogData, TaskDto>(TaskFormDialogComponent, {
      data,
      width: '720px',
      maxWidth: '95vw',
      autoFocus: 'first-tabbable',
    })
    .afterClosed();
}
