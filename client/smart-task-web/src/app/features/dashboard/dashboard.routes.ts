import { Routes } from '@angular/router';

export default [{ path: '', title: 'Dashboard', loadComponent: () => import('./dashboard.component') }] satisfies Routes;
