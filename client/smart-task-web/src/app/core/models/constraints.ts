/** Mirrors SmartTask.Domain/Constants so client validation matches the server. */
export const UserConstraints = {
  firstNameMaxLength: 100,
  lastNameMaxLength: 100,
  emailMaxLength: 256,
  passwordMinLength: 8,
  passwordMaxLength: 128,
} as const;

export const ProjectConstraints = {
  nameMinLength: 3,
  nameMaxLength: 150,
  descriptionMaxLength: 2000,
  /** CreateProjectDtoValidator allows a start date up to this many days in the past. */
  startDateToleranceDays: 1,
} as const;

export const TaskItemConstraints = {
  titleMinLength: 3,
  titleMaxLength: 200,
  descriptionMaxLength: 4000,
} as const;

export const AiConstraints = {
  inputMinLength: 3,
  inputMaxLength: 2000,
} as const;

export const PagingConstraints = {
  defaultPageSize: 10,
  maxPageSize: 100,
  searchTermMaxLength: 100,
  pageSizeOptions: [10, 25, 50],
} as const;

export const DashboardConstraints = {
  upcomingDayOptions: [7, 14, 30],
  defaultUpcomingDays: 7,
} as const;
