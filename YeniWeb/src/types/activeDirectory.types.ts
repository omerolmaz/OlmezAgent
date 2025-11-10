export interface ADUser {
  distinguishedName: string;
  displayName: string;
  email?: string | null;
  username: string;
  enabled: boolean;
  lastLogon?: string | null;
  createdAt?: string | null;
}

export interface ADComputer {
  distinguishedName: string;
  name: string;
  operatingSystem?: string | null;
  operatingSystemVersion?: string | null;
  lastLogonTimestamp?: string | null;
  createdAt?: string | null;
}

export interface ADDomainInfo {
  name: string;
  forestName: string;
  domainMode: string;
  pdcRoleOwner: string;
  ridRoleOwner: string;
  infrastructureRoleOwner: string;
  domainControllers: string[];
}
