import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { Router, provideRouter } from '@angular/router';
import { fail, makeSession, makeUser, ok } from '../../../testing/test-data';
import LoginComponent, { safeReturnUrl } from './login.component';

describe('LoginComponent', () => {
  let backend: HttpTestingController;

  async function setup() {
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(LoginComponent);
    await fixture.whenStable();
    const component = fixture.componentInstance;
    const form = component['form'] as FormGroup;
    return { fixture, component, form };
  }

  afterEach(() => backend.verify());

  it('does not call the API while the form is invalid', async () => {
    const { component, form } = await setup();
    form.setValue({ email: 'not-an-email', password: '' });
    component['submit']();

    backend.expectNone('/api/v1/auth/login');
    expect(form.get('email')?.hasError('email')).toBe(true);
    expect(form.get('password')?.hasError('required')).toBe(true);
  });

  it('shows one generic message on 401', async () => {
    const { fixture, component, form } = await setup();
    form.setValue({ email: 'ada@example.com', password: 'wrong' });
    component['submit']();
    backend.expectOne('/api/v1/auth/login').flush(fail(401, 'Invalid email or password.'), { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('Invalid email or password.');
  });

  it('maps 400 field errors onto the matching controls', async () => {
    const { component, form } = await setup();
    form.setValue({ email: 'ada@example.com', password: 'x' });
    component['submit']();
    backend
      .expectOne('/api/v1/auth/login')
      .flush(fail(400, 'Validation failed.', { errors: { email: ['Email must be a valid email address.'] } }), {
        status: 400,
        statusText: 'Bad Request',
      });

    expect(form.get('email')?.getError('server')).toBe('Email must be a valid email address.');
  });

  it('goes to the return URL after signing in', async () => {
    const { fixture, component, form } = await setup();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture.componentRef.setInput('returnUrl', '/projects/42');
    form.setValue({ email: 'ada@example.com', password: 'Secret#123' });
    component['submit']();
    backend.expectOne('/api/v1/auth/login').flush(ok(makeSession('t', makeUser())));

    expect(navigate).toHaveBeenCalledWith('/projects/42');
  });

  it('only accepts same-app return URLs', () => {
    expect(safeReturnUrl('/tasks/1')).toBe('/tasks/1');
    expect(safeReturnUrl('//evil.example')).toBe('/dashboard');
    expect(safeReturnUrl('https://evil.example')).toBe('/dashboard');
    expect(safeReturnUrl(undefined)).toBe('/dashboard');
  });
});
