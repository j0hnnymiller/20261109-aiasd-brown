export const e2eServerTopology: {
  readonly frontend: {
    readonly port: 4173;
    readonly origin: 'https://localhost:4173';
    readonly reuseExistingServer: false;
  };
  readonly api: {
    readonly port: 4174;
    readonly url: 'https://localhost:4174';
    readonly reuseExistingServer: false;
  };
  readonly development: {
    readonly frontendPort: 5173;
    readonly apiPort: 5001;
  };
};

export const playwrightOutputDirectory: '/tmp/task-manager-playwright-results';

export const credentialCaptureSettings: {
  readonly trace: 'off';
  readonly screenshot: 'off';
  readonly video: 'off';
  readonly pageSnapshot: 'off';
};

export function effectiveTaskManagerConnection(
  environment: Record<string, string | undefined>,
): string | undefined;

export function parsePostgresConnection(connectionString: string): {
  host: string | undefined;
  port: number;
  database: string | undefined;
  user: string | undefined;
  password: string | undefined;
};

export function createApiChildEnvironment(
  source: Record<string, string | undefined>,
): Record<string, string | undefined>;

export function ensurePortsAvailable(
  ports: readonly number[],
  isListening?: (port: number) => Promise<boolean>,
): Promise<void>;
