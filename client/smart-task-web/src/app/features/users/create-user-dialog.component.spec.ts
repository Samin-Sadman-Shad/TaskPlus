import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { MatDialogRef } from '@angular/material/dialog';
import { fail, makeUser, ok } from '../../../testing/test-data';
import { CreateUserDialogComponent } from './create-user-dialog.component';

describe('CreateUserDialogComponent', () => {
  let backend: HttpTestingController;
  const dialogRef = { close: vi.fn() };

  async function setup() {
    TestBed.configureTestingModule({
      imports: [CreateUserDialogComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: MatDialogRef, useValue: dialogRef }],
    });
    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(CreateUserDialogComponent);
    await fixture.whenStable();
    return { component: fixture.componentInstance, form: fixture.componentInstance['form'] as FormGroup };
  }

  afterEach(() => {
    backend.verify();
    dialogRef.close.mockReset();
  });

  const valid = {
    firstName: 'Grace',
    lastName: 'Hopper',
    email: 'grace@example.com',
    role: 'ProjectManager',
    password: 'Temp#2026x',
    confirmPassword: 'Temp#2026x',
  };

  it('applies the backend password rules', async () => {
    const { form } = await setup();
    form.patchValue({ ...valid, password: 'short', confirmPassword: 'short' });
    expect(form.get('password')?.hasError('passwordLength')).toBe(true);

    form.patchValue({ password: 'alllowercase1!', confirmPassword: 'alllowercase1!' });
    expect(form.get('password')?.hasError('passwordUpper')).toBe(true);

    form.patchValue({ password: 'Grace#2026x', confirmPassword: 'Grace#2026x' });
    expect(form.get('password')?.hasError('passwordContainsEmail')).toBe(true);

    form.patchValue({ password: valid.password, confirmPassword: 'Different#1' });
    expect(form.get('confirmPassword')?.hasError('mismatch')).toBe(true);

    form.patchValue({ confirmPassword: valid.password });
    expect(form.valid).toBe(true);
  });

  it('creates the user with the chosen role', async () => {
    const { component, form } = await setup();
    form.setValue(valid);
    component['save']();

    const req = backend.expectOne('/api/v1/users');
    expect(req.request.body).toEqual(valid);
    const created = makeUser({ id: 'u-9', role: 'ProjectManager', mustChangePassword: true });
    req.flush(ok(created, 201), { status: 201, statusText: 'Created' });

    expect(dialogRef.close).toHaveBeenCalledWith(created);
  });

  it('maps server field errors onto the form', async () => {
    const { component, form } = await setup();
    form.setValue(valid);
    component['save']();
    backend
      .expectOne('/api/v1/users')
      .flush(fail(400, 'Validation failed.', { errors: { email: ['Email must be a valid email address.'] } }), {
        status: 400,
        statusText: 'Bad Request',
      });

    expect(form.get('email')?.getError('server')).toBe('Email must be a valid email address.');
  });
});
