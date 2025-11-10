import { apiService } from './api.service';
import type { CreateUserRequest, UpdateUserRequest, User } from '../types/user.types';

type ApiEnvelope<T> = {
  success: boolean;
  data: T;
  error?: string;
};

function unwrap<T>(response: T | ApiEnvelope<T>): T {
  if (typeof response === 'object' && response !== null && 'success' in (response as ApiEnvelope<T>)) {
    const envelope = response as ApiEnvelope<T>;
    if (!envelope.success) {
      throw new Error(envelope.error ?? 'User request failed.');
    }
    return envelope.data;
  }
  return response as T;
}

export const userService = {
  async list(): Promise<User[]> {
    const response = await apiService.get<User[] | ApiEnvelope<User[]>>('/api/users');
    return unwrap(response);
  },

  async create(payload: CreateUserRequest): Promise<User> {
    const response = await apiService.post<User | ApiEnvelope<User>>('/api/users', payload);
    return unwrap(response);
  },

  async update(id: string, payload: UpdateUserRequest): Promise<User> {
    const response = await apiService.put<User | ApiEnvelope<User>>(`/api/users/${id}`, payload);
    return unwrap(response);
  },

  async remove(id: string): Promise<void> {
    await apiService.delete<void>(`/api/users/${id}`);
  },

  async setActive(id: string, isActive: boolean): Promise<User> {
    const response = await apiService.post<User | ApiEnvelope<User>>(`/api/users/${id}/status`, { isActive });
    return unwrap(response);
  },
};

export default userService;
