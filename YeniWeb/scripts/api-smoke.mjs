#!/usr/bin/env node

const baseUrl = process.env.API_URL || 'http://localhost:5236';
const username = process.env.API_USER || 'admin';
const password = process.env.API_PASS || 'Admin123!';

const TERMINAL_STATUSES = ['Success', 'Failed', 'Error', 'Cancelled', 'Completed'];

const tests = [];
let token = null;

async function request(path, { method = 'GET', body, headers = {}, authorize = true } = {}) {
  const url = `${baseUrl}${path}`;
  const init = {
    method,
    headers: {
      ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(authorize && token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
    body: body ? JSON.stringify(body) : undefined,
  };

  const response = await fetch(url, init);
  if (!response.ok) {
    const errorText = await response.text().catch(() => '');
    throw new Error(`${method} ${path} -> ${response.status} ${response.statusText}${errorText ? ` | ${errorText}` : ''}`);
  }

  if (response.status === 204) return null;

  const text = await response.text();
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

async function waitForCommand(commandId, { timeoutMs = 30000, intervalMs = 1000 } = {}) {
  const started = Date.now();
  let attempt = 0;
  while (Date.now() - started < timeoutMs) {
    const command = await request(`/api/commands/${commandId}`);
    if (command && TERMINAL_STATUSES.includes(command.status)) {
      return command;
    }
    attempt += 1;
    await new Promise((resolve) => setTimeout(resolve, intervalMs + Math.min(attempt * 100, 1000)));
  }
  throw new Error(`Command ${commandId} timed out after ${timeoutMs}ms`);
}

async function runTest(name, fn) {
  const start = Date.now();
  const entry = { name, success: false, durationMs: 0 };

  try {
    const detail = await fn();
    entry.success = true;
    if (detail !== undefined) {
      entry.detail = typeof detail === 'string' ? detail : JSON.stringify(detail);
    }
  } catch (error) {
    entry.error = error instanceof Error ? error.message : String(error);
    if (error instanceof Error && error.stack) {
      entry.stack = error.stack;
    }
  } finally {
    entry.durationMs = Date.now() - start;
    tests.push(entry);
    const status = entry.success ? 'PASS' : 'FAIL';
    console.log(`[${status}] ${name} (${entry.durationMs}ms)`);
    if (entry.success && entry.detail) {
      console.log(`       ${entry.detail}`);
    } else if (!entry.success && entry.error) {
      console.log(`       ${entry.error}`);
    }
  }
}

async function main() {
  console.log(`API smoke tests -> ${baseUrl}`);

  let devices = [];
  let targetDeviceId = null;
  let userId = null;

  await runTest('Login', async () => {
    const response = await request('/api/users/login', {
      method: 'POST',
      body: { username, password },
      authorize: false,
    });

    if (!response?.token) {
      throw new Error('Token missing from login response');
    }
    token = response.token;
    userId = response?.user?.id ?? null;
    return `token received, user ${response?.user?.username ?? 'unknown'}`;
  });

  await runTest('GET /api/devices', async () => {
    devices = await request('/api/devices');
    if (!Array.isArray(devices)) {
      throw new Error('Devices response is not an array');
    }
    if (devices.length === 0) {
      throw new Error('No devices returned from API');
    }
    targetDeviceId = devices[0].id;
    return `device count ${devices.length}, selected ${targetDeviceId}`;
  });

  await runTest('GET /api/devices/{id}', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    const device = await request(`/api/devices/${targetDeviceId}`);
    if (!device || device.id !== targetDeviceId) {
      throw new Error('Device payload mismatch');
    }
    return `status ${device.status}`;
  });

  await runTest('GET /api/commands/device/{id}', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    const commandHistory = await request(`/api/commands/device/${targetDeviceId}`);
    if (!Array.isArray(commandHistory)) {
      throw new Error('Command history is not an array');
    }
    return `commands ${commandHistory.length}`;
  });

  await runTest('POST /api/diagnostics/status/{id}', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    const { id: commandId } = await request(`/api/diagnostics/status/${targetDeviceId}`, {
      method: 'POST',
      body: {},
    });
    if (!commandId) throw new Error('Diagnostics command not started');
    const result = await waitForCommand(commandId, { timeoutMs: 45000 });
    return `command status ${result.status}`;
  });

  await runTest('POST /api/security/status/{id}', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    const { id: commandId } = await request(`/api/security/status/${targetDeviceId}`, {
      method: 'POST',
      body: {},
    });
    if (!commandId) throw new Error('Security command not started');
    const result = await waitForCommand(commandId, { timeoutMs: 45000 });
    return `command status ${result.status}`;
  });

  await runTest('POST /api/remoteops/ls/{id}', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    const { id: commandId } = await request(`/api/remoteops/ls/${targetDeviceId}`, {
      method: 'POST',
      body: { path: 'C:\\\\' },
    });
    if (!commandId) throw new Error('Remote ops command not started');
    const result = await waitForCommand(commandId, { timeoutMs: 20000 });
    let entryCount = 'n/a';
    if (result.result) {
      try {
        const parsed = JSON.parse(result.result);
        if (Array.isArray(parsed)) {
          entryCount = parsed.length;
        }
      } catch {
        entryCount = 'parse-error';
      }
    }
    return `command status ${result.status}, entries ${entryCount}`;
  });

  await runTest('GET /api/sessions/device/{id}', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    const sessions = await request(`/api/sessions/device/${targetDeviceId}`);
    if (!Array.isArray(sessions)) {
      throw new Error('Sessions response is not an array');
    }
    return `sessions ${sessions.length}`;
  });

  await runTest('GET /api/inventory/devices/{id}', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    try {
      const inventory = await request(`/api/inventory/devices/${targetDeviceId}`);
      const keys = inventory ? Object.keys(inventory) : [];
      return `inventory fields ${keys.length}`;
    } catch (error) {
      if (error.message && error.message.includes('404')) {
        return 'inventory not yet collected';
      }
      throw error;
    }
  });

  await runTest('POST /api/inventory/devices/{id}/refresh', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    if (!userId) throw new Error('User id missing');
    const response = await request(`/api/inventory/devices/${targetDeviceId}/refresh`, {
      method: 'POST',
      body: { userId },
    });
    if (!response?.commandId) {
      throw new Error('No command id returned');
    }
    const result = await waitForCommand(response.commandId, { timeoutMs: 120000 });
    return `command status ${result.status}`;
  });

  await runTest('GET /api/inventory/devices/{id}/software', async () => {
    if (!targetDeviceId) throw new Error('No device selected');
    const software = await request(`/api/inventory/devices/${targetDeviceId}/software`);
    const count = Array.isArray(software) ? software.length : 0;
    return `software count ${count}`;
  });

  console.log('\nTest summary:');
  tests.forEach((t) => {
    const status = t.success ? '[OK]' : '[X]';
    const detail = t.success ? (t.detail ?? '') : (t.error ?? '');
    console.log(` ${status} ${t.name}${detail ? ` -> ${detail}` : ''}`);
  });

  const failed = tests.filter((t) => !t.success);
  if (failed.length) {
    console.error(`\n${failed.length} test(s) failed.`);
    process.exit(1);
  }

  console.log('\nAll tests passed.');
}

main().catch((error) => {
  console.error('Unexpected error', error);
  process.exit(1);
});
