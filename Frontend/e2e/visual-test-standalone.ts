// Standalone visual test script for pairing flow
// Run with: npx ts-node e2e/visual-test-standalone.ts

import { chromium, Browser, Page } from 'playwright';
import * as path from 'path';
import * as fs from 'fs';

const screenshotsDir = path.join(__dirname, '..', 'test-results', 'visual-screenshots');

async function runVisualTest() {
  // Ensure screenshots directory exists
  if (!fs.existsSync(screenshotsDir)) {
    fs.mkdirSync(screenshotsDir, { recursive: true });
  }

  const results: string[] = [];
  const errors: string[] = [];
  let browser: Browser | null = null;

  try {
    console.log('\n========================================');
    console.log('   VISUAL PAIRING FLOW TEST - START    ');
    console.log('========================================\n');

    // Launch browser
    browser = await chromium.launch({ 
      headless: false,  // Set to true for CI
      slowMo: 500       // Slow down for visibility
    });
    
    const context = await browser.newContext({
      viewport: { width: 1920, height: 1080 }
    });
    
    const page = await context.newPage();

    // ========== STEP 1: Navigate to Farm Overview ==========
    console.log('=== STEP 1: Navigate to Farm Overview ===');
    try {
      await page.goto('http://localhost:4200', { 
        waitUntil: 'networkidle', 
        timeout: 30000 
      });
      
      await page.waitForTimeout(2000);
      
      const screenshot1Path = path.join(screenshotsDir, '01-farm-overview.png');
      await page.screenshot({ path: screenshot1Path, fullPage: true });
      
      results.push(`STEP 1: Successfully navigated to Farm Overview`);
      results.push(`  URL: ${page.url()}`);
      results.push(`  Screenshot: ${screenshot1Path}`);
      console.log(`Screenshot saved: ${screenshot1Path}`);
      console.log(`Current URL: ${page.url()}\n`);
      
    } catch (error) {
      errors.push(`STEP 1 Error: ${error}`);
      console.error(`Step 1 failed: ${error}\n`);
    }

    // ========== STEP 2: Find and click coordinator ==========
    console.log('=== STEP 2: Find and click coordinator card ===');
    try {
      // Log all links for debugging
      const allLinks = await page.locator('a[href]').all();
      console.log(`Found ${allLinks.length} links on page:`);
      
      for (const link of allLinks.slice(0, 20)) {
        const href = await link.getAttribute('href');
        const text = (await link.textContent())?.trim().substring(0, 60);
        if (href && (href.includes('coordinator') || href.includes('reservoir') || /[0-9A-Fa-f]{2}:/.test(text || ''))) {
          console.log(`  - href="${href}" text="${text}"`);
        }
      }

      // Try to find coordinator link
      const coordSelectors = [
        'a[href*="/coordinators/"]',
        'a[href*="/reservoirs/"]',
        '.coordinator-card a',
        '[data-testid*="coordinator"]',
      ];
      
      let coordElement = null;
      
      for (const selector of coordSelectors) {
        const element = page.locator(selector).first();
        if (await element.count() > 0) {
          coordElement = element;
          console.log(`Found coordinator with selector: ${selector}`);
          break;
        }
      }

      // If not found, try to find any card with MAC address
      if (!coordElement) {
        // Look for any link containing what looks like a MAC address in the URL
        const macLinks = await page.locator('a[href*=":"]').all();
        for (const link of macLinks) {
          const href = await link.getAttribute('href');
          if (href && /[0-9A-Fa-f]{2}(%3A|:)[0-9A-Fa-f]{2}/.test(href)) {
            coordElement = link;
            console.log(`Found MAC address link: ${href}`);
            break;
          }
        }
      }

      if (coordElement) {
        const href = await coordElement.getAttribute('href');
        console.log(`Clicking coordinator link: ${href}`);
        
        await coordElement.click();
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(2000);
        
        results.push(`STEP 2: Successfully clicked coordinator`);
        results.push(`  Navigated to: ${page.url()}`);
        console.log(`Navigated to: ${page.url()}\n`);
      } else {
        errors.push('STEP 2: Could not find coordinator card');
        console.log('Could not find coordinator card\n');
        
        // Take a screenshot for debugging
        const debugPath = path.join(screenshotsDir, '01b-debug-no-coordinator.png');
        await page.screenshot({ path: debugPath, fullPage: true });
      }
      
    } catch (error) {
      errors.push(`STEP 2 Error: ${error}`);
      console.error(`Step 2 failed: ${error}\n`);
    }

    // ========== STEP 3: Screenshot coordinator detail ==========
    console.log('=== STEP 3: Screenshot coordinator detail page ===');
    try {
      await page.waitForTimeout(1500);
      
      const screenshot2Path = path.join(screenshotsDir, '02-coordinator-detail.png');
      await page.screenshot({ path: screenshot2Path, fullPage: true });
      
      results.push(`STEP 3: Screenshot of coordinator detail`);
      results.push(`  URL: ${page.url()}`);
      results.push(`  Screenshot: ${screenshot2Path}`);
      console.log(`Screenshot saved: ${screenshot2Path}`);
      console.log(`Current URL: ${page.url()}\n`);
      
    } catch (error) {
      errors.push(`STEP 3 Error: ${error}`);
      console.error(`Step 3 failed: ${error}\n`);
    }

    // ========== STEP 4: Find and click Start Pairing ==========
    console.log('=== STEP 4: Find and click Start Pairing button ===');
    try {
      // List all buttons
      const allButtons = await page.locator('button').all();
      console.log(`Found ${allButtons.length} buttons:`);
      
      for (const btn of allButtons) {
        const text = (await btn.textContent())?.trim();
        const isVisible = await btn.isVisible();
        if (text && isVisible) {
          console.log(`  - "${text.substring(0, 50)}" (visible: ${isVisible})`);
        }
      }

      // Try to find pairing button
      const pairingSelectors = [
        'button:has-text("Start Pairing")',
        'button:has-text("Pairing")',
        'button:has-text("Pair")',
        '[data-testid="start-pairing"]',
      ];
      
      let pairingButton = null;
      
      for (const selector of pairingSelectors) {
        const element = page.locator(selector).first();
        if (await element.count() > 0 && await element.isVisible()) {
          pairingButton = element;
          console.log(`\nFound pairing button with selector: ${selector}`);
          break;
        }
      }

      if (pairingButton) {
        await pairingButton.click();
        await page.waitForTimeout(2000);
        
        results.push(`STEP 4: Successfully clicked Start Pairing button`);
        console.log('Clicked Start Pairing button\n');
      } else {
        errors.push('STEP 4: Could not find Start Pairing button');
        console.log('\nCould not find Start Pairing button');
        
        // Take debug screenshot
        const debugPath = path.join(screenshotsDir, '02b-debug-no-pairing-button.png');
        await page.screenshot({ path: debugPath, fullPage: true });
        console.log(`Debug screenshot: ${debugPath}\n`);
      }
      
    } catch (error) {
      errors.push(`STEP 4 Error: ${error}`);
      console.error(`Step 4 failed: ${error}\n`);
    }

    // ========== STEP 5: Screenshot pairing mode ==========
    console.log('=== STEP 5: Screenshot of pairing mode active ===');
    try {
      await page.waitForTimeout(1500);
      
      const screenshot3Path = path.join(screenshotsDir, '03-pairing-mode-active.png');
      await page.screenshot({ path: screenshot3Path, fullPage: true });
      
      results.push(`STEP 5: Screenshot of pairing mode`);
      results.push(`  Screenshot: ${screenshot3Path}`);
      console.log(`Screenshot saved: ${screenshot3Path}`);

      // Check for pairing UI elements
      console.log('\nChecking for Pairing UI Elements:');
      
      const checks = [
        { name: 'Countdown timer', selectors: ['[class*="countdown"]', '[class*="timer"]', 'text=/\\d+s|remaining/i'] },
        { name: 'Discovered Nodes', selectors: ['text=/discovered/i', 'text=/nodes/i', '[class*="discovered"]'] },
        { name: 'Cancel/Stop button', selectors: ['button:has-text("Cancel")', 'button:has-text("Stop")'] },
        { name: 'Pairing active indicator', selectors: ['text=/pairing/i', '[class*="pairing"]', '[class*="active"]'] },
      ];

      for (const check of checks) {
        let found = false;
        for (const selector of check.selectors) {
          try {
            const count = await page.locator(selector).count();
            if (count > 0) {
              found = true;
              break;
            }
          } catch {}
        }
        const status = found ? '✓ FOUND' : '✗ NOT FOUND';
        console.log(`  ${status}: ${check.name}`);
        results.push(`  ${check.name}: ${found ? 'Found' : 'Not found'}`);
      }
      
    } catch (error) {
      errors.push(`STEP 5 Error: ${error}`);
      console.error(`Step 5 failed: ${error}\n`);
    }

    // Wait a moment to observe
    console.log('\nWaiting 5 seconds for observation...');
    await page.waitForTimeout(5000);

  } catch (error) {
    errors.push(`General Error: ${error}`);
    console.error(`Test failed: ${error}`);
  } finally {
    // Close browser
    if (browser) {
      await browser.close();
    }
  }

  // ========== FINAL REPORT ==========
  console.log('\n========================================');
  console.log('         VISUAL TEST REPORT            ');
  console.log('========================================\n');
  
  console.log('RESULTS:');
  results.forEach(r => console.log(`  ${r}`));
  
  if (errors.length > 0) {
    console.log('\nERRORS:');
    errors.forEach(e => console.log(`  - ${e}`));
  } else {
    console.log('\nNo errors encountered!');
  }
  
  console.log('\nSCREENSHOTS SAVED TO:');
  console.log(`  ${screenshotsDir}`);
  
  console.log('\n========================================\n');
  
  return { results, errors };
}

// Run the test
runVisualTest().then(({ errors }) => {
  process.exit(errors.length > 0 ? 1 : 0);
}).catch(err => {
  console.error('Test execution failed:', err);
  process.exit(1);
});
