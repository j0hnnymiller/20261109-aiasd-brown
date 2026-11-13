import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    environmentOptions: {
      jsdom: {
        url: 'https://localhost/',
      },
    },
    include: [
      'src/App.test.tsx',
      'src/diagnostic-safety.regression.test.ts',
      'src/configuration.regression.test.ts',
      'src/api.regression.test.ts',
    ],
    setupFiles: ['./src/test-setup.ts'],
    passWithNoTests: false,
    clearMocks: true,
    restoreMocks: true,
  },
});
