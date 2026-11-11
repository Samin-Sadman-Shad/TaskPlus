import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNativeDateAdapter } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { makeSession, makeUser, ok } from '../testing/test-data';
import { routes } from './app.routes';
import { AuthService } from './core/auth/auth.service';

/** The guest route and the shell share the '' path; these checks pin the fall-through behaviour. */
describe('app routes', () => {
  let router: Router;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting(), provideNativeDateAdapter()],
    });
    router = TestBed.inject(Router);
    backend = TestBed.inject(HttpTestingController);
  });

  function signIn(role: 'Admin' | 'TeamMember', mustChangePassword = false) {
    TestBed.inject(AuthService).login({ email: 'a@b.c', password: 'x' }).subscribe();
    backend.expectOne('/api/v1/auth/login').flush(ok(makeSession('t', makeUser({ role, mustChangePassword }))));
  }

  it('sends a guest from a protected page to /login with a return URL', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/projects');
    expect(router.url).toBe('/login?returnUrl=%2Fprojects');
  });

  it('shows the register page to a guest', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/register');
    expect(router.url).toBe('/register');
  });

  it('sends a signed-in user from /login to the dashboard', async () => {
    signIn('TeamMember');
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/login');
    expect(router.url).toBe('/dashboard');
  });

  it('keeps a TeamMember out of /users', async () => {
    signIn('TeamMember');
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/users');
    expect(router.url).toBe('/forbidden');
  });

  it('forces the change-password page while a temporary password is active', async () => {
    signIn('Admin', true);
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/projects');
    expect(router.url).toBe('/account/change-password');
  });
});
