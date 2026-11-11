import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerInputEvent, MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router } from '@angular/router';
import { debounceTime, distinctUntilChanged, filter, map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { BreakpointService } from '../../core/layout/breakpoint.service';
import { PagingConstraints } from '../../core/models/constraints';
import { NotificationService } from '../../core/notifications/notification.service';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { fromDateOnly, toDateOnly } from '../../shared/utils/date-only';
import { injectUrlQueryState, readInt, readOneOf, readString } from '../../shared/utils/url-query-state';
import { ProjectsApi } from './data-access/projects.api';
import { PROJECT_SORT_FIELDS, ProjectDto, ProjectQueryParameters, ProjectSummaryDto } from './data-access/projects.models';
import { openProjectForm } from './project-form-dialog.component';

@Component({
  selector: 'app-project-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatDatepickerModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    EmptyStateComponent,
  ],
  template: `
    <app-page-header title="Projects" [subtitle]="subtitle()">
      @if (canCreate()) {
        <button mat-flat-button type="button" (click)="create()"><mat-icon>add</mat-icon> New project</button>
      }
    </app-page-header>

    <div class="filters" role="search">
      <mat-form-field class="search" subscriptSizing="dynamic">
        <mat-label>Search projects</mat-label>
        <mat-icon matIconPrefix aria-hidden="true">search</mat-icon>
        <input matInput type="search" [formControl]="search" [maxlength]="searchMax" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Starts from</mat-label>
        <input matInput [matDatepicker]="fromPicker" [value]="startFrom()" (dateChange)="patchDate('from', $event)" />
        <mat-datepicker-toggle matIconSuffix [for]="fromPicker" aria-label="Open starts from calendar" />
        <mat-datepicker #fromPicker />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Starts until</mat-label>
        <input matInput [matDatepicker]="toPicker" [value]="startTo()" [min]="startFrom()" (dateChange)="patchDate('to', $event)" />
        <mat-datepicker-toggle matIconSuffix [for]="toPicker" aria-label="Open starts until calendar" />
        <mat-datepicker #toPicker />
      </mat-form-field>
      @if (hasFilters()) {
        <button mat-button type="button" (click)="clearFilters()">Clear filters</button>
      }
    </div>

    @if (projects.hasValue()) {
      @let page = projects.value();
      @if (page.items.length === 0) {
        <app-empty-state
          icon="folder_open"
          title="No projects found"
          [message]="hasFilters() ? 'Try other filters.' : canCreate() ? 'Create your first project.' : 'You are not a member of any project yet.'"
        />
      } @else if (isSmall()) {
        <div class="card-list">
          @for (p of page.items; track p.id) {
            <a class="item-card" href="" (click)="$event.preventDefault(); open(p)">
              <div class="row">
                <span class="title">{{ p.name }}</span>
                <span class="muted">{{ p.completedTaskCount }}/{{ p.taskCount }} done</span>
              </div>
              <div class="meta">
                {{ p.startDate | date: 'mediumDate' }} – {{ p.endDate ? (p.endDate | date: 'mediumDate') : 'open' }}
                · {{ p.ownerName }} · {{ p.memberCount }} members
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
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th>
              <td mat-cell *matCellDef="let p">{{ p.name }}</td>
            </ng-container>
            <ng-container matColumnDef="owner">
              <th mat-header-cell *matHeaderCellDef>Owner</th>
              <td mat-cell *matCellDef="let p">{{ p.ownerName }}</td>
            </ng-container>
            <ng-container matColumnDef="startDate">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Start</th>
              <td mat-cell *matCellDef="let p">{{ p.startDate | date: 'mediumDate' }}</td>
            </ng-container>
            <ng-container matColumnDef="endDate">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>End</th>
              <td mat-cell *matCellDef="let p">{{ p.endDate ? (p.endDate | date: 'mediumDate') : '—' }}</td>
            </ng-container>
            <ng-container matColumnDef="tasks">
              <th mat-header-cell *matHeaderCellDef>Tasks done</th>
              <td mat-cell *matCellDef="let p">{{ p.completedTaskCount }} / {{ p.taskCount }}</td>
            </ng-container>
            <ng-container matColumnDef="members">
              <th mat-header-cell *matHeaderCellDef>Members</th>
              <td mat-cell *matCellDef="let p">{{ p.memberCount }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr
              mat-row
              *matRowDef="let row; columns: columns"
              class="clickable"
              tabindex="0"
              [attr.aria-label]="'Open project ' + row.name"
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
        aria-label="Project pages"
      />
    } @else if (projects.isLoading()) {
      <div class="loading"><mat-spinner diameter="36" aria-label="Loading projects" /></div>
    } @else if (projects.error()) {
      <app-empty-state icon="error_outline" title="Could not load projects" message="Check the filters or try again." />
    }
  `,
  styles: `
    .loading { display: grid; place-items: center; padding: 40px; }
    mat-paginator { background: transparent; }
  `,
})
export default class ProjectListComponent {
  private readonly api = inject(ProjectsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly url = injectUrlQueryState();
  protected readonly isSmall = inject(BreakpointService).isSmall;

  protected readonly pageSizes = [...PagingConstraints.pageSizeOptions];
  protected readonly searchMax = PagingConstraints.searchTermMaxLength;
  protected readonly columns = ['name', 'owner', 'startDate', 'endDate', 'tasks', 'members'];

  /** Admin and ProjectManager create projects; TeamMember doesn't see the button. */
  protected readonly canCreate = computed(() => this.auth.hasRole('Admin', 'ProjectManager'));
  protected readonly subtitle = computed(() =>
    this.auth.hasRole('Admin') ? 'All projects' : 'Projects you own or belong to',
  );

  protected readonly query = computed<ProjectQueryParameters>(() => {
    const p = this.url.params();
    return {
      search: readString(p, 'q'),
      startDateFrom: readString(p, 'from'),
      startDateTo: readString(p, 'to'),
      sortBy: readOneOf(p, 'sort', PROJECT_SORT_FIELDS),
      sortDirection: readOneOf(p, 'dir', ['Asc', 'Desc'] as const),
      pageNumber: readInt(p, 'page', 1),
      pageSize: Math.min(readInt(p, 'size', PagingConstraints.defaultPageSize), PagingConstraints.maxPageSize),
    };
  });

  protected readonly startFrom = computed(() => fromDateOnly(this.query().startDateFrom));
  protected readonly startTo = computed(() => fromDateOnly(this.query().startDateTo));
  protected readonly hasFilters = computed(() => {
    const q = this.query();
    return !!(q.search || q.startDateFrom || q.startDateTo);
  });

  protected readonly projects = rxResource({
    params: () => this.query(),
    stream: ({ params }) => this.api.list(params),
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
      .subscribe((q) => this.url.patch({ q, page: null }));
  }

  protected patchDate(key: 'from' | 'to', event: MatDatepickerInputEvent<Date>): void {
    this.url.patch({ [key]: toDateOnly(event.value), page: null });
  }

  protected clearFilters(): void {
    this.search.setValue('', { emitEvent: false });
    this.url.patch({ q: null, from: null, to: null, page: null });
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

  protected open(project: ProjectSummaryDto): void {
    void this.router.navigate(['/projects', project.id]);
  }

  protected create(): void {
    openProjectForm(this.dialog, {})
      .pipe(filter((p): p is ProjectDto => !!p))
      .subscribe((project) => {
        this.notifications.success('Project created.');
        void this.router.navigate(['/projects', project.id]);
      });
  }
}
