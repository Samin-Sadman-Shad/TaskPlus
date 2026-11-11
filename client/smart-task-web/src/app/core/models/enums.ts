/** String unions matching the API's enums (the API serialises enums by name). */
export type UserRole = 'Admin' | 'ProjectManager' | 'TeamMember';
export type TaskItemStatus = 'ToDo' | 'InProgress' | 'Completed' | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export const USER_ROLES: readonly UserRole[] = ['Admin', 'ProjectManager', 'TeamMember'];
export const TASK_STATUSES: readonly TaskItemStatus[] = ['ToDo', 'InProgress', 'Completed', 'Cancelled'];
export const TASK_PRIORITIES: readonly TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

/** Mirrors TaskItem.AllowedTransitions in SmartTask.Domain. The server still validates every change. */
export const TASK_STATUS_TRANSITIONS: Readonly<Record<TaskItemStatus, readonly TaskItemStatus[]>> = {
  ToDo: ['InProgress', 'Completed', 'Cancelled'],
  InProgress: ['ToDo', 'Completed', 'Cancelled'],
  Completed: ['InProgress'],
  Cancelled: ['ToDo'],
};
