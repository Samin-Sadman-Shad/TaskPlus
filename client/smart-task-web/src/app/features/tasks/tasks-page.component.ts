import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { UserOption } from '../../shared/components/user-picker.component';
import { TaskListComponent } from './task-list.component';

@Component({
  selector: 'app-tasks-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PageHeaderComponent, TaskListComponent],
  template: `
    <app-page-header title="Tasks" [subtitle]="subtitle()" />
    <app-task-list [assignees]="assigneeOptions()" />
  `,
})
export default class TasksPageComponent {
  private readonly auth = inject(AuthService);

  protected readonly subtitle = computed(() =>
    this.auth.hasRole('Admin')
      ? 'All tasks. Open a project to add tasks.'
      : this.auth.hasRole('ProjectManager')
        ? 'Tasks in your projects. Open a project to add tasks.'
        : 'Tasks in the projects you belong to.',
  );

  /** The global list offers an "assigned to me" filter. */
  protected readonly assigneeOptions = computed<UserOption[]>(() => {
    const me = this.auth.currentUser();
    return me ? [{ id: me.id, fullName: `Me (${me.fullName})`, email: me.email }] : [];
  });
}
