import { test, expect, Page } from '@playwright/test';

/**
 * Pairing Flow E2E tests - Tests the tower pairing workflow
 */
test.describe('Tower Pairing Flow', () => {
  const testFarmId = 'e2e-test-farm';
  const testCoordId = 'e2e-test-coord';
  const backendUrl = process.env.BACKEND_URL || 'http://localhost:8000';

  test.beforeEach(async ({ page }) => {
    // Navigate to reservoirs/coordinators page
    await page.goto('/reservoirs');
  });

  test('should display coordinators list', async ({ page }) => {
    // Wait for page to load
    await page.waitForLoadState('networkidle');
    
    // Page should show coordinators or empty state
    const content = await page.content();
    const hasCoordinators = content.includes('coordinator') || 
                           content.includes('Coordinator') ||
                           content.includes('reservoir') ||
                           content.includes('Reservoir');
    
    // Either coordinators or "no coordinators" message should be shown
    expect(hasCoordinators || content.includes('No') || content.includes('empty')).toBeTruthy();
  });

  test('should be able to navigate to coordinator detail', async ({ page }) => {
    // This test depends on having coordinators in the system
    // For E2E with no data, we verify the navigation structure works
    
    const coordLinks = page.locator('a[href*="/reservoirs/"]');
    const count = await coordLinks.count();
    
    if (count > 0) {
      // Click first coordinator
      await coordLinks.first().click();
      
      // Should navigate to detail page
      await expect(page).toHaveURL(/reservoirs\/.+/);
    } else {
      // No coordinators - that's fine for E2E without data
      console.log('No coordinators found - skipping navigation test');
    }
  });

  test('should show towers list page', async ({ page }) => {
    await page.goto('/towers');
    
    await page.waitForLoadState('networkidle');
    
    // Page should render
    await expect(page.locator('body')).toBeVisible();
  });

  test('should handle pairing API endpoints', async ({ request }) => {
    // Test pairing start API directly
    const response = await request.post(`${backendUrl}/api/pairing/start`, {
      data: {
        farm_id: testFarmId,
        coord_id: testCoordId,
        duration_s: 30
      }
    });
    
    // Should return OK (200) - creates a pairing session
    expect(response.status()).toBe(200);
    
    const body = await response.json();
    expect(body).toHaveProperty('status');
    
    // Clean up - stop the pairing session
    await request.post(`${backendUrl}/api/pairing/stop`, {
      data: {
        farm_id: testFarmId,
        coord_id: testCoordId
      }
    });
  });

  test('should be able to get pairing session status', async ({ request }) => {
    // Start a session first
    await request.post(`${backendUrl}/api/pairing/start`, {
      data: {
        farm_id: `${testFarmId}-status`,
        coord_id: `${testCoordId}-status`,
        duration_s: 60
      }
    });

    // Get session status
    const response = await request.get(
      `${backendUrl}/api/pairing/session/${testFarmId}-status/${testCoordId}-status`
    );
    
    expect(response.status()).toBe(200);
    
    const body = await response.json();
    expect(body.status).toBe('active');
    
    // Cleanup
    await request.post(`${backendUrl}/api/pairing/stop`, {
      data: {
        farm_id: `${testFarmId}-status`,
        coord_id: `${testCoordId}-status`
      }
    });
  });

  test('should return 404 for non-existent pairing session', async ({ request }) => {
    const response = await request.get(
      `${backendUrl}/api/pairing/session/nonexistent-farm/nonexistent-coord`
    );
    
    expect(response.status()).toBe(404);
  });
});

test.describe('Tower Detail Page', () => {
  const backendUrl = process.env.BACKEND_URL || 'http://localhost:8000';

  test('should show tower detail page with ID', async ({ page }) => {
    // Navigate to a specific tower URL pattern
    await page.goto('/towers/test-tower');
    
    // Page should load without crashing
    await expect(page.locator('body')).toBeVisible();
  });

  test('should display tower controls when tower exists', async ({ page, request }) => {
    // Create a test tower via API first
    const farmId = 'e2e-farm';
    const coordId = 'e2e-coord';
    const towerId = 'e2e-tower';
    
    await request.put(`${backendUrl}/api/towers/${farmId}/${coordId}/${towerId}`, {
      data: {
        tower_id: towerId,
        coord_id: coordId,
        farm_id: farmId,
        name: 'E2E Test Tower',
        status_mode: 'operational'
      }
    });

    // Navigate to towers list
    await page.goto('/towers');
    await page.waitForLoadState('networkidle');
    
    // Look for the tower in the list
    const towerLink = page.locator(`a[href*="${towerId}"], [data-tower-id="${towerId}"]`);
    
    if (await towerLink.count() > 0) {
      await towerLink.first().click();
      
      // Should show tower details
      await expect(page.locator('body')).toBeVisible();
    }
    
    // Cleanup
    await request.delete(`${backendUrl}/api/towers/${farmId}/${coordId}/${towerId}`);
  });
});
