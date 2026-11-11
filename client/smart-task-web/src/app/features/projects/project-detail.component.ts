import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, viewChild } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router, RouterLink } from '@angular/router';
import { filter, map, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { NotificationService } from '../../core/notifications/notification.service';
import { UserLookupApi } from '../../core/users/user-lookup.api';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { UserOption, UserPickerComponent } from '../../shared/components/user-picker.component';
import { TaskDto } from '../tasks/data-access/tasks.models';
import { openTaskForm } from '../tasks/task-form-dialog.component';
import { TaskListComponent } from '../tasks/task-list.component';
import { assignableUsers, canManageProject } from './data-access/project-permissions';
import { ProjectsApi } from './data-access/projects.api';
import { ProjectDto, ProjectMemberDto } from './data-access/projects.models';
import { openProjectForm } from './project-form-dialog.component';

@Component({
  selector: 'app-project-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    MatProgressSpinnerModule,
    EmptyStateComponent,
    UserPickerComponent,
    TaskListComponent,
  ],
  template: `
    @if (project.hasValue()) {
      @let p = project.value();
      <a class="back" routerLink="/projects"><mat-icon aria-hidden="true">arrow_back</mat-icon> Projects</a>
      <header class="header">
        <div>
          <h1>{{ p.name }}</h1>
          <p class="muted">
            {{ p.startDate | date: 'mediumDate' }} – {{ p.endDate ? (p.endDate | date: 'mediumDate') : 'no end date' }}
            · Owner {{ p.owner.fullName }} · {{ p.completedTaskCount }}/{{ p.taskCount }} tasks done
          </p>
        </div>
        @if (canManage()) {
          <div class="actions">
            <button mat-stroked-button type="button" (click)="edit(p)"><mat-icon>edit</mat-icon> Edit</button>
            <button mat-stroked-button type="button" class="delete" (click)="remove(p)"><mat-icon>delete</mat-icon> Delete</button>
          </div>
        }
      </header>

      <div class="layout">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>About</mat-card-title></mat-card-header>
          <mat-card-content>
            @if (p.description) {
              <p class="description">{{ p.description }}</p>
            } @else {
              <p class="muted">No description.</p>
            }
          </mat-card-content>
        </mat-card>

        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Members ({{ p.members.length + 1 }})</mat-card-title></mat-card-header>
          <mat-card-content>
            <mat-list dense>
              <mat-list-item>
                <span matListItemTitle>{{ p.owner.fullName }}</span>
                <span matListItemLine class="muted">Owner · {{ p.owner.email }}</span>
              </mat-list-item>
              @for (m of p.members; track m.userId) {
                <mat-list-item>
                  <span matListItemTitle>{{ m.fullName }}</span>
                  <span matListItemLine class="muted">{{ m.email }}</span>
                  @if (canManage()) {
                    <button
                      mat-icon-button
                      matListItemMeta
                      type="button"
                      [attr.aria-label]="'Remove ' + m.fullName + ' from the project'"
                      (click)="removeMember(p, m)"
                    >
                      <mat-icon>person_remove</mat-icon>
                    </button>
                  }
                </mat-list-item>
              }
            </mat-list>
            @if (canManage()) {
              <app-user-picker
                label="Add member"
                [search]="searchUsers"
                [excludeIds]="memberIds()"
                (picked)="addMember(p, $event)"
              />
            }
          </mat-card-content>
        </mat-card>
      </div>

      <section class="tasks" aria-labelledby="tasks-heading">
        <div class="tasks-header">
          <h2 id="tasks-heading">Tasks</h2>
          @if (canManage()) {
            <button mat-flat-button type="button" (click)="createTask(p)"><mat-icon>add</mat-icon> New task</button>
          }
        </div>
        <app-task-list [projectId]="p.id" [assignees]="assignees()" />
      </section>
    } @else if (project.isLoading()) {
      <div class="loading"><mat-spinner diameter="40" aria-label="Loading project" /></div>
    } @else {
      <app-empty-state icon="search_off" title="Project not found" message="It may have been deleted, or you are not a member.">
        <a mat-stroked-button routerLink="/projects">Back to projects</a>
      </app-empty-state>
    }
  `,
  styles: `
    .back { display: inline-flex; align-items: center; gap: 4px; margin-bottom: 12px; text-decoration: none; }
    .header { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 16px; margin-bottom: 20px; }
    h1 { margin: 0 0 4px; font: var(--mat-sys-headline-small); word-break: break-word; }
    .header p { margin: 0; }
    .actions { display: flex; gap: 8px; align-items: flex-start; }
    .delete { color: var(--app-danger); }
    .layout { display: grid; grid-template-columns: 3fr 2fr; gap: 16px; align-items: start; }
    .description { white-space: pre-wrap; margin: 0; }
    app-user-picker { display: block; margin-top: 8px; }
    .tasks { margin-top: 28px; }
    .tasks-header { display: flex; justify-content: space-between; align-items: center; gap: 8px; margin-bottom: 12px; }
    h2 { margin: 0; font: var(--mat-sys-title-large); }
    .loading { display: grid; place-items: center; padding: 48px; }
    @media (max-width: 959.98px) { .layout { grid-template-columns: 1fr; } }
  `,
})
export default class ProjectDetailComponent {
  private readonly api = inject(ProjectsApi);
  private readonly lookup = inject(UserLookupApi);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly taskList = viewChild(TaskListComponent);

  /** Route param :id (withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly project = rxResource({
    params: () => this.id(),
    stream: ({ params }) => this.api.get(params),
  });

  protected readonly canManage = computed(() =>
    canManageProject(this.auth.currentUser(), this.project.hasValue() ? this.project.value() : null),
  );

  protected readonly assignees = computed<UserOption[]>(() =>
    this.project.hasValue() ? assignableUsers(this.project.value()) : [],
  );
  protected readonly memberIds = computed(() => this.assignees().map((u) => u.id));

  /** users/lookup (Admin, PM). Only called by users who can manage, so the role check matches. */
  protected readonly searchUsers = (term: string) =>
    this.lookup.search(term).pipe(map((users) => users.map(({ id, fullName, email }) => ({ id, fullName, email }))));

  protected edit(project: ProjectDto): void {
    openProjectForm(this.dialog, { project })
      .pipe(filter((p): p is ProjectDto => !!p))
      .subscribe((saved) => {
        this.project.set(saved);
        this.notifications.success('Project saved.');
      });
  }

  protected remove(project: ProjectDto): void {
    this.confirm
      .confirm({
        title: 'Delete project?',
        message: `"${project.name}" and its tasks will be deleted.`,
        confirmText: 'Delete',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.delete(project.id)),
      )
      .subscribe(() => {
        this.notifications.success('Project deleted.');
        void this.router.navigateByUrl('/projects');
      });
  }

  protected addMember(project: ProjectDto, user: UserOption): void {
    this.api.addMember(project.id, user.id).subscribe(() => {
      this.notifications.success(`${user.fullName} added.`);
      this.project.reload();
    });
  }

  protected removeMember(project: ProjectDto, member: ProjectMemberDto): void {
    this.confirm
      .confirm({
        title: 'Remove member?',
        message: `${member.fullName} will lose access to "${project.name}".`,
        confirmText: 'Remove',
        destructive: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.api.removeMember(project.id, member.userId)),
      )
      .subscribe(() => {
        this.notifications.success(`${member.fullName} removed.`);
        this.project.reload();
      });
  }

  protected createTask(project: ProjectDto): void {
    openTaskForm(this.dialog, { projectId: project.id, assignees: this.assignees() })
      .pipe(filter((t): t is TaskDto => !!t))
      .subscribe(() => {
        this.notifications.success('Task created.');
        this.taskList()?.reload();
        this.project.reload();
      });
  }
}
