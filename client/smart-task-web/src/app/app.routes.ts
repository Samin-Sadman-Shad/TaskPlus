import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { guestGuard } from './core/guards/guest.guard';
import { passwordChangeGuard } from './core/guards/password-change.guard';
import { roleGuard } from './core/guards/role.guard';
import { ShellComponent } from './core/layout/shell.component';
import { UserRole } from './core/models/enums';

export const routes: Routes = [
  // /login and /register, for guests only. For a signed-in user this route doesn't match and the
  // shell below redirects those paths to the dashboard.
  { path: '', canMatch: [guestGuard], loadChildren: () => import('./features/auth/auth.routes') },
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    canActivateChild: [passwordChangeGuard],
    children: [
      { path: 'dashboard', loadChildren: () => import('./features/dashboard/dashboard.routes') },
      { path: 'projects', loadChildren: () => import('./features/projects/projects.routes') },
      { path: 'tasks', loadChildren: () => import('./features/tasks/tasks.routes') },
      {
        path: 'users',
        canMatch: [roleGuard],
        data: { roles: ['Admin'] satisfies UserRole[] },
        loadChildren: () => import('./features/users/users.routes'),
      },
      { path: 'account', loadChildren: () => import('./features/account/account.routes') },
      { path: 'forbidden', title: 'Not allowed', loadComponent: () => import('./shared/components/forbidden.component') },
      { path: 'login', redirectTo: 'dashboard' },
      { path: 'register', redirectTo: 'dashboard' },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
    ],
  },
  { path: '**', title: 'Not found', loadComponent: () => import('./shared/components/not-found.component') },
];
