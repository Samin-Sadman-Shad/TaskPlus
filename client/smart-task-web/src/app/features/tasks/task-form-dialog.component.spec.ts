import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { fail, ok } from '../../../testing/test-data';
import { today } from '../../shared/utils/date-only';
import { ImproveDescriptionResponseDto } from './data-access/tasks.models';
import { TaskFormDialogComponent, TaskFormDialogData } from './task-form-dialog.component';

describe('TaskFormDialogComponent', () => {
  let backend: HttpTestingController;
  const dialogRef = { close: vi.fn() };

  async function setup(data: TaskFormDialogData = { projectId: 'p-1', assignees: [] }) {
    TestBed.configureTestingModule({
      imports: [TaskFormDialogComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNativeDateAdapter(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRef },
      ],
    });
    backend = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(TaskFormDialogComponent);
    await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, form: fixture.componentInstance['form'] as FormGroup };
  }

  afterEach(() => {
    backend.verify();
    dialogRef.close.mockReset();
  });

  it('requires a title of 3+ characters and a due date not in the past', async () => {
    const { form } = await setup();
    form.patchValue({ title: 'ab', dueDate: today(-1) });
    expect(form.get('title')?.hasError('minlength')).toBe(true);
    expect(form.get('dueDate')?.hasError('minDate')).toBe(true);
  });

  it('shows the AI suggestion side by side and only applies it on Accept', async () => {
    const { fixture, component, form } = await setup();
    form.patchValue({ title: 'Login page', description: 'make login work' });
    component['improve']();

    const req = backend.expectOne('/api/v1/ai/task-descriptions/improve');
    expect(req.request.body).toEqual({ title: 'Login page', description: 'make login work' });
    const suggestion: ImproveDescriptionResponseDto = {
      originalDescription: 'make login work',
      improvedDescription: 'Implement the login page with email and password.',
      suggestions: ['Add acceptance criteria'],
    };
    req.flush(ok(suggestion));
    await fixture.whenStable();

    const review: HTMLElement = fixture.nativeElement.querySelector('.ai-review');
    expect(review.textContent).toContain('make login work');
    expect(review.textContent).toContain('Implement the login page');
    expect(form.get('description')?.value).toBe('make login work');

    component['acceptSuggestion'](suggestion);
    expect(form.get('description')?.value).toBe('Implement the login page with email and password.');
  });

  it('keeps the original description on Discard and shows AI errors inline', async () => {
    const { fixture, component, form } = await setup();
    form.patchValue({ title: 'Login', description: 'make login work' });
    component['improve']();
    backend
      .expectOne('/api/v1/ai/task-descriptions/improve')
      .flush(fail(503, 'The AI provider is not configured.'), { status: 503, statusText: 'Service Unavailable' });
    await fixture.whenStable();

    expect(component['aiError']()).toBe('The AI provider is not configured.');
    component['discardSuggestion']();
    expect(form.get('description')?.value).toBe('make login work');
  });

  it('creates the task in the project and maps server field errors', async () => {
    const { component, form } = await setup();
    form.patchValue({ title: 'Write docs', priority: 'High', dueDate: new Date(2030, 4, 1), assigneeId: 'u-2' });
    component['save']();

    const req = backend.expectOne('/api/v1/projects/p-1/tasks');
    expect(req.request.body).toEqual({
      title: 'Write docs',
      description: null,
      priority: 'High',
      dueDate: '2030-05-01',
      assigneeId: 'u-2',
    });
    req.flush(fail(400, 'Validation failed.', { errors: { assigneeId: ['The assignee must be a project member.'] } }), {
      status: 400,
      statusText: 'Bad Request',
    });

    expect(form.get('assigneeId')?.getError('server')).toBe('The assignee must be a project member.');
    expect(dialogRef.close).not.toHaveBeenCalled();
  });
});
