import { cleanup, configure } from '@testing-library/react';
import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';

configure({
  getElementError: (message) => new Error(message ?? 'An element query failed.'),
});

afterEach(() => {
  cleanup();
  localStorage.clear();
  document.cookie = '';
});
