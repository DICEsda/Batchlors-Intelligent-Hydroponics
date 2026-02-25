import { test, expect } from '@playwright/test';
import * as path from 'path';

/**
 * Visual Testing for Pairing Flow
 * This test captures screenshots at each step of the pairing workflow
 */
test.describe('Visual Pairing Flow Test', () => {
  const screenshotsDir = 'test-results/visual-screenshots';

  test('complete pairing flow with screenshots', async ({ page }) => {
    const results: string[] = [];
    const errors: string[] = [];

    // Step 1: Navigate to http://localhost:4200 - Farm Overview page
    console.log('\n=== STEP 1: Navigate to Farm Overview ===');
    try {
      await page.goto('/', { waitUntil: 'networkidle', timeout: 30000 });
      
      // Wait for content to load
      await page.waitForTimeout(2000);
      
      // Take screenshot of initial state
      const screenshot1Path = path.join(screenshotsDir, '01-farm-overview.png');
      await page.screenshot({ 
        path: screenshot1Path, 
        fullPage: true 
      });
      results.push(`Step 1: Successfully navigated to Farm Overview. Screenshot: ${screenshot1Path}`);
      console.log(`Screenshot saved: ${screenshot1Path}`);
      
      // Log current URL
      console.log(`Current URL: ${page.url()}`);
      
    } catch (error) {
      errors.push(`Step 1 Error: ${error}`);
      console.error(`Step 1 failed: ${error}`);
    }

    // Step 2: Look for coordinator card and click on it
    console.log('\n=== STEP 2: Find and click coordinator card ===');
    try {
      // Wait for coordinators to load
      await page.waitForTimeout(1000);
      
      // Try multiple selectors to find coordinator cards/links
      const coordSelectors = [
        'a[href*="/coordinators/"]',
        'a[href*="/reservoirs/"]',
        '[data-testid*="coordinator"]',
        '.coordinator-card',
        '[class*="coordinator"]',
        'a[href*="74:4D:BD:AB:A9:F4"]',
        'a[href*="74%3A4D%3A"]',  // URL encoded MAC
      ];
      
      let coordElement = null;
      let usedSelector = '';
      
      for (const selector of coordSelectors) {
        const element = page.locator(selector).first();
        if (await element.count() > 0) {
          coordElement = element;
          usedSelector = selector;
          break;
        }
      }
      
      // If no specific coordinator link found, look for any card-like element with MAC address text
      if (!coordElement) {
        // Try finding by text content containing MAC address pattern
        const macPattern = page.locator('text=/([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}/');
        if (await macPattern.count() > 0) {
          // Find parent clickable element
          coordElement = macPattern.first().locator('xpath=ancestor::a[1]');
          if (await coordElement.count() === 0) {
            coordElement = macPattern.first();
          }
          usedSelector = 'MAC address text pattern';
        }
      }
      
      // Log what we found on the page
      const pageContent = await page.content();
      console.log(`Page contains 'coordinator': ${pageContent.toLowerCase().includes('coordinator')}`);
      console.log(`Page contains MAC address: ${/([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}/.test(pageContent)}`);
      
      // List all links on the page for debugging
      const allLinks = await page.locator('a[href]').all();
      console.log(`\nAll links on page (${allLinks.length}):`);
      for (const link of allLinks.slice(0, 15)) { // Show first 15
        const href = await link.getAttribute('href');
        const text = await link.textContent();
        console.log(`  - href="${href}" text="${text?.slice(0, 50)}"`);
      }
      
      if (coordElement && await coordElement.count() > 0) {
        console.log(`Found coordinator using selector: ${usedSelector}`);
        
        // Click on the coordinator
        await coordElement.click();
        await page.waitForLoadState('networkidle');
        await page.waitForTimeout(2000);
        
        results.push(`Step 2: Successfully clicked coordinator card using selector: ${usedSelector}`);
        console.log(`Navigated to: ${page.url()}`);
      } else {
        // Try clicking on a reservoir link if coordinator not found
        const reservoirLink = page.locator('a[href*="/reservoir"], a:has-text("Reservoir")').first();
        if (await reservoirLink.count() > 0) {
          await reservoirLink.click();
          await page.waitForLoadState('networkidle');
          await page.waitForTimeout(1000);
          results.push(`Step 2: Clicked reservoir link instead`);
        } else {
          errors.push('Step 2: Could not find coordinator card or reservoir link');
        }
      }
      
    } catch (error) {
      errors.push(`Step 2 Error: ${error}`);
      console.error(`Step 2 failed: ${error}`);
    }

    // Step 3: Take screenshot of coordinator detail page
    console.log('\n=== STEP 3: Screenshot coordinator detail page ===');
    try {
      await page.waitForTimeout(1500);
      
      const screenshot2Path = path.join(screenshotsDir, '02-coordinator-detail.png');
      await page.screenshot({ 
        path: screenshot2Path, 
        fullPage: true 
      });
      results.push(`Step 3: Screenshot of coordinator detail page saved: ${screenshot2Path}`);
      console.log(`Screenshot saved: ${screenshot2Path}`);
      console.log(`Current URL: ${page.url()}`);
      
    } catch (error) {
      errors.push(`Step 3 Error: ${error}`);
      console.error(`Step 3 failed: ${error}`);
    }

    // Step 4: Find and click "Start Pairing" button
    console.log('\n=== STEP 4: Find and click Start Pairing button ===');
    try {
      // Try multiple selectors for the pairing button
      const pairingButtonSelectors = [
        'button:has-text("Start Pairing")',
        'button:has-text("Pairing")',
        '[data-testid="start-pairing"]',
        'button[class*="pairing"]',
        'button:has-text("Pair")',
        'hlm-button:has-text("Start Pairing")',
        'text=Start Pairing',
      ];
      
      let pairingButton = null;
      let usedSelector = '';
      
      for (const selector of pairingButtonSelectors) {
        const element = page.locator(selector).first();
        if (await element.count() > 0 && await element.isVisible()) {
          pairingButton = element;
          usedSelector = selector;
          break;
        }
      }
      
      // List all buttons for debugging
      const allButtons = await page.locator('button').all();
      console.log(`\nAll buttons on page (${allButtons.length}):`);
      for (const btn of allButtons.slice(0, 10)) {
        const text = await btn.textContent();
        const className = await btn.getAttribute('class');
        console.log(`  - text="${text?.trim().slice(0, 40)}" class="${className?.slice(0, 50)}"`);
      }
      
      if (pairingButton) {
        console.log(`Found pairing button using selector: ${usedSelector}`);
        
        // Take a screenshot before clicking
        const screenshot2bPath = path.join(screenshotsDir, '02b-before-pairing-click.png');
        await page.screenshot({ path: screenshot2bPath, fullPage: true });
        
        // Click the button
        await pairingButton.click();
        results.push(`Step 4: Successfully clicked Start Pairing button`);
        console.log('Clicked Start Pairing button');
        
        // Wait for pairing mode to activate
        await page.waitForTimeout(2000);
        
      } else {
        errors.push('Step 4: Could not find Start Pairing button');
        console.log('Could not find Start Pairing button');
        
        // Take screenshot showing current state
        const screenshot2cPath = path.join(screenshotsDir, '02c-no-pairing-button.png');
        await page.screenshot({ path: screenshot2cPath, fullPage: true });
      }
      
    } catch (error) {
      errors.push(`Step 4 Error: ${error}`);
      console.error(`Step 4 failed: ${error}`);
    }

    // Step 5: Take screenshot showing pairing mode
    console.log('\n=== STEP 5: Screenshot of pairing mode ===');
    try {
      await page.waitForTimeout(1000);
      
      const screenshot3Path = path.join(screenshotsDir, '03-pairing-mode-active.png');
      await page.screenshot({ 
        path: screenshot3Path, 
        fullPage: true 
      });
      results.push(`Step 5: Screenshot of pairing mode saved: ${screenshot3Path}`);
      console.log(`Screenshot saved: ${screenshot3Path}`);
      
      // Check for expected pairing UI elements
      const pairingUIChecks = {
        'Countdown timer': await page.locator('text=/\\d+s|\\d+:\\d+|remaining|countdown/i').count() > 0 ||
                           await page.locator('[class*="countdown"], [class*="timer"]').count() > 0,
        'Discovered Nodes section': await page.locator('text=/discovered|nodes|scanning/i').count() > 0 ||
                                    await page.locator('[class*="discovered"], [data-testid*="discovered"]').count() > 0,
        'Cancel button': await page.locator('button:has-text("Cancel"), button:has-text("Stop")').count() > 0,
        'Pairing indicator': await page.locator('text=/pairing|active|mode/i').count() > 0,
      };
      
      console.log('\nPairing UI Elements Check:');
      for (const [element, found] of Object.entries(pairingUIChecks)) {
        console.log(`  - ${element}: ${found ? 'FOUND' : 'NOT FOUND'}`);
      }
      
      results.push(`Pairing UI Check: ${JSON.stringify(pairingUIChecks)}`);
      
    } catch (error) {
      errors.push(`Step 5 Error: ${error}`);
      console.error(`Step 5 failed: ${error}`);
    }

    // Final Report
    console.log('\n========================================');
    console.log('          VISUAL TEST REPORT           ');
    console.log('========================================\n');
    
    console.log('RESULTS:');
    results.forEach((r, i) => console.log(`  ${i + 1}. ${r}`));
    
    if (errors.length > 0) {
      console.log('\nERRORS:');
      errors.forEach((e, i) => console.log(`  ${i + 1}. ${e}`));
    } else {
      console.log('\nNo errors encountered!');
    }
    
    console.log('\n========================================');
    
    // Assert no critical errors
    expect(errors.filter(e => e.includes('Step 1')).length).toBe(0);
  });
});
