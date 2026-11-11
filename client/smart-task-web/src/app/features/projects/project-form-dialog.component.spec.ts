import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { fail, ok } from '../../../testing/test-data';
import { today } from '../../shared/utils/date-only';
import { ProjectFormDialogComponent, ProjectFormDialogData } from './project-form-dialog.component';

describe('ProjectFormDialogComponent', () => {
  let backend: HttpTestingController;
  const dialogRef = { close: vi.fn() };

  async function setup(data: ProjectFormDialogData = {}) {
    TestBed.configureTestingModule({
      imports: [ProjectFormDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNativeDateAdapter(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(ProjectFormDialogComponent);
    await fixture.whenStable();
    return { component: fixture.componentInstance, form: fixture.componentInstance['form'] as FormGroup };
  }

  afterEach(() => {
    backend.verify();
    dialogRef.close.mockReset();
  });

  it('mirrors the server rules: name length and end date on or after start date', async () => {
    const { form } = await setup();
    form.patchValue({ name: 'ab', startDate: today(5), endDate: today(2) });

    expect(form.get('name')?.hasError('minlength')).toBe(true);
    expect(form.get('endDate')?.hasError('dateRange')).toBe(true);

    form.patchValue({ name: 'Website', endDate: today(10) });
    expect(form.valid).toBe(true);
  });

  it('rejects a new project starting more than one day in the past', async () => {
    const { form } = await setup();
    form.patchValue({ name: 'Website', startDate: today(-3) });
    expect(form.get('startDate')?.hasError('minDate')).toBe(true);
  });

  it('posts DateOnly strings and closes with the created project', async () => {
    const { component, form } = await setup();
    form.patchValue({ name: '  Website relaunch ', description: '', startDate: new Date(2030, 0, 5), endDate: null });
    component['save']();

    const req = backend.expectOne('/api/v1/projects');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Website relaunch', description: null, startDate: '2030-01-05', endDate: null });
    req.flush(ok({ id: 'p-1' }, 201), { status: 201, statusText: 'Created' });

    expect(dialogRef.close).toHaveBeenCalledWith({ id: 'p-1' });
  });

  it('maps a 400 onto the fields and keeps the dialog open', async () => {
    const { component, form } = await setup();
    form.patchValue({ name: 'Website', startDate: new Date(2030, 0, 5) });
    component['save']();
    backend
      .expectOne('/api/v1/projects')
      .flush(fail(400, 'Validation failed.', { errors: { name: ['Name is already used.'] } }), {
        status: 400,
        statusText: 'Bad Request',
      });

    expect(form.get('name')?.getError('server')).toBe('Name is already used.');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
