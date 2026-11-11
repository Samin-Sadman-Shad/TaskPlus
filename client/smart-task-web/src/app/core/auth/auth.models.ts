import { DateTimeString } from '../http/api.models';
import { UserRole } from '../models/enums';

export interface UserDto {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  lastLoginAt?: DateTimeString;
  mustChangePassword: boolean;
  createdAt: DateTimeString;
}

export interface UserSummaryDto {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface AuthResponseDto {
  accessToken: string;
  accessTokenExpiresAt: DateTimeString;
  user: UserDto;
}

export interface LoginRequestDto {
  email: string;
  password: string;
}

export interface RegisterRequestDto {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface ChangePasswordDto {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}
