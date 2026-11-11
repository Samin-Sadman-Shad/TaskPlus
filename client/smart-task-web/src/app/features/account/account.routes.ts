import { Routes } from '@angular/router';

export default [
  { path: 'change-password', title: 'Change password', loadComponent: () => import('./change-password.component') },
] satisfies Routes;
