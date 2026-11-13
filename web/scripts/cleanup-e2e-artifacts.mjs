import { rm } from 'node:fs/promises';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';

export default async function cleanupE2eArtifacts() {
  await rm(resolve(tmpdir(), 'task-manager-playwright-results'), { recursive: true, force: true });
}
