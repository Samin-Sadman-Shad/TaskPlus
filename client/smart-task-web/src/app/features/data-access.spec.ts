import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ok } from '../../testing/test-data';
import { UserLookupApi } from '../core/users/user-lookup.api';
import { DashboardApi } from './dashboard/data-access/dashboard.api';
import { ProjectsApi } from './projects/data-access/projects.api';
import { AiApi, TasksApi } from './tasks/data-access/tasks.api';
import { UsersApi } from './users/data-access/users.api';

/** Data-access services: URLs, methods, query params and BaseResponse unwrapping. */
describe('data-access services', () => {
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  const paged = { items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false };

  describe('ProjectsApi', () => {
    it('lists with paging, search, date filters and sort; empty values are dropped', () => {
      let result: unknown;
      TestBed.inject(ProjectsApi)
        .list({ pageNumber: 2, pageSize: 25, search: 'web', startDateFrom: '2026-01-01', sortBy: 'name', sortDirection: 'Desc', startDateTo: undefined })
        .subscribe((r) => (result = r));

      const req = backend.expectOne((r) => r.url === '/api/v1/projects');
      expect(req.request.method).toBe('GET');
      expect(req.request.params.toString()).toBe(
        'pageNumber=2&pageSize=25&search=web&startDateFrom=2026-01-01&sortBy=name&sortDirection=Desc',
      );
      req.flush(ok(paged));
      expect(result).toEqual(paged);
    });

    it('uses the member endpoints', () => {
      const api = TestBed.inject(ProjectsApi);
      api.addMember('p-1', 'u-2').subscribe();
      const add = backend.expectOne('/api/v1/projects/p-1/members');
      expect(add.request.method).toBe('POST');
      expect(add.request.body).toEqual({ userId: 'u-2' });
      add.flush(null, { status: 204, statusText: 'No Content' });

      api.removeMember('p-1', 'u-2').subscribe();
      const remove = backend.expectOne('/api/v1/projects/p-1/members/u-2');
      expect(remove.request.method).toBe('DELETE');
      remove.flush(null, { status: 204, statusText: 'No Content' });
    });
  });

  describe('TasksApi', () => {
    it('lists all visible tasks or one project\'s tasks with filters', () => {
      const api = TestBed.inject(TasksApi);
      api.list({ status: 'InProgress', isOverdue: true, assigneeId: 'u-1' }).subscribe();
      const all = backend.expectOne((r) => r.url === '/api/v1/tasks');
      expect(all.request.params.toString()).toBe('status=InProgress&isOverdue=true&assigneeId=u-1');
      all.flush(ok(paged));

      api.listByProject('p-1', { priority: 'High', dueDateFrom: '2026-01-01', dueDateTo: '2026-02-01' }).subscribe();
      const byProject = backend.expectOne((r) => r.url === '/api/v1/projects/p-1/tasks');
      expect(byProject.request.params.toString()).toBe('priority=High&dueDateFrom=2026-01-01&dueDateTo=2026-02-01');
      byProject.flush(ok(paged));
    });

    it('changes status and assignee with PATCH', () => {
      const api = TestBed.inject(TasksApi);
      api.changeStatus('t-1', 'Completed').subscribe();
      const status = backend.expectOne('/api/v1/tasks/t-1/status');
      expect(status.request.method).toBe('PATCH');
      expect(status.request.body).toEqual({ status: 'Completed' });
      status.flush(ok({}));

      api.assign('t-1', null).subscribe();
      const assign = backend.expectOne('/api/v1/tasks/t-1/assignee');
      expect(assign.request.body).toEqual({ assigneeId: null });
      assign.flush(ok({}));
    });

    it('calls the AI endpoint', () => {
      TestBed.inject(AiApi).improveDescription({ title: null, description: 'fix it' }).subscribe();
      const req = backend.expectOne('/api/v1/ai/task-descriptions/improve');
      expect(req.request.method).toBe('POST');
      req.flush(ok({ originalDescription: 'fix it', improvedDescription: 'Fix it.', suggestions: [] }));
    });
  });

  describe('UsersApi and UserLookupApi', () => {
    it('lists users with role and active filters', () => {
      TestBed.inject(UsersApi).list({ role: 'Admin', isActive: false, pageNumber: 1 }).subscribe();
      const req = backend.expectOne((r) => r.url === '/api/v1/users');
      expect(req.request.params.toString()).toBe('role=Admin&isActive=false&pageNumber=1');
      req.flush(ok(paged));
    });

    it('changes roles and toggles activation', () => {
      const api = TestBed.inject(UsersApi);
      api.changeRole('u-1', 'ProjectManager').subscribe();
      const role = backend.expectOne('/api/v1/users/u-1/role');
      expect(role.request.method).toBe('PATCH');
      expect(role.request.body).toEqual({ role: 'ProjectManager' });
      role.flush(ok({}));

      api.deactivate('u-1').subscribe();
      backend.expectOne('/api/v1/users/u-1/deactivate').flush(null, { status: 204, statusText: 'No Content' });
      api.activate('u-1').subscribe();
      backend.expectOne('/api/v1/users/u-1/activate').flush(null, { status: 204, statusText: 'No Content' });
    });

    it('looks users up by term', () => {
      TestBed.inject(UserLookupApi).search('gra').subscribe();
      backend.expectOne('/api/v1/users/lookup?search=gra').flush(ok([]));
    });
  });

  describe('DashboardApi', () => {
    it('sends upcomingDays', () => {
      TestBed.inject(DashboardApi).get(14).subscribe();
      backend.expectOne('/api/v1/dashboard?upcomingDays=14').flush(ok({}));
    });
  });
});
