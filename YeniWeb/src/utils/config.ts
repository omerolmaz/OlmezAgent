export const config = {
  // API URL: Use environment variable or relative path (works with Vite proxy in dev, same-origin in prod)
  apiUrl: import.meta.env.VITE_API_URL || (import.meta.env.DEV ? '/api' : '/api'),
  
  // WebSocket URL: Build dynamically from window.location or use env variable
  wsUrl: import.meta.env.VITE_WS_URL || (() => {
    if (typeof window === 'undefined') return 'ws://localhost:5236/ws';
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const host = import.meta.env.DEV ? 'localhost:5236' : window.location.host;
    return `${protocol}//${host}/ws`;
  })(),
  
  appName: import.meta.env.VITE_APP_NAME || 'OlmezWeb',
  appVersion: import.meta.env.VITE_APP_VERSION || '1.0.0',
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
};

export default config;

