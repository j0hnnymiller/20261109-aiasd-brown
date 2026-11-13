import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const certificateDirectory = process.env.TASK_MANAGER_CERT_DIR ?? resolve('.certs');
const useHttps = process.env.VITE_USE_HTTPS === 'true';
const apiTarget = process.env.TASK_MANAGER_API_TARGET ?? 'https://localhost:5001';

export default defineConfig({
  plugins: [react()],
  server: {
    host: 'localhost',
    strictPort: true,
    https: useHttps
      ? {
          key: readFileSync(resolve(certificateDirectory, 'localhost.key')),
          cert: readFileSync(resolve(certificateDirectory, 'localhost.crt')),
        }
      : undefined,
    proxy: {
      '/api': {
        target: apiTarget,
        secure: true,
      },
    },
  },
});
