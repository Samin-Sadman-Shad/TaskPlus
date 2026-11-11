import { AuthResponseDto, UserDto } from '../app/core/auth/auth.models';
import { BaseResponse } from '../app/core/http/api.models';
import { UserRole } from '../app/core/models/enums';

export function makeUser(overrides: Partial<UserDto> = {}): UserDto {
  return {
    id: 'u-1',
    firstName: 'Ada',
    lastName: 'Lovelace',
    fullName: 'Ada Lovelace',
    email: 'ada@example.com',
    role: 'TeamMember' satisfies UserRole,
    isActive: true,
    mustChangePassword: false,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

export function makeSession(token = 'access-1', user: UserDto = makeUser()): AuthResponseDto {
  return {
    accessToken: token,
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user,
  };
}

export function ok<T>(data: T, statusCode = 200): BaseResponse<T> {
  return { statusCode, success: true, data };
}

export function fail(statusCode: number, message: string, extra: Partial<BaseResponse> = {}): BaseResponse {
  return { statusCode, success: false, message, ...extra };
}
