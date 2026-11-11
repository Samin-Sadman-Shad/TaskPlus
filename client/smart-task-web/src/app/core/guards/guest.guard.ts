import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { AuthService } from '../auth/auth.service';

/**
 * Matches the login/register routes only for guests. For a signed-in user the router falls through
 * to the shell, which redirects /login and /register to the dashboard.
 */
export const guestGuard: CanMatchFn = () => !inject(AuthService).isAuthenticated();
