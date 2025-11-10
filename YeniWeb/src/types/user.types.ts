// User Types

export type UserRight = 'ViewDevices' | 'ExecuteCommands' | 'ManageDevices' | 'All';

export interface User {
  id: string;
  username: string;
  email?: string;
  fullName?: string;
  rights: UserRight;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  token: string;
  user: User;
  expiresAt: string;
}

export interface CreateUserRequest {
  username: string;
  password: string;
  email?: string;
  rights: UserRight;
  isActive?: boolean;
}

export interface UpdateUserRequest {
  username?: string;
  password?: string;
  email?: string;
  rights?: UserRight;
  isActive?: boolean;
}
