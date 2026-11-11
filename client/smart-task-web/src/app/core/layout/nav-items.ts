import { UserRole } from '../models/enums';

export interface NavItem {
  label: string;
  icon: string;
  path: string;
  /** Empty means every role. */
  roles: readonly UserRole[];
}

export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Dashboard', icon: 'dashboard', path: '/dashboard', roles: [] },
  { label: 'Projects', icon: 'folder', path: '/projects', roles: [] },
  { label: 'Tasks', icon: 'task_alt', path: '/tasks', roles: [] },
  { label: 'Users', icon: 'group', path: '/users', roles: ['Admin'] },
];

export function navItemsFor(role: UserRole | null): NavItem[] {
  if (role === null) {
    return [];
  }
  return NAV_ITEMS.filter((item) => item.roles.length === 0 || item.roles.includes(role));
}
