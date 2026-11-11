import { Routes } from '@angular/router';

export default [
  { path: 'login', title: 'Sign in', loadComponent: () => import('./login.component') },
  { path: 'register', title: 'Create account', loadComponent: () => import('./register.component') },
] satisfies Routes;
