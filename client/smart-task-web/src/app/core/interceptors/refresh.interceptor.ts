import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { isApiUrl, isAuthUrl } from '../http/api-url';

const RETRIED_AFTER_REFRESH = new HttpContextToken<boolean>(() => false);

/**
 * On a 401 from a non-auth API call: refresh once (shared by all parallel failures, see
 * AuthService.refresh), retry the request once with the new token, and sign out if the refresh fails.
 */
export const refreshInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  return next(req).pipe(
    catchError((error: unknown) => {
      const canRefresh =
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        isApiUrl(req.url) &&
        !isAuthUrl(req.url) &&
        !req.context.get(RETRIED_AFTER_REFRESH);

      if (!canRefresh) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError(() => {
          auth.endExpiredSession();
          return throwError(() => error);
        }),
        switchMap((session) =>
          next(
            req.clone({
              setHeaders: { Authorization: `Bearer ${session.accessToken}` },
              context: req.context.set(RETRIED_AFTER_REFRESH, true),
            }),
          ),
        ),
      );
    }),
  );
};
