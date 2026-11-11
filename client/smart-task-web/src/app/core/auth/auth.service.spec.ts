import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { fail, makeSession, makeUser, ok } from '../../../testing/test-data';
import { AuthService } from './auth.service';
import { TokenStorage } from './token-storage';

describe('AuthService', () => {
  let service: AuthService;
  let backend: HttpTestingController;
  let tokens: TokenStorage;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    backend = TestBed.inject(HttpTestingController);
    tokens = TestBed.inject(TokenStorage);
  });

  afterEach(() => backend.verify());

  it('restores the session from the refresh cookie on start-up', async () => {
    const user = makeUser({ role: 'ProjectManager' });
    const restored = service.tryRestoreSession();

    const req = backend.expectOne('/api/v1/auth/refresh');
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush(ok(makeSession('restored-token', user)));
    await restored;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()?.id).toBe(user.id);
    expect(service.role()).toBe('ProjectManager');
    expect(service.hasRole('Admin', 'ProjectManager')).toBe(true);
    expect(tokens.get()).toBe('restored-token');
  });

  it('silently stays signed out when there is no valid refresh cookie', async () => {
    const restored = service.tryRestoreSession();
    backend.expectOne('/api/v1/auth/refresh').flush(fail(401, 'No session'), { status: 401, statusText: 'Unauthorized' });

    await expect(restored).resolves.toBeUndefined();
    expect(service.isAuthenticated()).toBe(false);
    expect(tokens.get()).toBeNull();
  });

  it('keeps the access token in memory only', () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem');
    service.login({ email: 'ada@example.com', password: 'x' }).subscribe();
    backend.expectOne('/api/v1/auth/login').flush(ok(makeSession('memory-token')));

    expect(tokens.get()).toBe('memory-token');
    expect(setItem).not.toHaveBeenCalled();
  });

  it('stores the new access token after a password change', () => {
    service.changePassword({ currentPassword: 'a', newPassword: 'b', confirmNewPassword: 'b' }).subscribe();
    const req = backend.expectOne('/api/v1/users/me/password');
    req.flush(ok(makeSession('after-change', makeUser({ mustChangePassword: false }))));

    expect(tokens.get()).toBe('after-change');
    expect(service.mustChangePassword()).toBe(false);
  });
});
