import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerInputEvent, MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, map } from 'rxjs';
import { BreakpointService } from '../../core/layout/breakpoint.service';
import { PagingConstraints } from '../../core/models/constraints';
import { TASK_PRIORITIES, TASK_STATUSES } from '../../core/models/enums';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { PriorityChipComponent } from '../../shared/components/priority-chip.component';
import { StatusChipComponent } from '../../shared/components/status-chip.component';
import { UserOption } from '../../shared/components/user-picker.component';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { fromDateOnly, toDateOnly } from '../../shared/utils/date-only';
import { injectUrlQueryState, readInt, readOneOf, readString } from '../../shared/utils/url-query-state';
import { TasksApi } from './data-access/tasks.api';
import { TASK_SORT_FIELDS, TaskQueryParameters, TaskSummaryDto } from './data-access/tasks.models';

/**
 * Server-side paged task list. Used by the Tasks page (all visible tasks) and by the project
 * detail page (one project's tasks). Filters, sort and page live in the URL query string.
 */
@Component({
  selector: 'app-task-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    StatusChipComponent,
    PriorityChipComponent,
    EmptyStateComponent,
    EnumLabelPipe,
  ],
  template: `
    <div class="filters" role="search">
      <mat-form-field class="search" subscriptSizing="dynamic">
        <mat-label>Search tasks</mat-label>
        <mat-icon matIconPrefix aria-hidden="true">search</mat-icon>
        <input matInput type="search" [formControl]="search" [maxlength]="searchMax" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select [value]="query().status ?? null" (selectionChange)="patch({ status: $event.value })">
          <mat-option [value]="null">Any status</mat-option>
          @for (s of statuses; track s) {
            <mat-option [value]="s">{{ s | enumLabel }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Priority</mat-label>
        <mat-select [value]="query().priority ?? null" (selectionChange)="patch({ priority: $event.value })">
          <mat-option [value]="null">Any priority</mat-option>
          @for (p of priorities; track p) {
            <mat-option [value]="p">{{ p }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      @if (assignees().length > 0) {
        <mat-form-field subscriptSizing="dynamic">
          <mat-label>Assignee</mat-label>
          <mat-select [value]="query().assigneeId ?? null" (selectionChange)="patch({ assignee: $event.value })">
            <mat-option [value]="null">Anyone</mat-option>
            @for (u of assignees(); track u.id) {
              <mat-option [value]="u.id">{{ u.fullName }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      }
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Due from</mat-label>
        <input matInput [matDatepicker]="fromPicker" [value]="dueFrom()" (dateChange)="patchDate('from', $event)" />
        <mat-datepicker-toggle matIconSuffix [for]="fromPicker" aria-label="Open due from calendar" />
        <mat-datepicker #fromPicker />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Due to</mat-label>
        <input matInput [matDatepicker]="toPicker" [value]="dueTo()" [min]="dueFrom()" (dateChange)="patchDate('to', $event)" />
        <mat-datepicker-toggle matIconSuffix [for]="toPicker" aria-label="Open due to calendar" />
        <mat-datepicker #toPicker />
      </mat-form-field>
      <mat-slide-toggle [checked]="query().isOverdue === true" (change)="patch({ overdue: $event.checked || null })">
        Overdue only
      </mat-slide-toggle>
      @if (hasFilters()) {
        <button mat-button type="button" (click)="clearFilters()">Clear filters</button>
      }
    </div>

    @if (tasks.hasValue()) {
      @let page = tasks.value();
      @if (page.items.length === 0) {
        <app-empty-state icon="task_alt" title="No tasks found" [message]="hasFilters() ? 'Try other filters.' : 'There are no tasks here yet.'" />
      } @else if (isSmall()) {
        <div class="card-list">
          @for (task of page.items; track task.id) {
            <a class="item-card" href="" (click)="$event.preventDefault(); open(task)">
              <div class="row">
                <span class="title">{{ task.title }}</span>
                <app-status-chip [status]="task.status" />
              </div>
              <div class="row meta">
                <app-priority-chip [priority]="task.priority" />
                <span [class.overdue]="task.isOverdue">{{ task.dueDate ? (task.dueDate | date: 'mediumDate') : 'No due date' }}</span>
                <span>{{ task.assigneeName ?? 'Unassigned' }}</span>
              </div>
            </a>
          }
        </div>
      } @else {
        <div class="table-wrap">
          <table
            mat-table
            [dataSource]="page.items"
            matSort
            [matSortActive]="query().sortBy ?? ''"
            [matSortDirection]="query().sortDirection === 'Desc' ? 'desc' : query().sortBy ? 'asc' : ''"
            (matSortChange)="sort($event)"
          >
            <ng-container matColumnDef="title">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Title</th>
              <td mat-cell *matCellDef="let t">{{ t.title }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
              <td mat-cell *matCellDef="let t"><app-status-chip [status]="t.status" /></td>
            </ng-container>
            <ng-container matColumnDef="priority">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Priority</th>
              <td mat-cell *matCellDef="let t"><app-priority-chip [priority]="t.priority" /></td>
            </ng-container>
            <ng-container matColumnDef="dueDate">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Due</th>
              <td mat-cell *matCellDef="let t" [class.overdue]="t.isOverdue">
                {{ t.dueDate ? (t.dueDate | date: 'mediumDate') : '—' }}
                @if (t.isOverdue) {
                  <span class="cdk-visually-hidden">(overdue)</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="assignee">
              <th mat-header-cell *matHeaderCellDef>Assignee</th>
              <td mat-cell *matCellDef="let t">{{ t.assigneeName ?? 'Unassigned' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr
              mat-row
              *matRowDef="let row; columns: columns"
              class="clickable"
              tabindex="0"
              [attr.aria-label]="'Open task ' + row.title"
              (click)="open(row)"
              (keydown.enter)="open(row)"
            ></tr>
          </table>
        </div>
      }
      <mat-paginator
        [length]="page.totalCount"
        [pageIndex]="page.pageNumber - 1"
        [pageSize]="page.pageSize"
        [pageSizeOptions]="pageSizes"
        (page)="changePage($event)"
        aria-label="Task pages"
      />
    } @else if (tasks.isLoading()) {
      <div class="loading"><mat-spinner diameter="36" aria-label="Loading tasks" /></div>
    } @else if (tasks.error()) {
      <app-empty-state icon="error_outline" title="Could not load tasks" message="Check the filters or try again." />
    }
  `,
  styles: `
    .loading { display: grid; place-items: center; padding: 40px; }
    mat-paginator { background: transparent; }
  `,
})
export class TaskListComponent {
  private readonly api = inject(TasksApi);
  private readonly router = inject(Router);
  private readonly url = injectUrlQueryState();
  protected readonly isSmall = inject(BreakpointService).isSmall;

  /** Set to list one project's tasks; otherwise every task the user can see. */
  readonly projectId = input<string>();
  /** Options for the assignee filter (project members, or just "Me" on the global list). */
  readonly assignees = input<readonly UserOption[]>([]);

  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;
  protected readonly pageSizes = [...PagingConstraints.pageSizeOptions];
  protected readonly searchMax = PagingConstraints.searchTermMaxLength;
  protected readonly columns = ['title', 'status', 'priority', 'dueDate', 'assignee'];

  /** URL → API query. Unknown values are dropped (whitelisted sort fields and enums). */
  protected readonly query = computed<TaskQueryParameters>(() => {
    const p = this.url.params();
    return {
      search: readString(p, 'q'),
      status: readOneOf(p, 'status', TASK_STATUSES),
      priority: readOneOf(p, 'priority', TASK_PRIORITIES),
      assigneeId: readString(p, 'assignee'),
      dueDateFrom: readString(p, 'from'),
      dueDateTo: readString(p, 'to'),
      isOverdue: p.get('overdue') === 'true' ? true : undefined,
      sortBy: readOneOf(p, 'sort', TASK_SORT_FIELDS),
      sortDirection: readOneOf(p, 'dir', ['Asc', 'Desc'] as const),
      pageNumber: readInt(p, 'page', 1),
      pageSize: Math.min(readInt(p, 'size', PagingConstraints.defaultPageSize), PagingConstraints.maxPageSize),
    };
  });

  protected readonly dueFrom = computed(() => fromDateOnly(this.query().dueDateFrom));
  protected readonly dueTo = computed(() => fromDateOnly(this.query().dueDateTo));
  protected readonly hasFilters = computed(() => {
    const q = this.query();
    return !!(q.search || q.status || q.priority || q.assigneeId || q.dueDateFrom || q.dueDateTo || q.isOverdue);
  });

  protected readonly tasks = rxResource({
    params: () => ({ projectId: this.projectId(), query: this.query() }),
    stream: ({ params }) =>
      params.projectId ? this.api.listByProject(params.projectId, params.query) : this.api.list(params.query),
  });

  protected readonly search = new FormControl(this.query().search ?? '', { nonNullable: true });

  constructor() {
    this.search.valueChanges
      .pipe(
        map((v) => v.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntilDestroyed(),
      )
      .subscribe((q) => this.patch({ q }));
  }

  /** Reloads the current page, e.g. after a task was created. */
  reload(): void {
    this.tasks.reload();
  }

  /** Any filter change goes back to page 1. */
  protected patch(patch: Record<string, string | boolean | null>): void {
    this.url.patch({ ...patch, page: null });
  }

  protected patchDate(key: 'from' | 'to', event: MatDatepickerInputEvent<Date>): void {
    this.patch({ [key]: toDateOnly(event.value) });
  }

  protected clearFilters(): void {
    this.search.setValue('', { emitEvent: false });
    this.patch({ q: null, status: null, priority: null, assignee: null, from: null, to: null, overdue: null });
  }

  protected sort(sort: Sort): void {
    this.url.patch({
      sort: sort.direction ? sort.active : null,
      dir: sort.direction === 'desc' ? 'Desc' : sort.direction === 'asc' ? 'Asc' : null,
      page: null,
    });
  }

  protected changePage(event: PageEvent): void {
    this.url.patch({
      page: event.pageIndex > 0 ? event.pageIndex + 1 : null,
      size: event.pageSize !== PagingConstraints.defaultPageSize ? event.pageSize : null,
    });
  }

  protected open(task: TaskSummaryDto): void {
    void this.router.navigate(['/tasks', task.id]);
  }
}
