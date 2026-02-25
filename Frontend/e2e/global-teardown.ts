import { FullConfig } from '@playwright/test';

/**
 * Global teardown for E2E tests.
 * Cleanup any resources created during tests.
 */
async function globalTeardown(config: FullConfig): Promise<void> {
  console.log('E2E tests completed, cleaning up...');
  // Add any cleanup logic here if needed
}

export default globalTeardown;
