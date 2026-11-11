import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { UserDto } from '../../../core/auth/auth.models';
import { apiUrl, toHttpParams, unwrap } from '../../../core/http/api-url';
import { BaseResponse, PageQuery, PagedResult } from '../../../core/http/api.models';
import { UserRole } from '../../../core/models/enums';

/** UserQueryParameters.SortFields */
export const USER_SORT_FIELDS = ['name', 'email', 'createdAt'] as const;
export type UserSortField = (typeof USER_SORT_FIELDS)[number];

export interface UserQueryParameters extends PageQuery<UserSortField> {
  role?: UserRole;
  isActive?: boolean;
}

export interface CreateUserDto {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
  role: UserRole;
}

export interface ChangeUserRoleDto {
  role: UserRole;
}

/** Admin-only user management endpoints. */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  list(query: UserQueryParameters): Observable<PagedResult<UserDto>> {
    return this.http
      .get<BaseResponse<PagedResult<UserDto>>>(apiUrl('users'), { params: toHttpParams(query) })
      .pipe(unwrap());
  }

  /** The password is temporary: the user must change it at first sign-in. */
  create(dto: CreateUserDto): Observable<UserDto> {
    return this.http.post<BaseResponse<UserDto>>(apiUrl('users'), dto).pipe(unwrap());
  }

  changeRole(id: string, role: UserRole): Observable<UserDto> {
    return this.http
      .patch<BaseResponse<UserDto>>(apiUrl('users', id, 'role'), { role } satisfies ChangeUserRoleDto)
      .pipe(unwrap());
  }

  activate(id: string): Observable<void> {
    return this.http.post<void>(apiUrl('users', id, 'activate'), null);
  }

  /** Also revokes the user's refresh tokens on the server. */
  deactivate(id: string): Observable<void> {
    return this.http.post<void>(apiUrl('users', id, 'deactivate'), null);
  }
}
