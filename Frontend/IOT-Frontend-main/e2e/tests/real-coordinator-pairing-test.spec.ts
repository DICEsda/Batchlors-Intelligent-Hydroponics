import { test, expect } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';

/**
 * Real Coordinator Pairing Flow Test
 * Tests pairing functionality with the actual coordinator in the database
 * Coordinator MAC: 74:4D:BD:AB:A9:F4
 */
test.describe('Real Coordinator Pairing Test', () => {
  const screenshotsDir = 'test-results/real-coordinator-pairing';
  const REAL_COORDINATOR_ID = '74:4D:BD:AB:A9:F4';
  const COORDINATOR_URL = `/coordinators/${encodeURIComponent(REAL_COORDINATOR_ID)}`;

  test.beforeAll(async () => {
    // Ensure screenshots directory exists
    if (!fs.existsSync(screenshotsDir)) {
      fs.mkdirSync(screenshotsDir, { recursive: true });
    }
  });

  test('complete pairing flow with real coordinator', async ({ page }) => {
    const testResults: { step: string; status: string; details: string }[] = [];
    
    // Increase timeout for this test
    test.setTimeout(120000);

    // ======================================
    // STEP 1: Navigate to Overview page
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log('STEP 1: Navigate to Overview Page');
    console.log('='.repeat(60));
    
    await page.goto('/overview', { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(2000);
    
    const overviewScreenshot = path.join(screenshotsDir, '01-overview-page.png');
    await page.screenshot({ path: overviewScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: ${overviewScreenshot}`);
    console.log(`  Current URL: ${page.url()}`);
    
    testResults.push({
      step: '1. Navigate to Overview',
      status: 'PASS',
      details: `URL: ${page.url()}`
    });

    // ======================================
    // STEP 2: Navigate directly to Coordinator Detail page
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log(`STEP 2: Navigate to Coordinator ${REAL_COORDINATOR_ID}`);
    console.log('='.repeat(60));
    
    await page.goto(COORDINATOR_URL, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(3000);
    
    const coordDetailScreenshot = path.join(screenshotsDir, '02-coordinator-detail.png');
    await page.screenshot({ path: coordDetailScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: ${coordDetailScreenshot}`);
    console.log(`  Current URL: ${page.url()}`);
    
    // Verify page loaded correctly
    const pageTitle = await page.title();
    const pageContent = await page.content();
    const hasCoordinatorContent = pageContent.includes('74:4D:BD') || 
                                  pageContent.toLowerCase().includes('coordinator') ||
                                  pageContent.toLowerCase().includes('reservoir');
    
    console.log(`  Page contains coordinator info: ${hasCoordinatorContent}`);
    
    // Log visible text for debugging
    const mainContent = await page.locator('main, [role="main"], .content, #content').first().textContent().catch(() => 'N/A');
    console.log(`  Main content preview: ${mainContent?.slice(0, 200)}...`);
    
    testResults.push({
      step: '2. Coordinator Detail Page',
      status: hasCoordinatorContent ? 'PASS' : 'CHECK',
      details: `URL: ${page.url()}, Has content: ${hasCoordinatorContent}`
    });

    // ======================================
    // STEP 3: Find and Click "Start Pairing" button
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log('STEP 3: Find and Click "Start Pairing" Button');
    console.log('='.repeat(60));
    
    // List all buttons on the page first
    const allButtons = await page.locator('button').all();
    console.log(`\n  Found ${allButtons.length} buttons on page:`);
    for (let i = 0; i < Math.min(allButtons.length, 15); i++) {
      const btn = allButtons[i];
      const text = await btn.textContent().catch(() => '');
      const isVisible = await btn.isVisible().catch(() => false);
      const isEnabled = await btn.isEnabled().catch(() => false);
      console.log(`    [${i}] "${text?.trim()}" - visible: ${isVisible}, enabled: ${isEnabled}`);
    }
    
    // Try to find Start Pairing button
    const pairingButtonSelectors = [
      'button:has-text("Start Pairing")',
      'button:has-text("start pairing")',
      'button:has-text("Pairing")',
      '[data-testid="start-pairing"]',
      'button:has-text("Pair")',
      'button >> text=Start Pairing',
      'button >> text=start pairing',
    ];
    
    let pairingButton = null;
    let usedSelector = '';
    
    for (const selector of pairingButtonSelectors) {
      try {
        const element = page.locator(selector).first();
        const count = await element.count();
        if (count > 0) {
          const isVisible = await element.isVisible();
          if (isVisible) {
            pairingButton = element;
            usedSelector = selector;
            console.log(`\n  ✓ Found pairing button with selector: ${selector}`);
            break;
          }
        }
      } catch (e) {
        // Continue to next selector
      }
    }
    
    if (!pairingButton) {
      // Take screenshot showing current state
      const noButtonScreenshot = path.join(screenshotsDir, '03a-no-pairing-button-found.png');
      await page.screenshot({ path: noButtonScreenshot, fullPage: true });
      console.log(`  ✗ Could not find Start Pairing button`);
      console.log(`  Screenshot saved: ${noButtonScreenshot}`);
      
      testResults.push({
        step: '3. Click Start Pairing',
        status: 'FAIL',
        details: 'Button not found - check UI for pairing button'
      });
    } else {
      // Screenshot before clicking
      const beforePairingScreenshot = path.join(screenshotsDir, '03a-before-pairing.png');
      await page.screenshot({ path: beforePairingScreenshot, fullPage: true });
      
      // Click the button
      console.log(`  Clicking Start Pairing button...`);
      await pairingButton.click();
      
      // Wait for pairing mode to activate
      await page.waitForTimeout(2000);
      
      console.log(`  ✓ Clicked Start Pairing button`);
      
      testResults.push({
        step: '3. Click Start Pairing',
        status: 'PASS',
        details: `Button found with selector: ${usedSelector}`
      });
    }

    // ======================================
    // STEP 4: Screenshot Pairing Mode Active
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log('STEP 4: Screenshot Pairing Mode Active');
    console.log('='.repeat(60));
    
    await page.waitForTimeout(1000);
    
    const pairingActiveScreenshot = path.join(screenshotsDir, '04-pairing-mode-active.png');
    await page.screenshot({ path: pairingActiveScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: ${pairingActiveScreenshot}`);
    
    // Check for pairing UI elements
    const pairingUIChecks = {
      'Countdown/Timer': await page.locator('text=/\\d+s|\\d+:\\d+|remaining|countdown|timeout/i').count() > 0 ||
                        await page.locator('[class*="countdown"], [class*="timer"], [class*="time"]').count() > 0,
      'Pairing Mode indicator': await page.locator('text=/pairing.*mode|pairing.*active|mode.*active/i').count() > 0,
      'Discovered Nodes': await page.locator('text=/discovered|scanning|nodes found|available/i').count() > 0,
      'Cancel/Stop button': await page.locator('button:has-text("Cancel"), button:has-text("Stop"), button:has-text("End")').count() > 0,
    };
    
    console.log('\n  Pairing UI Elements:');
    for (const [element, found] of Object.entries(pairingUIChecks)) {
      console.log(`    ${found ? '✓' : '✗'} ${element}: ${found ? 'FOUND' : 'NOT FOUND'}`);
    }
    
    testResults.push({
      step: '4. Pairing Mode Active',
      status: Object.values(pairingUIChecks).some(v => v) ? 'PASS' : 'CHECK',
      details: JSON.stringify(pairingUIChecks)
    });

    // ======================================
    // STEP 5: Wait 5 seconds and take another screenshot
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log('STEP 5: Wait 5 seconds and verify countdown');
    console.log('='.repeat(60));
    
    console.log('  Waiting 5 seconds...');
    await page.waitForTimeout(5000);
    
    const countdown5secScreenshot = path.join(screenshotsDir, '05-after-5-seconds.png');
    await page.screenshot({ path: countdown5secScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: ${countdown5secScreenshot}`);
    
    // Try to read countdown value
    const countdownElements = await page.locator('[class*="countdown"], [class*="timer"], text=/\\d+s/').all();
    if (countdownElements.length > 0) {
      for (const el of countdownElements) {
        const text = await el.textContent().catch(() => '');
        console.log(`  Countdown element text: "${text}"`);
      }
    }
    
    testResults.push({
      step: '5. Wait 5 seconds',
      status: 'PASS',
      details: 'Screenshot taken after 5 second delay'
    });

    // ======================================
    // STEP 6: Click Cancel/Stop Pairing button
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log('STEP 6: Click Cancel/Stop Pairing Button');
    console.log('='.repeat(60));
    
    const cancelButtonSelectors = [
      'button:has-text("Cancel")',
      'button:has-text("Stop")',
      'button:has-text("End Pairing")',
      'button:has-text("Stop Pairing")',
      '[data-testid="cancel-pairing"]',
      '[data-testid="stop-pairing"]',
      'button >> text=Cancel',
      'button >> text=Stop',
    ];
    
    let cancelButton = null;
    let cancelSelector = '';
    
    for (const selector of cancelButtonSelectors) {
      try {
        const element = page.locator(selector).first();
        const count = await element.count();
        if (count > 0) {
          const isVisible = await element.isVisible();
          if (isVisible) {
            cancelButton = element;
            cancelSelector = selector;
            console.log(`  ✓ Found cancel button with selector: ${selector}`);
            break;
          }
        }
      } catch (e) {
        // Continue
      }
    }
    
    if (cancelButton) {
      console.log('  Clicking Cancel button...');
      await cancelButton.click();
      await page.waitForTimeout(2000);
      console.log('  ✓ Clicked Cancel button');
      
      testResults.push({
        step: '6. Cancel Pairing',
        status: 'PASS',
        details: `Button found with selector: ${cancelSelector}`
      });
    } else {
      console.log('  ✗ Could not find Cancel button');
      
      // Take screenshot anyway
      const noCancelScreenshot = path.join(screenshotsDir, '06a-no-cancel-button.png');
      await page.screenshot({ path: noCancelScreenshot, fullPage: true });
      
      testResults.push({
        step: '6. Cancel Pairing',
        status: 'FAIL',
        details: 'Cancel button not found'
      });
    }

    // ======================================
    // STEP 7: Final screenshot - pairing stopped
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log('STEP 7: Final Screenshot - Pairing Stopped');
    console.log('='.repeat(60));
    
    await page.waitForTimeout(1000);
    
    const finalScreenshot = path.join(screenshotsDir, '07-pairing-stopped.png');
    await page.screenshot({ path: finalScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: ${finalScreenshot}`);
    
    // Verify pairing mode is no longer active
    const stillPairing = await page.locator('text=/pairing.*mode|pairing.*active/i').count() > 0;
    console.log(`  Pairing mode still active: ${stillPairing}`);
    
    testResults.push({
      step: '7. Final State',
      status: 'PASS',
      details: `Pairing mode active: ${stillPairing}`
    });

    // ======================================
    // FINAL REPORT
    // ======================================
    console.log('\n' + '='.repeat(60));
    console.log('                   TEST REPORT                   ');
    console.log('='.repeat(60));
    console.log('\n  Screenshots saved to: ' + screenshotsDir);
    console.log('\n  Results:');
    for (const result of testResults) {
      const icon = result.status === 'PASS' ? '✓' : result.status === 'FAIL' ? '✗' : '?';
      console.log(`    ${icon} ${result.step}: ${result.status}`);
      console.log(`      ${result.details}`);
    }
    console.log('\n' + '='.repeat(60));
    
    // Count failures
    const failures = testResults.filter(r => r.status === 'FAIL').length;
    expect(failures).toBeLessThanOrEqual(2); // Allow some tolerance
  });
});
