import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { UserRole } from '../models/enums';

/** Allows the route when the user has one of route.data['roles']. UX only: the API enforces roles too. */
export const roleGuard: CanMatchFn = (route, segments) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // canMatch runs before the shell's authGuard, so handle the signed-out case here too.
  if (!auth.isAuthenticated()) {
    const returnUrl = '/' + segments.map((s) => s.path).join('/');
    return router.createUrlTree(['/login'], { queryParams: { returnUrl } });
  }

  const roles = (route.data?.['roles'] ?? []) as UserRole[];
  return roles.length === 0 || auth.hasRole(...roles) ? true : router.createUrlTree(['/forbidden']);
};
