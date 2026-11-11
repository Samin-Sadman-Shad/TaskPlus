import { UserDto } from '../../../core/auth/auth.models';
import { UserOption } from '../../../shared/components/user-picker.component';
import { ProjectDto } from './projects.models';

/** Admin, or the ProjectManager who owns the project (CLAUDE.md §4). The API enforces the same rule. */
export function canManageProject(user: UserDto | null, project: ProjectDto | null | undefined): boolean {
  if (!user || !project) {
    return false;
  }
  return user.role === 'Admin' || (user.role === 'ProjectManager' && project.owner.id === user.id);
}

/** Everyone a task in this project can be assigned to: the owner plus the members. */
export function assignableUsers(project: ProjectDto): UserOption[] {
  const owner: UserOption = { id: project.owner.id, fullName: project.owner.fullName, email: project.owner.email };
  const members = project.members.map((m) => ({ id: m.userId, fullName: m.fullName, email: m.email }));
  return [owner, ...members.filter((m) => m.id !== owner.id)];
}
