import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Observable, debounceTime, distinctUntilChanged, filter, map, switchMap } from 'rxjs';
import { UserDto } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { BreakpointService } from '../../core/layout/breakpoint.service';
import { PagingConstraints } from '../../core/models/constraints';
import { USER_ROLES, UserRole } from '../../core/models/enums';
import { NotificationService } from '../../core/notifications/notification.service';
import { ConfirmService } from '../../shared/components/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { EnumLabelPipe } from '../../shared/pipes/enum-label.pipe';
import { injectUrlQueryState, readInt, readOneOf, readString } from '../../shared/utils/url-query-state';
import { ChangeRoleDialogComponent } from './change-role-dialog.component';
import { CreateUserDialogComponent } from './create-user-dialog.component';
import { USER_SORT_FIELDS, UserQueryParameters, UsersApi } from './data-access/users.api';

@Component({
  selector: 'app-user-list',
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
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    EmptyStateComponent,
    EnumLabelPipe,
  ],
  template: `
    <app-page-header title="Users" subtitle="Manage accounts and roles">
      <button mat-flat-button type="button" (click)="create()"><mat-icon>person_add</mat-icon> New user</button>
    </app-page-header>

    <div class="filters" role="search">
      <mat-form-field class="search" subscriptSizing="dynamic">
        <mat-label>Search users</mat-label>
        <mat-icon matIconPrefix aria-hidden="true">search</mat-icon>
        <input matInput type="search" [formControl]="search" [maxlength]="searchMax" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Role</mat-label>
        <mat-select [value]="query().role ?? null" (selectionChange)="patch({ role: $event.value })">
          <mat-option [value]="null">Any role</mat-option>
          @for (r of roles; track r) {
            <mat-option [value]="r">{{ r | enumLabel }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select [value]="activeFilter()" (selectionChange)="patch({ active: $event.value })">
          <mat-option [value]="null">Any status</mat-option>
          <mat-option value="true">Active</mat-option>
          <mat-option value="false">Inactive</mat-option>
        </mat-select>
      </mat-form-field>
    </div>

    <mat-menu #actions="matMenu">
      <ng-template matMenuContent let-user="user">
        <button mat-menu-item type="button" (click)="changeRole(user)"><mat-icon>badge</mat-icon> Change role</button>
        @if (user.isActive) {
          <button mat-menu-item type="button" (click)="setActive(user, false)"><mat-icon>block</mat-icon> Deactivate</button>
        } @else {
          <button mat-menu-item type="button" (click)="setActive(user, true)"><mat-icon>check_circle</mat-icon> Activate</button>
        }
      </ng-template>
    </mat-menu>

    @if (users.hasValue()) {
      @let page = users.value();
      @if (page.items.length === 0) {
        <app-empty-state icon="group" title="No users found" message="Try other filters." />
      } @else if (isSmall()) {
        <div class="card-list">
          @for (u of page.items; track u.id) {
            <div class="item-card">
              <div class="row">
                <span class="title">{{ u.fullName }}</span>
                @if (u.id !== meId()) {
                  <button mat-icon-button type="button" [matMenuTriggerFor]="actions" [matMenuTriggerData]="{ user: u }" [attr.aria-label]="'Actions for ' + u.fullName">
                    <mat-icon>more_vert</mat-icon>
                  </button>
                }
              </div>
              <div class="meta">{{ u.email }}</div>
              <div class="meta">{{ u.role | enumLabel }} · {{ u.isActive ? 'Active' : 'Inactive' }}</div>
            </div>
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
              <td mat-cell *matCellDef="let u">
                {{ u.fullName }}
                @if (u.mustChangePassword) {
                  <span class="muted small">(temporary password)</span>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="email">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Email</th>
              <td mat-cell *matCellDef="let u">{{ u.email }}</td>
            </ng-container>
            <ng-container matColumnDef="role">
              <th mat-header-cell *matHeaderCellDef>Role</th>
              <td mat-cell *matCellDef="let u">{{ u.role | enumLabel }}</td>
            </ng-container>
            <ng-container matColumnDef="status">
              <th mat-header-cell *matHeaderCellDef>Status</th>
              <td mat-cell *matCellDef="let u" [class.muted]="!u.isActive">{{ u.isActive ? 'Active' : 'Inactive' }}</td>
            </ng-container>
            <ng-container matColumnDef="lastLoginAt">
              <th mat-header-cell *matHeaderCellDef>Last sign-in</th>
              <td mat-cell *matCellDef="let u">{{ u.lastLoginAt ? (u.lastLoginAt | date: 'medium') : 'Never' }}</td>
            </ng-container>
            <ng-container matColumnDef="createdAt">
              <th mat-header-cell *matHeaderCellDef mat-sort-header>Created</th>
              <td mat-cell *matCellDef="let u">{{ u.createdAt | date: 'mediumDate' }}</td>
            </ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef><span class="cdk-visually-hidden">Actions</span></th>
              <td mat-cell *matCellDef="let u" class="actions-cell">
                @if (u.id !== meId()) {
                  <button mat-icon-button type="button" [matMenuTriggerFor]="actions" [matMenuTriggerData]="{ user: u }" [attr.aria-label]="'Actions for ' + u.fullName">
                    <mat-icon>more_vert</mat-icon>
                  </button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
      }
      <mat-paginator
        [length]="page.totalCount"
        [pageIndex]="page.pageNumber - 1"
        [pageSize]="page.pageSize"
        [pageSizeOptions]="pageSizes"
        (page)="changePage($event)"
        aria-label="User pages"
      />
    } @else if (users.isLoading()) {
      <div class="loading"><mat-spinner diameter="36" aria-label="Loading users" /></div>
    } @else if (users.error()) {
      <app-empty-state icon="error_outline" title="Could not load users" message="Please try again." />
    }
  `,
  styles: `
    .loading { display: grid; place-items: center; padding: 40px; }
    .small { font-size: 12px; }
    .actions-cell { width: 56px; text-align: right; }
    mat-paginator { background: transparent; }
  `,
})
export default class UserListComponent {
  private readonly api = inject(UsersApi);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly url = injectUrlQueryState();
  protected readonly isSmall = inject(BreakpointService).isSmall;

  protected readonly roles = USER_ROLES;
  protected readonly pageSizes = [...PagingConstraints.pageSizeOptions];
  protected readonly searchMax = PagingConstraints.searchTermMaxLength;
  protected readonly columns = ['name', 'email', 'role', 'status', 'lastLoginAt', 'createdAt', 'actions'];
  /** An admin can't change their own role or deactivate themselves from here. */
  protected readonly meId = computed(() => this.auth.currentUser()?.id);

  protected readonly query = computed<UserQueryParameters>(() => {
    const p = this.url.params();
    const active = p.get('active');
    return {
      search: readString(p, 'q'),
      role: readOneOf(p, 'role', USER_ROLES),
      isActive: active === 'true' ? true : active === 'false' ? false : undefined,
      sortBy: readOneOf(p, 'sort', USER_SORT_FIELDS),
      sortDirection: readOneOf(p, 'dir', ['Asc', 'Desc'] as const),
      pageNumber: readInt(p, 'page', 1),
      pageSize: Math.min(readInt(p, 'size', PagingConstraints.defaultPageSize), PagingConstraints.maxPageSize),
    };
  });

  protected readonly activeFilter = computed(() => {
    const active = this.query().isActive;
    return active === undefined ? null : String(active);
  });

  protected readonly users = rxResource({
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
      .subscribe((q) => this.patch({ q }));
  }

  protected patch(patch: Record<string, string | null>): void {
    this.url.patch({ ...patch, page: null });
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

  protected create(): void {
    this.dialog
      .open<CreateUserDialogComponent, void, UserDto>(CreateUserDialogComponent, { width: '640px', maxWidth: '95vw' })
      .afterClosed()
      .pipe(filter((u): u is UserDto => !!u))
      .subscribe((user) => {
        this.notifications.success(`${user.fullName} created. Share the temporary password with them securely.`);
        this.users.reload();
      });
  }

  protected changeRole(user: UserDto): void {
    this.dialog
      .open<ChangeRoleDialogComponent, UserDto, UserRole>(ChangeRoleDialogComponent, { data: user, width: '420px' })
      .afterClosed()
      .pipe(
        filter((role): role is UserRole => !!role && role !== user.role),
        switchMap((role) => this.api.changeRole(user.id, role)),
      )
      .subscribe((updated) => {
        this.notifications.success(`${updated.fullName} is now ${updated.role}.`);
        this.users.reload();
      });
  }

  protected setActive(user: UserDto, active: boolean): void {
    const request: Observable<void> = active ? this.api.activate(user.id) : this.api.deactivate(user.id);
    this.confirm
      .confirm({
        title: active ? 'Activate user?' : 'Deactivate user?',
        message: active
          ? `${user.fullName} will be able to sign in again.`
          : `${user.fullName} will be signed out everywhere and can no longer sign in.`,
        confirmText: active ? 'Activate' : 'Deactivate',
        destructive: !active,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => request),
      )
      .subscribe(() => {
        this.notifications.success(active ? 'User activated.' : 'User deactivated.');
        this.users.reload();
      });
  }
}
