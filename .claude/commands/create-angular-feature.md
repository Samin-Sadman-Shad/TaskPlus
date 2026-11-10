---
description: Scaffold the Angular core (auth, interceptors, guards, layout) or a lazy-loaded feature (auth, dashboard, projects, tasks, users) with standalone components and reactive forms
argument-hint: "core | auth | dashboard | projects | tasks | users"
allowed-tools: Read, Write, Edit, Glob, Grep, Bash(npm:*), Bash(npx:*), Bash(ng:*), Bash(ls:*), Bash(mkdir:*)
---

# /create-angular-feature

Build the Angular frontend one slice at a time. **Run `core` first**, then the features in any order.

**Scope:** `$ARGUMENTS` — required. One of `core`, `auth`, `dashboard`, `projects`, `tasks`, `users`.
If it's empty or unknown, list the options and stop.

## 0. Read context first

1. `CLAUDE.md` — roles & permissions
2. `docs/API-CONVENTIONS.md` — response shape (§2), endpoint catalogue (§7), auth endpoints, cookie design
3. The DTOs under `src/SmartTask.Application/DTOs/` — TypeScript models must match them exactly (camelCase)
4. Existing code under `client/smart-task-web/`

## 1. Project setup (only if `client/smart-task-web` doesn't exist)

```bash
npx @angular/cli@latest new smart-task-web --directory client/smart-task-web \
  --routing --style=scss --ssr=false --skip-git
cd client/smart-task-web && npx ng add @angular/material
```
- TypeScript `strict: true` plus Angular `strictTemplates` (CLI defaults — check them).
- Use the CLI's defaults for change detection (zoneless if that's the default for the installed version).
- `proxy.conf.json` maps `/api` → `https://localhost:7180` (`secure: false` for the dev certificate) and
  is wired into `angular.json` → `serve.options.proxyConfig`. The browser then talks to the same
  origin, so the `SameSite=Strict` refresh cookie works in development.
- `src/environments/environment.ts`: `apiBaseUrl: '/api/v1'`.
- ESLint via `ng add @angular-eslint/schematics`.

## 2. Folder structure

```
src/app/
├── core/                       # singletons, loaded once
│   ├── auth/                   auth.service.ts, token-storage.ts (memory only), auth.models.ts
│   ├── guards/                 auth.guard.ts, role.guard.ts, guest.guard.ts
│   ├── interceptors/           auth.interceptor.ts, refresh.interceptor.ts, error.interceptor.ts, loading.interceptor.ts
│   ├── http/                   api.models.ts (BaseResponse<T>, PagedResult<T>, query params), api-url.ts
│   ├── layout/                 shell.component (toolbar + responsive sidenav), nav items by role
│   └── notifications/          notification.service.ts (MatSnackBar wrapper)
├── shared/                     # reusable, no dependency on features
│   ├── components/             confirm-dialog, empty-state, page-header, status-chip, priority-chip
│   ├── pipes/                  enum-label.pipe.ts
│   └── forms/                  control-error.component.ts (shows validation + server field errors)
├── features/
│   ├── auth/                   login, register pages + auth.routes.ts
│   ├── dashboard/              dashboard page, stat cards, charts + dashboard.routes.ts
│   ├── projects/               list, detail, form dialog, data-access/projects.api.ts + projects.routes.ts
│   ├── tasks/                  list, detail, form dialog (with "Improve with AI"), data-access/tasks.api.ts + tasks.routes.ts
│   └── users/                  admin user list, role change + users.routes.ts
├── app.routes.ts
└── app.config.ts
```

## 3. Angular rules (all slices)

- **Standalone components only**, `changeDetection: ChangeDetectionStrategy.OnPush`.
- `inject()` instead of constructor parameters; **signals** for component state (`signal`, `computed`),
  `input()`/`output()` APIs, and built-in control flow (`@if`, `@for` with `track`, `@defer` for charts).
- **Reactive forms**, strongly typed, via `NonNullableFormBuilder`. Validators mirror the backend
  (same max lengths, required fields, date rules). On a 400 response, map `errors` onto the matching
  controls (`setErrors({ server: msg })`).
- **Data access** lives in `features/<x>/data-access/*.api.ts` (an `@Injectable({ providedIn: 'root' })`
  class with one method per endpoint, returning `Observable<T>` already unwrapped from `BaseResponse`).
  Components never call `HttpClient` directly.
- List pages keep search/filter/sort/page state in **URL query params**, so links and refresh keep
  the view. Search input is debounced (300 ms) with `distinctUntilChanged`. Use `mat-table` +
  `mat-paginator` + `matSort` with server-side paging.
- **Responsive**: Material layout + CSS grid/flex. The sidenav goes from `side` to `over` below 960 px
  (`BreakpointObserver`). Tables switch to cards on small screens.
- Accessibility: labels on all inputs, `aria-label` on icon buttons, visible focus.
- Hide actions the user's role can't perform (e.g. TeamMember sees no "New project"). **The server
  still enforces** this; hiding is only for UX.
- No `any`. Enums are string union types matching the API (`'ToDo' | 'InProgress' | 'Completed' | 'Cancelled'`).

## 4. Slice: `core`

- `api.models.ts`: `BaseResponse<T>`, `PagedResult<T>`, `ProblemFieldErrors`, query param types.
- **AuthService** (signals): `currentUser`, `isAuthenticated = computed(...)`, `role`. Methods
  `login`, `register`, `refresh`, `logout`, `hasRole(...roles)`.
  - The access token is kept **in memory only** (never in `localStorage`/`sessionStorage`).
  - The refresh token is an HttpOnly cookie the browser handles. Auth calls use `withCredentials: true`.
  - On app start, `provideAppInitializer(() => inject(AuthService).tryRestoreSession())` calls
    `POST /auth/refresh` to restore the session after a page reload (a 401 is silently ignored).
  - Schedule a silent refresh ~1 minute before `accessTokenExpiresAt`.
- **Functional interceptors** (`HttpInterceptorFn`), registered in order with
  `provideHttpClient(withInterceptors([...]), withFetch())`:
  1. `authInterceptor` — adds `Authorization: Bearer` to `/api` requests except `/auth/*`
  2. `refreshInterceptor` — on a 401 from a non-auth request, run **one shared** refresh (single
     in-flight observable with `shareReplay`), retry the original request once, and log out on failure
  3. `errorInterceptor` — shows the `BaseResponse.message` in a snackbar for 403/404/409/429/5xx;
     leaves 400 to the form; never shows raw error objects
  4. `loadingInterceptor` — a global loading signal for the progress bar
- **Functional guards** (`CanActivateFn` / `CanMatchFn`): `authGuard` (redirects to `/login?returnUrl=`),
  `guestGuard` (logged-in users skip the login page), and `roleGuard` reading `route.data['roles']`.
- **Routes** (`app.routes.ts`), all lazy:
  ```ts
  { path: 'login', canMatch: [guestGuard], loadChildren: () => import('./features/auth/auth.routes') },
  { path: '', component: ShellComponent, canActivate: [authGuard], children: [
      { path: 'dashboard', loadChildren: () => import('./features/dashboard/dashboard.routes') },
      { path: 'projects',  loadChildren: () => import('./features/projects/projects.routes') },
      { path: 'tasks',     loadChildren: () => import('./features/tasks/tasks.routes') },
      { path: 'users', canMatch: [roleGuard], data: { roles: ['Admin'] }, loadChildren: () => import('./features/users/users.routes') },
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' } ] },
  { path: '**', loadComponent: () => import('./shared/components/not-found.component') }
  ```
  Use `withComponentInputBinding()` so route params bind to component inputs.
- `ShellComponent`: toolbar (user menu, logout), sidenav with nav items filtered by role, global
  progress bar, and `<router-outlet>`.

## 5. Feature slices

| Slice | Must include |
|---|---|
| `auth` | Login (email, password, show/hide password, `returnUrl` redirect) and register (fields + password rules + confirm password match validator, then redirect to login with a success snackbar). Generic error message on 401 |
| `dashboard` | Stat cards (total projects, total tasks, completed vs pending, overdue); tasks by status and by priority as charts (`ng2-charts`/Chart.js inside `@defer (on viewport)`); upcoming due tasks list linking to the task. Selector for `upcomingDays` (7/14/30) |
| `projects` | Server-side paged list with search, date filters and sort; detail page with members and the project's tasks; create/edit dialog (reactive form, date range validator); delete with confirm dialog; add/remove member using `users/lookup` autocomplete. Actions shown only to Admin/owner PM |
| `tasks` | Global "My/visible tasks" list and project task list sharing one list component (filters: status, priority, assignee, due range, overdue toggle; search; sort; paging); detail page; create/edit dialog with an **"Improve with AI"** button that calls the AI endpoint, shows original vs suggestion side by side, and lets the user **Accept** or **Discard** (never auto-applies); a status menu that offers only valid transitions; assign via autocomplete of project members |
| `users` | Admin-only paged user list with role filter; change role (select + confirm); activate/deactivate |

Each slice exports its routes as `export default [...] satisfies Routes;`.

## 6. Tests

- Vitest or Karma/Jasmine, whichever the installed CLI sets up by default.
- Required: `authGuard`, `roleGuard`, `refreshInterceptor` (one refresh for parallel 401s, retry,
  logout on failure), AuthService session restore, one form component per feature (validators and
  server error mapping), and the data-access service URLs/params (`HttpTestingController`).

## 7. Verify

```bash
cd client/smart-task-web
npm run lint
npm test -- --watch=false
npm run build
grep -rn "localStorage\|sessionStorage" src/app   # must print nothing for tokens
```

Self-check, report ✅/❌:
- [ ] Standalone + OnPush + signals; lazy-loaded routes; functional guards and interceptors
- [ ] Reactive typed forms with server field-error mapping
- [ ] Access token in memory, refresh via HttpOnly cookie, single-flight refresh on 401
- [ ] Models match backend DTOs and `BaseResponse`
- [ ] Responsive layout checked at 375 px, 768 px and 1280 px (describe what you verified)
- [ ] Role-based UI matches `CLAUDE.md` §4
- [ ] Lint, tests and build pass

## 8. Finish

1. Update `docs/PROGRESS.md` (which slice was done).
2. Print the routes added and how to run: API on `https://localhost:7180`, then `npm start` in
   `client/smart-task-web` and open `http://localhost:4200`.
3. Suggested commit: `feat(web): <slice> feature`.
