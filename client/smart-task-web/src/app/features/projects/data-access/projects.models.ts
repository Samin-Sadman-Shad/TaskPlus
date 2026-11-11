import { UserSummaryDto } from '../../../core/auth/auth.models';
import { DateOnlyString, DateTimeString, PageQuery } from '../../../core/http/api.models';

export interface ProjectMemberDto {
  userId: string;
  fullName: string;
  email: string;
  joinedAt: DateTimeString;
}

export interface ProjectDto {
  id: string;
  name: string;
  description?: string;
  startDate: DateOnlyString;
  endDate?: DateOnlyString;
  owner: UserSummaryDto;
  /** The owner is a member too but has no row here. */
  members: ProjectMemberDto[];
  taskCount: number;
  completedTaskCount: number;
  createdAt: DateTimeString;
  updatedAt?: DateTimeString;
}

export interface ProjectSummaryDto {
  id: string;
  name: string;
  startDate: DateOnlyString;
  endDate?: DateOnlyString;
  ownerName: string;
  taskCount: number;
  completedTaskCount: number;
  memberCount: number;
  createdAt: DateTimeString;
}

export interface CreateProjectDto {
  name: string;
  description: string | null;
  startDate: DateOnlyString;
  endDate: DateOnlyString | null;
}

export type UpdateProjectDto = CreateProjectDto;

export interface AddProjectMemberDto {
  userId: string;
}

/** ProjectQueryParameters.SortFields */
export const PROJECT_SORT_FIELDS = ['name', 'startDate', 'endDate', 'createdAt'] as const;
export type ProjectSortField = (typeof PROJECT_SORT_FIELDS)[number];

export interface ProjectQueryParameters extends PageQuery<ProjectSortField> {
  ownerId?: string;
  startDateFrom?: DateOnlyString;
  startDateTo?: DateOnlyString;
}
