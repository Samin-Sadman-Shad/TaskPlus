import { Routes } from '@angular/router';

/** Mounted behind roleGuard (Admin) in app.routes.ts. */
export default [{ path: '', title: 'Users', loadComponent: () => import('./user-list.component') }] satisfies Routes;
