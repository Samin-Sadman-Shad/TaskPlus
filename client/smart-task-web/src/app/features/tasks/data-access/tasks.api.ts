import { HttpClient, HttpContext } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SKIP_ERROR_TOAST, apiUrl, toHttpParams, unwrap } from '../../../core/http/api-url';
import { BaseResponse, PagedResult } from '../../../core/http/api.models';
import { TaskItemStatus } from '../../../core/models/enums';
import {
  CreateTaskDto,
  ImproveDescriptionRequestDto,
  ImproveDescriptionResponseDto,
  TaskDto,
  TaskQueryParameters,
  TaskSummaryDto,
  UpdateTaskDto,
} from './tasks.models';

@Injectable({ providedIn: 'root' })
export class TasksApi {
  private readonly http = inject(HttpClient);

  /** GET tasks — every task the user can see (scoped by role on the server). */
  list(query: TaskQueryParameters): Observable<PagedResult<TaskSummaryDto>> {
    return this.http
      .get<BaseResponse<PagedResult<TaskSummaryDto>>>(apiUrl('tasks'), { params: toHttpParams(query) })
      .pipe(unwrap());
  }

  /** GET projects/{projectId}/tasks */
  listByProject(projectId: string, query: TaskQueryParameters): Observable<PagedResult<TaskSummaryDto>> {
    return this.http
      .get<BaseResponse<PagedResult<TaskSummaryDto>>>(apiUrl('projects', projectId, 'tasks'), {
        params: toHttpParams(query),
      })
      .pipe(unwrap());
  }

  get(id: string): Observable<TaskDto> {
    return this.http.get<BaseResponse<TaskDto>>(apiUrl('tasks', id)).pipe(unwrap());
  }

  create(projectId: string, dto: CreateTaskDto): Observable<TaskDto> {
    return this.http.post<BaseResponse<TaskDto>>(apiUrl('projects', projectId, 'tasks'), dto).pipe(unwrap());
  }

  update(id: string, dto: UpdateTaskDto): Observable<TaskDto> {
    return this.http.put<BaseResponse<TaskDto>>(apiUrl('tasks', id), dto).pipe(unwrap());
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(apiUrl('tasks', id));
  }

  changeStatus(id: string, status: TaskItemStatus): Observable<TaskDto> {
    return this.http.patch<BaseResponse<TaskDto>>(apiUrl('tasks', id, 'status'), { status }).pipe(unwrap());
  }

  assign(id: string, assigneeId: string | null): Observable<TaskDto> {
    return this.http.patch<BaseResponse<TaskDto>>(apiUrl('tasks', id, 'assignee'), { assigneeId }).pipe(unwrap());
  }
}

@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);

  /** POST ai/task-descriptions/improve. Errors are shown inline in the dialog, not as a toast. */
  improveDescription(dto: ImproveDescriptionRequestDto): Observable<ImproveDescriptionResponseDto> {
    return this.http
      .post<BaseResponse<ImproveDescriptionResponseDto>>(apiUrl('ai', 'task-descriptions', 'improve'), dto, {
        context: new HttpContext().set(SKIP_ERROR_TOAST, true),
      })
      .pipe(unwrap());
  }
}
