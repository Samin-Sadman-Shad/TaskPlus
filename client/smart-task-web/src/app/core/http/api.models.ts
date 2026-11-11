/** Field name (camelCase) → messages, as sent in BaseResponse.errors on a 400. */
export type ProblemFieldErrors = Record<string, string[]>;

/** Stable machine-readable codes from SmartTask.Application ErrorCodes. */
export type ApiErrorCode =
  | 'AUTH_REQUIRED'
  | 'TOKEN_EXPIRED'
  | 'TOKEN_INVALID'
  | 'INSUFFICIENT_ROLE'
  | 'RESOURCE_FORBIDDEN'
  | 'PASSWORD_CHANGE_REQUIRED'
  | 'ACCOUNT_INACTIVE';

/** The API's response envelope (BaseResponse / BaseResponse&lt;T&gt;); nulls are omitted by the server. */
export interface BaseResponse<T = never> {
  statusCode: number;
  success: boolean;
  message?: string;
  errorCode?: ApiErrorCode;
  errors?: ProblemFieldErrors;
  traceId?: string;
  data?: T;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export type SortDirection = 'Asc' | 'Desc';

/** Mirrors PageQuery. Sort fields are whitelisted per resource. */
export interface PageQuery<TSort extends string = string> {
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  sortBy?: TSort;
  sortDirection?: SortDirection;
}

/** ISO date without time (C# DateOnly), e.g. "2026-11-11". */
export type DateOnlyString = string;
/** ISO date-time with offset (C# DateTimeOffset). */
export type DateTimeString = string;
