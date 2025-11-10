import apiService from './api.service';
import type { LoginRequest, LoginResponse, User } from '../types/user.types';

class AuthService {
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    // Backend directly returns LoginResponse (not wrapped in ApiResponse)
    const response = await apiService.post<LoginResponse>(
      '/api/users/login',
      credentials
    );
    
    if (response.token && response.user) {
      localStorage.setItem('token', response.token);
      localStorage.setItem('user', JSON.stringify(response.user));
      return response;
    }
    
    throw new Error('Login failed');
  }

  async logout(): Promise<void> {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
  }

  getToken(): string | null {
    return localStorage.getItem('token');
  }

  getUser(): User | null {
    const userStr = localStorage.getItem('user');
    if (!userStr) return null;
    try {
      return JSON.parse(userStr);
    } catch {
      return null;
    }
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }
}

export const authService = new AuthService();
export default authService;

