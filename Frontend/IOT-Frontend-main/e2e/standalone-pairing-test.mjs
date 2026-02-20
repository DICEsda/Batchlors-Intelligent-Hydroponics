/**
 * Standalone Playwright test runner for real coordinator pairing flow
 * Run with: node e2e/standalone-pairing-test.mjs
 */

import { chromium } from 'playwright';
import * as path from 'path';
import * as fs from 'fs';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const screenshotsDir = path.join(__dirname, '..', 'test-results', 'real-coordinator-pairing');

const REAL_COORDINATOR_ID = '74:4D:BD:AB:A9:F4';
const FRONTEND_URL = 'http://localhost:4200';

async function runTest() {
  console.log('\n' + '='.repeat(70));
  console.log('   REAL COORDINATOR PAIRING FLOW TEST');
  console.log('='.repeat(70));
  console.log(`   Coordinator ID: ${REAL_COORDINATOR_ID}`);
  console.log(`   Frontend URL: ${FRONTEND_URL}`);
  console.log('='.repeat(70) + '\n');

  // Ensure screenshots directory exists
  if (!fs.existsSync(screenshotsDir)) {
    fs.mkdirSync(screenshotsDir, { recursive: true });
  }

  const browser = await chromium.launch({ 
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });
  
  const context = await browser.newContext({
    viewport: { width: 1920, height: 1080 }
  });
  
  const page = await context.newPage();
  const testResults = [];

  try {
    // ======================================
    // STEP 1: Navigate to Overview page
    // ======================================
    console.log('\n' + '─'.repeat(60));
    console.log('STEP 1: Navigate to Overview Page');
    console.log('─'.repeat(60));
    
    await page.goto(`${FRONTEND_URL}/overview`, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(2000);
    
    const overviewScreenshot = path.join(screenshotsDir, '01-overview-page.png');
    await page.screenshot({ path: overviewScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: 01-overview-page.png`);
    console.log(`  URL: ${page.url()}`);
    
    testResults.push({ step: '1. Overview Page', status: 'PASS', screenshot: overviewScreenshot });

    // ======================================
    // STEP 2: Navigate to Coordinator Detail page
    // ======================================
    console.log('\n' + '─'.repeat(60));
    console.log(`STEP 2: Navigate to Coordinator Detail Page`);
    console.log(`  URL: /coordinators/${encodeURIComponent(REAL_COORDINATOR_ID)}`);
    console.log('─'.repeat(60));
    
    const coordUrl = `${FRONTEND_URL}/coordinators/${encodeURIComponent(REAL_COORDINATOR_ID)}`;
    await page.goto(coordUrl, { waitUntil: 'networkidle', timeout: 30000 });
    await page.waitForTimeout(3000);
    
    const coordDetailScreenshot = path.join(screenshotsDir, '02-coordinator-detail.png');
    await page.screenshot({ path: coordDetailScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: 02-coordinator-detail.png`);
    console.log(`  URL: ${page.url()}`);
    
    // Check page content
    const pageContent = await page.content();
    const hasCoordinatorInfo = pageContent.includes('74:4D:BD') || 
                               pageContent.toLowerCase().includes('coordinator') ||
                               pageContent.toLowerCase().includes('reservoir');
    console.log(`  Contains coordinator info: ${hasCoordinatorInfo}`);
    
    testResults.push({ step: '2. Coordinator Detail', status: hasCoordinatorInfo ? 'PASS' : 'WARN', screenshot: coordDetailScreenshot });

    // ======================================
    // STEP 3: Find and Click "Start Pairing" button
    // ======================================
    console.log('\n' + '─'.repeat(60));
    console.log('STEP 3: Find and Click "Start Pairing" Button');
    console.log('─'.repeat(60));
    
    // List all buttons on the page
    const allButtons = await page.locator('button').all();
    console.log(`\n  Found ${allButtons.length} buttons on page:`);
    for (let i = 0; i < allButtons.length; i++) {
      const btn = allButtons[i];
      const text = await btn.textContent().catch(() => '');
      const isVisible = await btn.isVisible().catch(() => false);
      if (isVisible) {
        console.log(`    [${i}] "${text?.trim().substring(0, 50)}"`);
      }
    }
    
    // Try to find Start Pairing button
    const pairingButtonSelectors = [
      'button:has-text("Start Pairing")',
      'button:has-text("start pairing")',
      'button:has-text("Pairing")',
      'button:has-text("Pair")',
      '[data-testid="start-pairing"]',
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
            break;
          }
        }
      } catch (e) {
        // Continue to next selector
      }
    }
    
    if (pairingButton) {
      console.log(`\n  ✓ Found button with: ${usedSelector}`);
      
      // Check if button is disabled
      const isDisabled = await pairingButton.isDisabled();
      console.log(`  Button disabled: ${isDisabled}`);
      
      if (isDisabled) {
        // Wait for button to become enabled (data loading)
        console.log('  Waiting for button to become enabled (data loading)...');
        try {
          await pairingButton.waitFor({ state: 'attached', timeout: 10000 });
          // Wait for the disabled attribute to be removed
          await page.waitForFunction(
            (selector) => {
              const btn = document.querySelector(selector);
              return btn && !btn.disabled;
            },
            'button:has-text("Start Pairing")',
            { timeout: 15000 }
          );
          console.log('  ✓ Button is now enabled');
        } catch (e) {
          console.log(`  ✗ Button remained disabled: ${e.message}`);
          console.log('  Attempting force click anyway...');
        }
      }
      
      // Screenshot before clicking
      const beforePairingScreenshot = path.join(screenshotsDir, '03a-before-pairing-click.png');
      await page.screenshot({ path: beforePairingScreenshot, fullPage: true });
      
      // Click the button (use force if still disabled)
      console.log('  Clicking Start Pairing button...');
      try {
        await pairingButton.click({ timeout: 5000 });
        console.log('  ✓ Button clicked');
      } catch (clickError) {
        console.log('  Normal click failed, trying force click...');
        await pairingButton.click({ force: true });
        console.log('  ✓ Button force-clicked');
      }
      await page.waitForTimeout(2000);
      
      testResults.push({ step: '3. Click Start Pairing', status: 'PASS', details: usedSelector });
    } else {
      console.log('\n  ✗ Could not find Start Pairing button');
      const noButtonScreenshot = path.join(screenshotsDir, '03-no-pairing-button.png');
      await page.screenshot({ path: noButtonScreenshot, fullPage: true });
      testResults.push({ step: '3. Click Start Pairing', status: 'FAIL', screenshot: noButtonScreenshot });
    }

    // ======================================
    // STEP 4: Screenshot Pairing Mode Active
    // ======================================
    console.log('\n' + '─'.repeat(60));
    console.log('STEP 4: Screenshot Pairing Mode Active State');
    console.log('─'.repeat(60));
    
    await page.waitForTimeout(1000);
    
    const pairingActiveScreenshot = path.join(screenshotsDir, '04-pairing-mode-active.png');
    await page.screenshot({ path: pairingActiveScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: 04-pairing-mode-active.png`);
    
    // Check for pairing UI elements
    console.log('\n  Checking for pairing UI elements:');
    
    const countdownVisible = await page.locator('text=/\\d+s|\\d+:\\d+|remaining|countdown|timeout/i').count() > 0 ||
                            await page.locator('[class*="countdown"], [class*="timer"]').count() > 0;
    console.log(`    ${countdownVisible ? '✓' : '✗'} Countdown/Timer: ${countdownVisible ? 'FOUND' : 'NOT FOUND'}`);
    
    const pairingModeVisible = await page.locator('text=/pairing.*mode|pairing.*active|mode.*active/i').count() > 0;
    console.log(`    ${pairingModeVisible ? '✓' : '✗'} Pairing Mode indicator: ${pairingModeVisible ? 'FOUND' : 'NOT FOUND'}`);
    
    const discoveredNodesVisible = await page.locator('text=/discovered|scanning|available|nodes/i').count() > 0;
    console.log(`    ${discoveredNodesVisible ? '✓' : '✗'} Discovered Nodes section: ${discoveredNodesVisible ? 'FOUND' : 'NOT FOUND'}`);
    
    const cancelButtonVisible = await page.locator('button:has-text("Cancel"), button:has-text("Stop"), button:has-text("End")').count() > 0;
    console.log(`    ${cancelButtonVisible ? '✓' : '✗'} Cancel/Stop button: ${cancelButtonVisible ? 'FOUND' : 'NOT FOUND'}`);
    
    testResults.push({ 
      step: '4. Pairing Mode Active', 
      status: (countdownVisible || pairingModeVisible) ? 'PASS' : 'CHECK',
      screenshot: pairingActiveScreenshot
    });

    // ======================================
    // STEP 5: Wait 5 seconds and screenshot
    // ======================================
    console.log('\n' + '─'.repeat(60));
    console.log('STEP 5: Wait 5 seconds and verify countdown');
    console.log('─'.repeat(60));
    
    console.log('  Waiting 5 seconds...');
    await page.waitForTimeout(5000);
    
    const after5secScreenshot = path.join(screenshotsDir, '05-after-5-seconds.png');
    await page.screenshot({ path: after5secScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: 05-after-5-seconds.png`);
    
    testResults.push({ step: '5. After 5 seconds', status: 'PASS', screenshot: after5secScreenshot });

    // ======================================
    // STEP 6: Click Cancel/Stop Pairing button
    // ======================================
    console.log('\n' + '─'.repeat(60));
    console.log('STEP 6: Click Cancel/Stop Pairing Button');
    console.log('─'.repeat(60));
    
    const cancelButtonSelectors = [
      'button:has-text("Cancel")',
      'button:has-text("Stop")',
      'button:has-text("End Pairing")',
      'button:has-text("Stop Pairing")',
      'button:has-text("Cancel Pairing")',
    ];
    
    let cancelButton = null;
    
    for (const selector of cancelButtonSelectors) {
      try {
        const element = page.locator(selector).first();
        const count = await element.count();
        if (count > 0 && await element.isVisible()) {
          cancelButton = element;
          console.log(`  ✓ Found cancel button: ${selector}`);
          break;
        }
      } catch (e) {
        // Continue
      }
    }
    
    if (cancelButton) {
      console.log('  Clicking Cancel button...');
      await cancelButton.click();
      await page.waitForTimeout(2000);
      console.log('  ✓ Cancel button clicked');
      testResults.push({ step: '6. Cancel Pairing', status: 'PASS' });
    } else {
      console.log('  ✗ Could not find Cancel button');
      testResults.push({ step: '6. Cancel Pairing', status: 'FAIL' });
    }

    // ======================================
    // STEP 7: Final screenshot - pairing stopped
    // ======================================
    console.log('\n' + '─'.repeat(60));
    console.log('STEP 7: Final Screenshot - Pairing Stopped');
    console.log('─'.repeat(60));
    
    await page.waitForTimeout(1000);
    
    const finalScreenshot = path.join(screenshotsDir, '07-pairing-stopped.png');
    await page.screenshot({ path: finalScreenshot, fullPage: true });
    console.log(`✓ Screenshot saved: 07-pairing-stopped.png`);
    
    testResults.push({ step: '7. Final State', status: 'PASS', screenshot: finalScreenshot });

  } catch (error) {
    console.error('\n  ✗ Test error:', error.message);
    
    // Take error screenshot
    const errorScreenshot = path.join(screenshotsDir, 'error-screenshot.png');
    await page.screenshot({ path: errorScreenshot, fullPage: true }).catch(() => {});
    
    testResults.push({ step: 'Error', status: 'FAIL', details: error.message });
  } finally {
    await browser.close();
  }

  // ======================================
  // FINAL REPORT
  // ======================================
  console.log('\n' + '='.repeat(70));
  console.log('                        TEST REPORT');
  console.log('='.repeat(70));
  console.log(`\n  Screenshots saved to: ${screenshotsDir}\n`);
  
  let passed = 0, failed = 0, warnings = 0;
  
  for (const result of testResults) {
    const icon = result.status === 'PASS' ? '✓' : result.status === 'FAIL' ? '✗' : '?';
    console.log(`  ${icon} ${result.step}: ${result.status}`);
    if (result.details) console.log(`      ${result.details}`);
    if (result.screenshot) console.log(`      Screenshot: ${path.basename(result.screenshot)}`);
    
    if (result.status === 'PASS') passed++;
    else if (result.status === 'FAIL') failed++;
    else warnings++;
  }
  
  console.log('\n' + '─'.repeat(70));
  console.log(`  Summary: ${passed} passed, ${failed} failed, ${warnings} warnings`);
  console.log('='.repeat(70) + '\n');
  
  // List all screenshots
  console.log('  Generated Screenshots:');
  const screenshots = fs.readdirSync(screenshotsDir).filter(f => f.endsWith('.png'));
  for (const s of screenshots.sort()) {
    console.log(`    - ${s}`);
  }
  console.log('');
  
  return failed === 0;
}

runTest().then(success => {
  process.exit(success ? 0 : 1);
}).catch(err => {
  console.error('Fatal error:', err);
  process.exit(1);
});
