import { Routes } from '@angular/router';

export default [
  { path: '', title: 'Projects', loadComponent: () => import('./project-list.component') },
  { path: ':id', title: 'Project', loadComponent: () => import('./project-detail.component') },
] satisfies Routes;
