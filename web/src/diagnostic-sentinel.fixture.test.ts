import { describe, expect, it, vi } from 'vitest';

describe('isolated synthetic setup-mock assertion fixture', () => {
  it('fails on a safe projection without printing the mocked credential argument', () => {
    if (process.env.TASK_MANAGER_DIAGNOSTIC_FIXTURE !== '1') {
      return;
    }

    const syntheticCredential = ['SYNTHETIC', 'VITEST', 'CREDENTIAL', 'SENTINEL'].join('-');
    const setupRequest = vi.fn((body: string) => body.includes('Secure'));
    const secureCookie = setupRequest(`device=${syntheticCredential}; HttpOnly; Path=/`);
    expect(secureCookie).toBe(true);
  });
});
