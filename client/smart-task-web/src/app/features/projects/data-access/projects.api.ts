import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiUrl, toHttpParams, unwrap } from '../../../core/http/api-url';
import { BaseResponse, PagedResult } from '../../../core/http/api.models';
import { CreateProjectDto, ProjectDto, ProjectQueryParameters, ProjectSummaryDto, UpdateProjectDto } from './projects.models';

@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);

  list(query: ProjectQueryParameters): Observable<PagedResult<ProjectSummaryDto>> {
    return this.http
      .get<BaseResponse<PagedResult<ProjectSummaryDto>>>(apiUrl('projects'), { params: toHttpParams(query) })
      .pipe(unwrap());
  }

  get(id: string): Observable<ProjectDto> {
    return this.http.get<BaseResponse<ProjectDto>>(apiUrl('projects', id)).pipe(unwrap());
  }

  create(dto: CreateProjectDto): Observable<ProjectDto> {
    return this.http.post<BaseResponse<ProjectDto>>(apiUrl('projects'), dto).pipe(unwrap());
  }

  update(id: string, dto: UpdateProjectDto): Observable<ProjectDto> {
    return this.http.put<BaseResponse<ProjectDto>>(apiUrl('projects', id), dto).pipe(unwrap());
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(apiUrl('projects', id));
  }

  addMember(projectId: string, userId: string): Observable<void> {
    return this.http.post<void>(apiUrl('projects', projectId, 'members'), { userId });
  }

  removeMember(projectId: string, userId: string): Observable<void> {
    return this.http.delete<void>(apiUrl('projects', projectId, 'members', userId));
  }
}
