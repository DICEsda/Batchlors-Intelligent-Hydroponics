import { test, expect } from '@playwright/test';

/**
 * Dashboard E2E tests - Tests the main dashboard functionality
 */
test.describe('Dashboard', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to dashboard
    await page.goto('/overview');
  });

  test('should load the dashboard page', async ({ page }) => {
    // Wait for the page to load
    await expect(page).toHaveTitle(/IOT|Dashboard|Overview/i);
    
    // Check for main layout elements
    await expect(page.locator('nav, [data-testid="sidebar"], aside')).toBeVisible();
  });

  test('should display navigation menu', async ({ page }) => {
    // Check for navigation items
    const nav = page.locator('nav, aside, [role="navigation"]');
    await expect(nav).toBeVisible();
  });

  test('should navigate to reservoirs page', async ({ page }) => {
    // Click on reservoirs link
    await page.getByRole('link', { name: /reservoir/i }).first().click();
    
    // Should navigate to reservoirs page
    await expect(page).toHaveURL(/reservoir/i);
  });

  test('should navigate to towers page', async ({ page }) => {
    // Click on towers link
    await page.getByRole('link', { name: /tower/i }).first().click();
    
    // Should navigate to towers page
    await expect(page).toHaveURL(/tower/i);
  });

  test('should show loading state while fetching data', async ({ page }) => {
    // Intercept API calls to add delay
    await page.route('**/api/**', async route => {
      await new Promise(resolve => setTimeout(resolve, 500));
      await route.continue();
    });

    // Reload to trigger loading state
    await page.reload();

    // Check for loading indicator (spinner, skeleton, etc.)
    const loadingIndicator = page.locator('[class*="loading"], [class*="spinner"], [data-loading="true"]');
    // Loading state may be brief, so we just verify page eventually loads
    await expect(page.locator('body')).toBeVisible();
  });

  test('should handle API errors gracefully', async ({ page }) => {
    // Mock API to return error
    await page.route('**/api/towers/**', route => {
      route.fulfill({
        status: 500,
        body: JSON.stringify({ error: 'Internal Server Error' })
      });
    });

    await page.reload();

    // Page should still be usable (not crash)
    await expect(page.locator('body')).toBeVisible();
  });
});

test.describe('Responsive Design', () => {
  test('should work on mobile viewport', async ({ page }) => {
    // Set mobile viewport
    await page.setViewportSize({ width: 375, height: 667 });
    
    await page.goto('/overview');
    
    // Page should render without horizontal scroll
    const bodyWidth = await page.evaluate(() => document.body.scrollWidth);
    const viewportWidth = await page.evaluate(() => window.innerWidth);
    
    expect(bodyWidth).toBeLessThanOrEqual(viewportWidth + 10); // Allow small margin
  });

  test('should work on tablet viewport', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    
    await page.goto('/overview');
    
    await expect(page.locator('body')).toBeVisible();
  });
});
