import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { ChartConfiguration } from 'chart.js';
import { BaseChartDirective, provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { TASK_PRIORITIES, TASK_STATUSES, TaskItemStatus, TaskPriority } from '../../core/models/enums';
import { enumLabel } from '../../shared/pipes/enum-label.pipe';

const STATUS_COLORS: Record<TaskItemStatus, string> = {
  ToDo: '#94a3b8',
  InProgress: '#3b82f6',
  Completed: '#22c55e',
  Cancelled: '#d1d5db',
};

const PRIORITY_COLORS: Record<TaskPriority, string> = {
  Low: '#94a3b8',
  Medium: '#3b82f6',
  High: '#f59e0b',
  Critical: '#dc2626',
};

/**
 * Status and priority charts. Loaded with @defer (on viewport) from the dashboard, so Chart.js
 * is only downloaded when the charts scroll into view. Chart.js is registered here, not app-wide.
 */
@Component({
  selector: 'app-task-charts',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [BaseChartDirective, MatCardModule],
  providers: [provideCharts(withDefaultRegisterables())],
  template: `
    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Tasks by status</mat-card-title></mat-card-header>
      <mat-card-content>
        <div class="chart" role="img" [attr.aria-label]="statusSummary()">
          <canvas baseChart type="doughnut" [data]="statusData()" [options]="doughnutOptions"></canvas>
        </div>
      </mat-card-content>
    </mat-card>
    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Tasks by priority</mat-card-title></mat-card-header>
      <mat-card-content>
        <div class="chart" role="img" [attr.aria-label]="prioritySummary()">
          <canvas baseChart type="bar" [data]="priorityData()" [options]="barOptions"></canvas>
        </div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    :host { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; }
    .chart { position: relative; height: 240px; }
  `,
})
export class TaskChartsComponent {
  readonly byStatus = input.required<Record<TaskItemStatus, number>>();
  readonly byPriority = input.required<Record<TaskPriority, number>>();

  protected readonly doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    maintainAspectRatio: false,
    plugins: { legend: { position: 'right' } },
  };

  protected readonly barOptions: ChartConfiguration<'bar'>['options'] = {
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: { y: { beginAtZero: true, ticks: { precision: 0 } }, x: { grid: { display: false } } },
  };

  protected readonly statusData = computed<ChartConfiguration<'doughnut'>['data']>(() => ({
    labels: TASK_STATUSES.map(enumLabel),
    datasets: [
      {
        data: TASK_STATUSES.map((s) => this.byStatus()[s] ?? 0),
        backgroundColor: TASK_STATUSES.map((s) => STATUS_COLORS[s]),
        borderWidth: 0,
      },
    ],
  }));

  protected readonly priorityData = computed<ChartConfiguration<'bar'>['data']>(() => ({
    labels: [...TASK_PRIORITIES],
    datasets: [
      {
        label: 'Tasks',
        data: TASK_PRIORITIES.map((p) => this.byPriority()[p] ?? 0),
        backgroundColor: TASK_PRIORITIES.map((p) => PRIORITY_COLORS[p]),
        borderRadius: 4,
      },
    ],
  }));

  /** Text alternative for screen readers. */
  protected readonly statusSummary = computed(
    () => 'Tasks by status: ' + TASK_STATUSES.map((s) => `${enumLabel(s)} ${this.byStatus()[s] ?? 0}`).join(', '),
  );
  protected readonly prioritySummary = computed(
    () => 'Tasks by priority: ' + TASK_PRIORITIES.map((p) => `${p} ${this.byPriority()[p] ?? 0}`).join(', '),
  );
}
