import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiUrl, toHttpParams, unwrap } from '../../../core/http/api-url';
import { BaseResponse } from '../../../core/http/api.models';
import { TaskItemStatus, TaskPriority } from '../../../core/models/enums';
import { TaskSummaryDto } from '../../tasks/data-access/tasks.models';

export interface DashboardStatsDto {
  totalProjects: number;
  totalTasks: number;
  /** Every status is present (zero when there are none). */
  tasksByStatus: Record<TaskItemStatus, number>;
  tasksByPriority: Record<TaskPriority, number>;
  completedCount: number;
  pendingCount: number;
  overdueCount: number;
  upcomingDueTasks: TaskSummaryDto[];
}

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);

  /** GET dashboard?upcomingDays= — scoped by role on the server. */
  get(upcomingDays: number): Observable<DashboardStatsDto> {
    return this.http
      .get<BaseResponse<DashboardStatsDto>>(apiUrl('dashboard'), { params: toHttpParams({ upcomingDays }) })
      .pipe(unwrap());
  }
}
