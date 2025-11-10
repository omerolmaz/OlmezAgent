export const config = {
  apiUrl: import.meta.env.VITE_API_URL || 'http://localhost:5236',
  wsUrl: import.meta.env.VITE_WS_URL || 'ws://localhost:5236/ws',
  appName: import.meta.env.VITE_APP_NAME || 'OlmezWeb',
  appVersion: import.meta.env.VITE_APP_VERSION || '1.0.0',
  isDevelopment: import.meta.env.DEV,
  isProduction: import.meta.env.PROD,
};

export default config;
