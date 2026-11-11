import { UserSummaryDto } from '../../../core/auth/auth.models';
import { DateOnlyString, DateTimeString, PageQuery } from '../../../core/http/api.models';
import { TaskItemStatus, TaskPriority } from '../../../core/models/enums';

export interface TaskDto {
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  description?: string;
  status: TaskItemStatus;
  priority: TaskPriority;
  dueDate?: DateOnlyString;
  isOverdue: boolean;
  assignee?: UserSummaryDto;
  completedAt?: DateTimeString;
  createdAt: DateTimeString;
  updatedAt?: DateTimeString;
}

export interface TaskSummaryDto {
  id: string;
  projectId: string;
  title: string;
  status: TaskItemStatus;
  priority: TaskPriority;
  dueDate?: DateOnlyString;
  isOverdue: boolean;
  assigneeName?: string;
}

export interface CreateTaskDto {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: DateOnlyString | null;
  assigneeId: string | null;
}

export interface UpdateTaskDto {
  title: string;
  description: string | null;
  priority: TaskPriority;
  dueDate: DateOnlyString | null;
}

export interface UpdateTaskStatusDto {
  status: TaskItemStatus;
}

export interface AssignTaskDto {
  assigneeId: string | null;
}

/** TaskQueryParameters.SortFields */
export const TASK_SORT_FIELDS = ['title', 'dueDate', 'priority', 'status', 'createdAt'] as const;
export type TaskSortField = (typeof TASK_SORT_FIELDS)[number];

export interface TaskQueryParameters extends PageQuery<TaskSortField> {
  status?: TaskItemStatus;
  priority?: TaskPriority;
  assigneeId?: string;
  dueDateFrom?: DateOnlyString;
  dueDateTo?: DateOnlyString;
  isOverdue?: boolean;
}

export interface ImproveDescriptionRequestDto {
  title: string | null;
  description: string;
}

export interface ImproveDescriptionResponseDto {
  originalDescription: string;
  improvedDescription: string;
  suggestions: string[];
}
