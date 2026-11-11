import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { catchError, filter, of, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { TASK_STATUS_TRANSITIONS, TaskItemStatus } from '../../core/models/enums';
import { NotificationService } from '../../core/notifications/notification.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { PriorityChipComponent } from '../../shared/components/priority-chip.component';
import { StatusChipComponent } from '../../shared/components/status-chip.component';
import { UserOption, UserPickerComponent } from '../../shared/components/user-picker.component';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { assignableUsers, canManageProject } from '../projects/data-access/project-permissions';
import { ProjectsApi } from '../projects/data-access/projects.api';
import { TasksApi } from './data-access/tasks.api';
import { TaskDto } from './data-access/tasks.models';
import { openTaskForm } from './task-form-dialog.component';

@Component({
  selector: 'app-task-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    StatusChipComponent,
    PriorityChipComponent,
    EmptyStateComponent,
    UserPickerComponent,
    EnumLabelPipe,
  ],
  template: `
    @if (task(); as t) {
      <a class="back" [routerLink]="['/projects', t.projectId]">
        <mat-icon aria-hidden="true">arrow_back</mat-icon> {{ t.projectName }}
      </a>
      <header class="header">
        <div>
          <h1>{{ t.title }}</h1>
          <div class="chips">
            <app-status-chip [status]="t.status" />
            <app-priority-chip [priority]="t.priority" />
            @if (t.isOverdue) {
              <span class="overdue">Overdue</span>
            }
          </div>
        </div>
        <div class="actions">
          @if (canChangeStatus() && nextStatuses().length > 0) {
            <button mat-stroked-button type="button" [matMenuTriggerFor]="statusMenu" [disabled]="busy()">
              <mat-icon>swap_horiz</mat-icon> Change status
            </button>
            <mat-menu #statusMenu="matMenu">
              @for (s of nextStatuses(); track s) {
                <button mat-menu-item type="button" (click)="changeStatus(s)">{{ s | enumLabel }}</button>
              }
            </mat-menu>
          }
          @if (canManage()) {
            <button mat-stroked-button type="button" (click)="edit(t)"><mat-icon>edit</mat-icon> Edit</button>
            <button mat-stroked-button type="button" class="delete" (click)="remove(t)"><mat-icon>delete</mat-icon> Delete</button>
          }
        </div>
      </header>

      <div class="layout">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Description</mat-card-title></mat-card-header>
          <mat-card-content>
            @if (t.description) {
              <p class="description">{{ t.description }}</p>
            } @else {
              <p class="muted">No description.</p>
            }
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined">
          <mat-card-content>
            <dl class="facts">
              <dt>Assignee</dt>
              <dd>
                {{ t.assignee?.fullName ?? 'Unassigned' }}
                @if (canManage() && t.assignee) {
                  <button mat-button type="button" (click)="assign(null)" [disabled]="busy()">Unassign</button>
                }
              </dd>
              <dt>Due date</dt>
              <dd [class.overdue]="t.isOverdue">{{ t.dueDate ? (t.dueDate | date: 'mediumDate') : 'None' }}</dd>
              <dt>Created</dt>
              <dd>{{ t.createdAt | date: 'medium' }}</dd>
              @if (t.updatedAt) {
                <dt>Updated</dt>
                <dd>{{ t.updatedAt | date: 'medium' }}</dd>
              }
              @if (t.completedAt) {
                <dt>Completed</dt>
                <dd>{{ t.completedAt | date: 'medium' }}</dd>
              }
            </dl>
            @if (canManage()) {
              <div class="assign">
                <app-user-picker
                  label="Assign to"
                  placeholder="Project owner or member"
                  [search]="searchAssignees"
                  [excludeIds]="t.assignee ? [t.assignee.id] : []"
                  (picked)="assign($event.id)"
                />
              </div>
            }
          </mat-card-content>
        </mat-card>
      </div>
    } @else if (taskResource.isLoading()) {
      <div class="loading"><mat-spinner diameter="40" aria-label="Loading task" /></div>
    } @else {
      <app-empty-state icon="search_off" title="Task not found" message="It may have been deleted, or you no longer have access.">
        <a mat-stroked-button routerLink="/tasks">Back to tasks</a>
      </app-empty-state>
    }
  `,
  styles: `
    .back { display: inline-flex; align-items: center; gap: 4px; margin-bottom: 12px; text-decoration: none; }
    .header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
    h1 { margin: 0 0 8px; font: var(--mat-sys-headline-small); word-break: break-word; }
    .chips { display: flex; align-items: center; gap: 12px; }
    .actions { display: flex; flex-wrap: wrap; gap: 8px; align-items: flex-start; }
    .delete { color: var(--app-danger); }
    .layout { display: grid; grid-template-columns: 2fr 1fr; gap: 16px; align-items: start; }
    .description { white-space: pre-wrap; margin: 0; }
    .facts { margin: 0; display: grid; gap: 4px; }
    dt { color: var(--app-muted); font-size: 12px; margin-top: 8px; }
    dd { margin: 0; display: flex; align-items: center; gap: 8px; flex-wrap: wrap; }
    .assign { margin-top: 16px; }
    .loading { display: grid; place-items: center; padding: 48px; }
    @media (max-width: 959.98px) { .layout { grid-template-columns: 1fr; } }
  `,
})
export default class TaskDetailComponent {
  private readonly tasksApi = inject(TasksApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  /** Route param :id (withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly taskResource = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.tasksApi.get(params),
  });

  /** Local copy so updates from the API replace it without refetching. */
  protected readonly task = linkedSignal<TaskDto | undefined>(() =>
    this.taskResource.hasValue() ? this.taskResource.value() : undefined,
  );

  /** The project tells us who owns it (permissions) and who can be assigned. */
  private readonly project = rxResource({
    params: () => this.task()?.projectId,
    stream: ({ params }) => this.projectsApi.get(params),
  });

  protected readonly busy = signal(false);

  protected readonly canManage = computed(() =>
    canManageProject(this.auth.currentUser(), this.project.hasValue() ? this.project.value() : null),
  );

  /** Admin, owning PM, or the assignee (CLAUDE.md §4). */
  protected readonly canChangeStatus = computed(() => {
    const me = this.auth.currentUser();
    return this.canManage() || (!!me && this.task()?.assignee?.id === me.id);
  });

  protected readonly nextStatuses = computed(() => {
    const status = this.task()?.status;
    return status ? TASK_STATUS_TRANSITIONS[status] : [];
  });

  private readonly assignees = computed<UserOption[]>(() =>
    this.project.hasValue() ? assignableUsers(this.project.value()) : [],
  );

  /** Local filter over project owner + members for the assign autocomplete. */
  protected readonly searchAssignees = (term: string) => {
    const t = term.toLowerCase();
    return of(this.assignees().filter((u) => !t || u.fullName.toLowerCase().includes(t) || u.email.toLowerCase().includes(t)));
  };

  protected changeStatus(status: TaskItemStatus): void {
    this.run(this.tasksApi.changeStatus(this.id(), status), 'Status updated.');
  }

  protected assign(assigneeId: string | null): void {
    this.run(this.tasksApi.assign(this.id(), assigneeId), assigneeId ? 'Task assigned.' : 'Task unassigned.');
  }

  protected edit(task: TaskDto): void {
    openTaskForm(this.dialog, { projectId: task.projectId, task, assignees: this.assignees() })
      .pipe(filter((saved): saved is TaskDto => !!saved))
      .subscribe((saved) => {
        this.task.set(saved);
        this.notifications.success('Task saved.');
      });
  }

  protected remove(task: TaskDto): void {
    this.confirm
      .confirm({ title: 'Delete task?', message: `"${task.title}" will be deleted.`, confirmText: 'Delete', destructive: true })
      .pipe(
        filter(Boolean),
        switchMap(() => this.tasksApi.delete(task.id)),
      )
      .subscribe(() => {
        this.notifications.success('Task deleted.');
        void this.router.navigate(['/projects', task.projectId]);
      });
  }

  private run(request: ReturnType<TasksApi['get']>, message: string): void {
    this.busy.set(true);
    request.pipe(catchError(() => of(null))).subscribe((updated) => {
      this.busy.set(false);
      if (updated) {
        this.task.set(updated);
        this.notifications.success(message);
      }
    });
  }
}
