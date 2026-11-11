import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummaryDto } from '../auth/auth.models';
import { apiUrl, toHttpParams, unwrap } from '../http/api-url';
import { BaseResponse } from '../http/api.models';

/** GET users/lookup (Admin, ProjectManager): up to 20 active users matching the term. */
@Injectable({ providedIn: 'root' })
export class UserLookupApi {
  private readonly http = inject(HttpClient);

  search(term: string): Observable<UserSummaryDto[]> {
    return this.http
      .get<BaseResponse<UserSummaryDto[]>>(apiUrl('users', 'lookup'), { params: toHttpParams({ search: term }) })
      .pipe(unwrap());
  }
}
