import { spawn } from 'node:child_process';
import cleanupE2eArtifacts from './cleanup-e2e-artifacts.mjs';

try {
  const exitCode = await new Promise((resolve, reject) => {
    const playwright = spawn('playwright', ['test', ...process.argv.slice(2)], { stdio: 'inherit' });
    playwright.once('error', reject);
    playwright.once('exit', (code) => resolve(code ?? 1));
  });
  process.exitCode = exitCode;
} finally {
  await cleanupE2eArtifacts();
}
