import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { TokenStorage } from '../auth/token-storage';
import { isApiUrl, isAuthUrl } from '../http/api-url';

/** Adds the bearer token to API calls, except /auth/* (those use the refresh cookie). */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(TokenStorage).get();
  if (!token || !isApiUrl(req.url) || isAuthUrl(req.url)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
};
