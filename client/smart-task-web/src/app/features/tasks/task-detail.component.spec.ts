import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { makeSession, makeUser, ok } from '../../../testing/test-data';
import { AuthService } from '../../core/auth/auth.service';
import { UserOption } from '../../shared/components/user-picker.component';
import { ProjectDto } from '../projects/data-access/projects.models';
import TaskDetailComponent from './task-detail.component';
import { TaskDto } from './data-access/tasks.models';

describe('TaskDetailComponent assignment', () => {
  let backend: HttpTestingController;

  const admin = makeUser({ id: 'admin-1', role: 'Admin', fullName: 'Ada Admin', email: 'admin@example.com' });
  const task: TaskDto = {
    id: 't-1',
    projectId: 'p-1',
    projectName: 'Website',
    title: 'Login page',
    status: 'ToDo',
    priority: 'High',
    isOverdue: false,
    createdAt: '2026-11-11T00:00:00Z',
  };
  const project: ProjectDto = {
    id: 'p-1',
    name: 'Website',
    startDate: '2026-11-11',
    owner: { id: 'admin-1', fullName: 'Ada Admin', email: 'admin@example.com', role: 'Admin' },
    members: [{ userId: 'm-1', fullName: 'Mia Member', email: 'mia@example.com', joinedAt: '2026-11-11T00:00:00Z' }],
    taskCount: 1,
    completedTaskCount: 0,
    createdAt: '2026-11-11T00:00:00Z',
  };
  const outsider: UserOption = { id: 'o-1', fullName: 'Oscar Outsider', email: 'oscar@example.com' };

  async function setup() {
    TestBed.configureTestingModule({
      imports: [TaskDetailComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(AuthService).login({ email: admin.email, password: 'x' }).subscribe();
    backend.expectOne('/api/v1/auth/login').flush(ok(makeSession('token', admin)));

    const fixture = TestBed.createComponent(TaskDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    await fixture.whenStable();
    backend.expectOne('/api/v1/tasks/t-1').flush(ok(task));
    await fixture.whenStable();
    backend.expectOne('/api/v1/projects/p-1').flush(ok(project));
    await fixture.whenStable();
    return { fixture, component: fixture.componentInstance };
  }

  afterEach(() => backend.verify());

  it('suggests every active user, not only project members, and marks non-members', async () => {
    const { component } = await setup();
    const search = component['searchAssignees'] as (term: string) => import('rxjs').Observable<UserOption[]>;

    const result = firstValueFrom(search(''));
    backend.expectOne('/api/v1/users/lookup?search=').flush(
      ok([
        { id: 'admin-1', fullName: 'Ada Admin', email: 'admin@example.com', role: 'Admin' },
        { id: 'm-1', fullName: 'Mia Member', email: 'mia@example.com', role: 'TeamMember' },
        { ...outsider, role: 'TeamMember' },
      ]),
    );
    const options = await result;

    expect(options.map((o) => o.id)).toEqual(['admin-1', 'm-1', 'o-1']);
    expect(options.find((o) => o.id === 'm-1')?.note).toBeUndefined();
    expect(options.find((o) => o.id === 'o-1')?.note).toContain('will be added');
  });

  it('adds a non-member to the project before assigning them', async () => {
    const { component, fixture } = await setup();
    component['assignPicked'](outsider);

    const add = backend.expectOne('/api/v1/projects/p-1/members');
    expect(add.request.method).toBe('POST');
    expect(add.request.body).toEqual({ userId: 'o-1' });
    add.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    const assign = backend.expectOne('/api/v1/tasks/t-1/assignee');
    expect(assign.request.body).toEqual({ assigneeId: 'o-1' });
    assign.flush(ok({ ...task, assignee: { ...outsider, role: 'TeamMember' } }));

    // The project is reloaded so the new member shows up everywhere.
    backend.expectOne('/api/v1/projects/p-1').flush(ok({ ...project, members: [...project.members] }));
    await fixture.whenStable();
    expect(component['task']()?.assignee?.id).toBe('o-1');
  });

  it('assigns a member directly', async () => {
    const { component } = await setup();
    component['assignPicked']({ id: 'm-1', fullName: 'Mia Member', email: 'mia@example.com' });

    backend.expectNone('/api/v1/projects/p-1/members');
    backend.expectOne('/api/v1/tasks/t-1/assignee').flush(ok({ ...task }));
  });
});
