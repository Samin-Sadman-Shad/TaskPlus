import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { CHANGE_PASSWORD_URL } from '../guards/password-change.guard';
import { SKIP_ERROR_TOAST, isApiUrl, readApiError } from '../http/api-url';
import { NotificationService } from '../notifications/notification.service';

const TOAST_STATUSES = new Set([403, 404, 409, 429]);

const FALLBACK_MESSAGES: Record<number, string> = {
  0: 'Cannot reach the server. Check your connection and try again.',
  403: 'You do not have permission to do that.',
  404: 'The item was not found.',
  409: 'The item was changed or already exists.',
  429: 'Too many requests. Please wait a moment and try again.',
};

/**
 * Turns API failures into user-facing messages. 400 (validation) and 401 (refresh interceptor or
 * the login form) are left to the caller. Raw error objects are never shown.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const notifications = inject(NotificationService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && isApiUrl(req.url)) {
        const body = readApiError(error);

        if (error.status === 403 && body?.errorCode === 'PASSWORD_CHANGE_REQUIRED') {
          void router.navigateByUrl(CHANGE_PASSWORD_URL);
        } else if (!req.context.get(SKIP_ERROR_TOAST) && shouldNotify(error.status)) {
          notifications.error(body?.message ?? FALLBACK_MESSAGES[error.status] ?? 'Something went wrong. Please try again.');
        }
      }
      return throwError(() => error);
    }),
  );
};

function shouldNotify(status: number): boolean {
  return status === 0 || status >= 500 || TOAST_STATUSES.has(status);
}
