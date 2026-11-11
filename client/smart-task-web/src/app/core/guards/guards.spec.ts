import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  PartialMatchRouteSnapshot,
  Route,
  Router,
  RouterStateSnapshot,
  UrlSegment,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { makeUser } from '../../../testing/test-data';
import { UserDto } from '../auth/auth.models';
import { AuthService } from '../auth/auth.service';
import { UserRole } from '../models/enums';
import { authGuard } from './auth.guard';
import { passwordChangeGuard } from './password-change.guard';
import { roleGuard } from './role.guard';

class FakeAuth {
  readonly user = signal<UserDto | null>(null);
  isAuthenticated = () => this.user() !== null;
  mustChangePassword = () => this.user()?.mustChangePassword === true;
  hasRole = (...roles: UserRole[]) => !!this.user() && roles.includes(this.user()!.role);
}

describe('guards', () => {
  let auth: FakeAuth;
  let router: Router;

  beforeEach(() => {
    auth = new FakeAuth();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: auth }],
    });
    router = TestBed.inject(Router);
  });

  const state = (url: string) => ({ url }) as RouterStateSnapshot;
  const route = {} as ActivatedRouteSnapshot;
  const snapshot = {} as PartialMatchRouteSnapshot;

  describe('authGuard', () => {
    it('allows a signed-in user', () => {
      auth.user.set(makeUser());
      expect(TestBed.runInInjectionContext(() => authGuard(route, state('/projects')))).toBe(true);
    });

    it('redirects a guest to /login with the return URL', () => {
      const result = TestBed.runInInjectionContext(() => authGuard(route, state('/projects?page=2')));
      expect(result).toBeInstanceOf(UrlTree);
      expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Fprojects%3Fpage%3D2');
    });
  });

  describe('roleGuard', () => {
    const adminRoute: Route = { data: { roles: ['Admin'] } };
    const segments = [new UrlSegment('users', {})];

    it('allows a user with a listed role', () => {
      auth.user.set(makeUser({ role: 'Admin' }));
      expect(TestBed.runInInjectionContext(() => roleGuard(adminRoute, segments, snapshot))).toBe(true);
    });

    it('sends other roles to /forbidden', () => {
      auth.user.set(makeUser({ role: 'ProjectManager' }));
      const result = TestBed.runInInjectionContext(() => roleGuard(adminRoute, segments, snapshot));
      expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
    });

    it('sends a guest to /login (canMatch runs before the shell authGuard)', () => {
      const result = TestBed.runInInjectionContext(() => roleGuard(adminRoute, segments, snapshot));
      expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Fusers');
    });

    it('allows any signed-in user when no roles are listed', () => {
      auth.user.set(makeUser());
      expect(TestBed.runInInjectionContext(() => roleGuard({}, segments, snapshot))).toBe(true);
    });
  });

  describe('passwordChangeGuard', () => {
    it('redirects every page to change-password while a temporary password is active', () => {
      auth.user.set(makeUser({ mustChangePassword: true }));
      const result = TestBed.runInInjectionContext(() => passwordChangeGuard(route, state('/dashboard')));
      expect(router.serializeUrl(result as UrlTree)).toBe('/account/change-password');
      expect(TestBed.runInInjectionContext(() => passwordChangeGuard(route, state('/account/change-password')))).toBe(true);
    });

    it('does nothing otherwise', () => {
      auth.user.set(makeUser());
      expect(TestBed.runInInjectionContext(() => passwordChangeGuard(route, state('/dashboard')))).toBe(true);
    });
  });
});
