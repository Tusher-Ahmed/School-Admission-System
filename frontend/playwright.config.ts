import { defineConfig, devices } from '@playwright/test';

// End-to-end tests: a real browser drives the real UI, which talks to the real backend and the real PostgreSQL
// database (connection from backend/.env or ConnectionStrings__Default). Data is NOT deleted afterwards, so it can be
// inspected in pgAdmin. Separate ports keep this away from a backend/frontend you may already have running.
export const API_PORT = 8090;
export const WEB_PORT = 5180;
// Short hold so the expiry scenario takes seconds, not 10 minutes.
export const HOLD_SECONDS = 15;

export default defineConfig({
  testDir: './e2e',
  timeout: 120_000,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    trace: 'on',
    video: 'on',
    screenshot: 'on',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: [
    {
      command: `dotnet run --project src/SchoolAdmission.Api --urls http://localhost:${API_PORT}`,
      cwd: '../backend',
      url: `http://localhost:${API_PORT}/health`,
      env: { HOLD_DURATION_SECONDS: String(HOLD_SECONDS) },
      timeout: 180_000,
      reuseExistingServer: false,
    },
    {
      command: `npm run dev -- --port ${WEB_PORT} --strictPort`,
      url: `http://localhost:${WEB_PORT}`,
      env: { VITE_API_BASE_URL: `http://localhost:${API_PORT}` },
      timeout: 60_000,
      reuseExistingServer: false,
    },
  ],
});
