import { apiService } from './api.service';
import type { ADDomainInfo, ADComputer, ADUser } from '../types/activeDirectory.types';

export const activeDirectoryService = {
  async testConnection(): Promise<{ connected: boolean; message: string }> {
    return apiService.get('/api/activedirectory/test');
  },

  async getDomainInfo(): Promise<ADDomainInfo> {
    return apiService.get('/api/activedirectory/domain-info');
  },

  async getUsers(filter?: string): Promise<ADUser[]> {
    return apiService.get('/api/activedirectory/users', filter ? { filter } : undefined);
  },

  async getComputers(filter?: string): Promise<ADComputer[]> {
    return apiService.get('/api/activedirectory/computers', filter ? { filter } : undefined);
  },
};

export default activeDirectoryService;
