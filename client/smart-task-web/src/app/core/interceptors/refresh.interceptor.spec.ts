import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { fail, makeSession, ok } from '../../../testing/test-data';
import { AuthService } from '../auth/auth.service';
import { TokenStorage } from '../auth/token-storage';
import { authInterceptor } from './auth.interceptor';
import { refreshInterceptor } from './refresh.interceptor';

describe('refreshInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor, refreshInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    TestBed.inject(TokenStorage).set('expired-token');
  });

  afterEach(() => backend.verify());

  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  it('runs one refresh for parallel 401s and retries each request once with the new token', () => {
    const results: unknown[] = [];
    http.get('/api/v1/projects').subscribe((r) => results.push(r));
    http.get('/api/v1/tasks').subscribe((r) => results.push(r));

    const first = backend.expectOne('/api/v1/projects');
    const second = backend.expectOne('/api/v1/tasks');
    expect(first.request.headers.get('Authorization')).toBe('Bearer expired-token');

    first.flush(fail(401, 'Token expired', { errorCode: 'TOKEN_EXPIRED' }), unauthorized);
    second.flush(fail(401, 'Token expired', { errorCode: 'TOKEN_EXPIRED' }), unauthorized);

    const refresh = backend.match('/api/v1/auth/refresh');
    expect(refresh).toHaveLength(1);
    expect(refresh[0].request.withCredentials).toBe(true);
    expect(refresh[0].request.headers.has('Authorization')).toBe(false);
    refresh[0].flush(ok(makeSession('fresh-token')));

    const retryProjects = backend.expectOne('/api/v1/projects');
    const retryTasks = backend.expectOne('/api/v1/tasks');
    expect(retryProjects.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    expect(retryTasks.request.headers.get('Authorization')).toBe('Bearer fresh-token');
    retryProjects.flush(ok('projects'));
    retryTasks.flush(ok('tasks'));

    expect(results).toHaveLength(2);
  });

  it('does not refresh again when the retried request also returns 401', () => {
    let error: HttpErrorResponse | undefined;
    http.get('/api/v1/projects').subscribe({ error: (e: HttpErrorResponse) => (error = e) });

    backend.expectOne('/api/v1/projects').flush(fail(401, 'expired'), unauthorized);
    backend.expectOne('/api/v1/auth/refresh').flush(ok(makeSession('fresh-token')));
    backend.expectOne('/api/v1/projects').flush(fail(401, 'still no'), unauthorized);

    backend.expectNone('/api/v1/auth/refresh');
    expect(error?.status).toBe(401);
  });

  it('signs out and passes the original error on when the refresh fails', () => {
    const endSession = vi.spyOn(auth, 'endExpiredSession').mockImplementation(() => undefined);
    let error: HttpErrorResponse | undefined;
    http.get('/api/v1/projects').subscribe({ error: (e: HttpErrorResponse) => (error = e) });

    backend.expectOne('/api/v1/projects').flush(fail(401, 'expired'), unauthorized);
    backend.expectOne('/api/v1/auth/refresh').flush(fail(401, 'Refresh token invalid'), unauthorized);

    expect(endSession).toHaveBeenCalledTimes(1);
    expect(error?.status).toBe(401);
    expect(error?.url).toContain('/api/v1/projects');
  });

  it('never refreshes for a 401 from an auth endpoint', () => {
    http.post('/api/v1/auth/login', {}).subscribe({ error: () => undefined });
    backend.expectOne('/api/v1/auth/login').flush(fail(401, 'Invalid credentials'), unauthorized);
    backend.expectNone('/api/v1/auth/refresh');
  });
});
