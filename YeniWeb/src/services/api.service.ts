import axios, { AxiosError } from 'axios';
import type { AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import config from '../utils/config';

class ApiService {
  private api: AxiosInstance;

  constructor() {
    this.api = axios.create({
      baseURL: config.apiUrl,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    });

    // Request interceptor
    this.api.interceptors.request.use(
      (config: InternalAxiosRequestConfig) => {
        const token = localStorage.getItem('token');
        if (token && config.headers) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
      },
      (error: AxiosError) => {
        return Promise.reject(error);
      }
    );

    // Response interceptor
    this.api.interceptors.response.use(
      (response) => response,
      async (error: AxiosError) => {
        if (error.response?.status === 401) {
          // Token expired or invalid
          localStorage.removeItem('token');
          localStorage.removeItem('user');
          window.location.href = '/login';
        }
        return Promise.reject(error);
      }
    );
  }

  // Generic request methods
  async get<T>(url: string, params?: Record<string, unknown> | URLSearchParams): Promise<T> {
    const response = await this.api.get<T>(url, params ? { params } : undefined);
    return response.data;
  }

  async post<T, D = unknown>(url: string, data?: D): Promise<T> {
    const response = await this.api.post<T>(url, data);
    return response.data;
  }

  async getBlob(url: string, params?: Record<string, unknown> | URLSearchParams): Promise<Blob> {
    const response = await this.api.get<Blob>(url, {
      params,
      responseType: 'blob',
    });
    return response.data;
  }

  async put<T, D = unknown>(url: string, data?: D): Promise<T> {
    const response = await this.api.put<T>(url, data);
    return response.data;
  }

  async delete<T>(url: string, data?: unknown): Promise<T> {
    const response = await this.api.delete<T>(url, data ? { data } : undefined);
    return response.data;
  }

  // Get axios instance for custom requests
  getAxiosInstance(): AxiosInstance {
    return this.api;
  }
}

export const apiService = new ApiService();
export default apiService;

