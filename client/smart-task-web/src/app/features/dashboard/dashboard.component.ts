import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { DashboardConstraints } from '../../core/models/constraints';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';
import { PageHeaderComponent } from '../../shared/components/page-header.component';
import { PriorityChipComponent } from '../../shared/components/priority-chip.component';
import { StatusChipComponent } from '../../shared/components/status-chip.component';
import { injectUrlQueryState } from '../../shared/utils/url-query-state';
import { DashboardApi } from './data-access/dashboard.api';
import { TaskChartsComponent } from './task-charts.component';

@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonToggleModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    EmptyStateComponent,
    StatusChipComponent,
    PriorityChipComponent,
    TaskChartsComponent,
  ],
  template: `
    <app-page-header title="Dashboard" [subtitle]="scope()" />

    @if (stats.hasValue()) {
      @let s = stats.value();
      <section class="stats" aria-label="Summary">
        <mat-card appearance="outlined" class="stat">
          <span class="label">Projects</span><span class="value">{{ s.totalProjects }}</span>
        </mat-card>
        <mat-card appearance="outlined" class="stat">
          <span class="label">Tasks</span><span class="value">{{ s.totalTasks }}</span>
        </mat-card>
        <mat-card appearance="outlined" class="stat">
          <span class="label">Completed</span><span class="value done">{{ s.completedCount }}</span>
        </mat-card>
        <mat-card appearance="outlined" class="stat">
          <span class="label">Pending</span><span class="value">{{ s.pendingCount }}</span>
        </mat-card>
        <mat-card appearance="outlined" class="stat">
          <span class="label">Overdue</span><span class="value" [class.overdue]="s.overdueCount > 0">{{ s.overdueCount }}</span>
        </mat-card>
      </section>

      @defer (on viewport) {
        <app-task-charts [byStatus]="s.tasksByStatus" [byPriority]="s.tasksByPriority" />
      } @placeholder {
        <div class="chart-placeholder" aria-hidden="true"></div>
      } @loading (minimum 200ms) {
        <div class="chart-placeholder"><mat-spinner diameter="32" aria-label="Loading charts" /></div>
      }

      <mat-card appearance="outlined" class="upcoming">
        <mat-card-header class="upcoming-header">
          <mat-card-title>Upcoming due tasks</mat-card-title>
          <mat-button-toggle-group
            hideSingleSelectionIndicator
            aria-label="Upcoming window in days"
            [value]="upcomingDays()"
            (change)="setDays($event.value)"
          >
            @for (d of dayOptions; track d) {
              <mat-button-toggle [value]="d">{{ d }} days</mat-button-toggle>
            }
          </mat-button-toggle-group>
        </mat-card-header>
        <mat-card-content>
          @for (task of s.upcomingDueTasks; track task.id) {
            <a class="task-row" [routerLink]="['/tasks', task.id]">
              <span class="title">{{ task.title }}</span>
              <span class="meta">
                <app-priority-chip [priority]="task.priority" />
                <app-status-chip [status]="task.status" />
                <span [class.overdue]="task.isOverdue">{{ task.dueDate | date: 'mediumDate' }}</span>
              </span>
            </a>
          } @empty {
            <app-empty-state icon="event_available" title="Nothing due" [message]="'No open tasks due in the next ' + upcomingDays() + ' days.'" />
          }
        </mat-card-content>
      </mat-card>
    } @else if (stats.isLoading()) {
      <div class="loading"><mat-spinner diameter="40" aria-label="Loading dashboard" /></div>
    } @else if (stats.error()) {
      <app-empty-state icon="error_outline" title="Could not load the dashboard" message="Please try again in a moment." />
    }
  `,
  styles: `
    .stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 16px; margin-bottom: 16px; }
    .stat { padding: 16px; display: flex; flex-direction: column; gap: 4px; }
    .label { color: var(--app-muted); font-size: 13px; }
    .value { font-size: 28px; font-weight: 500; }
    .done { color: #15803d; }
    .chart-placeholder { min-height: 300px; display: grid; place-items: center; }
    .upcoming { margin-top: 16px; }
    .upcoming-header { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 8px; padding-bottom: 8px; }
    .task-row { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 8px; padding: 10px 4px; border-top: 1px solid var(--app-border); color: inherit; text-decoration: none; }
    .task-row:hover { background: #f9fafb; }
    .title { font-weight: 500; }
    .meta { display: flex; align-items: center; gap: 12px; font-size: 13px; }
    .loading { display: grid; place-items: center; padding: 48px; }
  `,
})
export default class DashboardComponent {
  private readonly api = inject(DashboardApi);
  private readonly auth = inject(AuthService);
  private readonly query = injectUrlQueryState();

  protected readonly dayOptions = DashboardConstraints.upcomingDayOptions;

  /** ?days= in the URL, limited to the offered options. */
  protected readonly upcomingDays = computed(() => {
    const value = Number(this.query.params().get('days'));
    return this.dayOptions.find((d) => d === value) ?? DashboardConstraints.defaultUpcomingDays;
  });

  protected readonly stats = rxResource({
    params: () => this.upcomingDays(),
    stream: ({ params }) => this.api.get(params),
  });

  protected readonly scope = computed(() => {
    switch (this.auth.role()) {
      case 'Admin':
        return 'All projects and tasks';
      case 'ProjectManager':
        return 'Projects you own';
      default:
        return 'Your projects and assigned tasks';
    }
  });

  protected setDays(days: number): void {
    this.query.patch({ days: days === DashboardConstraints.defaultUpcomingDays ? null : days });
  }
}
