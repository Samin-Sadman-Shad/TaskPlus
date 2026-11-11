import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Tasks', loadComponent: () => import('./tasks-page.component') },
  { path: ':id', title: 'Task', loadComponent: () => import('./task-detail.component') },
] satisfies Routes;
