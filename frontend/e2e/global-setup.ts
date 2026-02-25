import { FullConfig } from '@playwright/test';

/**
 * Global setup for E2E tests.
 * Waits for the backend and frontend to be ready before running tests.
 */
async function globalSetup(config: FullConfig): Promise<void> {
  const backendUrl = process.env.BACKEND_URL || 'http://localhost:8000';
  const frontendUrl = process.env.FRONTEND_URL || 'http://localhost:4200';

  console.log('Waiting for services to be ready...');
  console.log(`Backend URL: ${backendUrl}`);
  console.log(`Frontend URL: ${frontendUrl}`);

  // Wait for backend health check
  await waitForService(`${backendUrl}/health/live`, 'Backend', 60);
  
  // Wait for frontend to be serving
  await waitForService(frontendUrl, 'Frontend', 60);

  console.log('All services ready, starting tests...');
}

async function waitForService(url: string, name: string, maxRetries: number): Promise<void> {
  for (let i = 0; i < maxRetries; i++) {
    try {
      const response = await fetch(url);
      if (response.ok || response.status === 200) {
        console.log(`✓ ${name} is ready at ${url}`);
        return;
      }
    } catch (error) {
      // Service not ready yet
    }

    if (i < maxRetries - 1) {
      console.log(`Waiting for ${name}... (${i + 1}/${maxRetries})`);
      await new Promise(resolve => setTimeout(resolve, 2000));
    }
  }

  throw new Error(`${name} at ${url} did not become ready after ${maxRetries} attempts`);
}

export default globalSetup;
