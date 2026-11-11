import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export const CHANGE_PASSWORD_URL = '/account/change-password';

/** While an admin-set (temporary) password is active, every page redirects to the change-password form. */
export const passwordChangeGuard: CanActivateChildFn = (_route, state) => {
  if (!inject(AuthService).mustChangePassword() || state.url.startsWith(CHANGE_PASSWORD_URL)) {
    return true;
  }
  return inject(Router).createUrlTree([CHANGE_PASSWORD_URL]);
};
