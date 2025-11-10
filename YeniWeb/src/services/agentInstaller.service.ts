import { apiService } from './api.service';

export interface AgentInstallerRequest {
  deviceName?: string;
  groupName?: string;
  enrollmentKey?: string;
}

export interface GpoPackageRequest {
  domainName: string;
  groupName?: string;
}

function buildParams(params: Record<string, string | undefined>) {
  const searchParams = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value && value.trim().length > 0) {
      searchParams.append(key, value.trim());
    }
  });
  return searchParams;
}

export const agentInstallerService = {
  async downloadWindowsInstaller(request: AgentInstallerRequest): Promise<Blob> {
    const params = buildParams({
      deviceName: request.deviceName,
      groupName: request.groupName,
      enrollmentKey: request.enrollmentKey,
    });

    return apiService.getBlob('/api/agentinstaller/download/windows', params);
  },

  async downloadGpoPackage(request: GpoPackageRequest): Promise<Blob> {
    const params = buildParams({
      domainName: request.domainName,
      groupName: request.groupName,
    });

    return apiService.getBlob('/api/agentinstaller/download/gpo', params);
  },
};

export default agentInstallerService;
