import { HttpContextToken, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BaseResponse } from './api.models';

/** Builds an absolute API path, e.g. apiUrl('projects', id) → /api/v1/projects/{id}. */
export function apiUrl(...segments: string[]): string {
  return [environment.apiBaseUrl, ...segments.map(encodeURIComponent)].join('/');
}

export function isApiUrl(url: string): boolean {
  return url.startsWith(environment.apiBaseUrl);
}

/** Auth endpoints never carry the bearer token and never trigger a refresh. */
export function isAuthUrl(url: string): boolean {
  return url.startsWith(`${environment.apiBaseUrl}/auth/`);
}

type QueryValue = string | number | boolean | null | undefined;

/** Turns a query object into HttpParams, dropping empty values. */
export function toHttpParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query) as [string, QueryValue][]) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}

/** Unwraps BaseResponse.data. Use only on endpoints that always return data on success. */
export function unwrap<T>() {
  return (source: Observable<BaseResponse<T>>): Observable<T> => source.pipe(map((response) => response.data as T));
}

/** Reads the BaseResponse body from an HTTP error, if the server sent one. */
export function readApiError(error: unknown): BaseResponse | null {
  if (error instanceof HttpErrorResponse && error.error && typeof error.error === 'object' && 'success' in error.error) {
    return error.error as BaseResponse;
  }
  return null;
}

/** Set on a request whose errors the caller shows inline, so the error interceptor stays quiet. */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);
