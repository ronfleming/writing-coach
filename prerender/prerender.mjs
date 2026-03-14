/**
 * Pre-rendering script for German Writing Coach
 *
 * Generates static HTML files for SEO-valuable routes.
 * Runs after the Blazor WASM build, before deployment.
 *
 * Usage: node prerender.mjs [--output-dir <path>] [--base-url <url>]
 */

import { chromium } from 'playwright';
import { writeFileSync, mkdirSync, existsSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Configuration
const config = {
  // Where the built Blazor app is located
  blazorOutputDir: process.env.BLAZOR_OUTPUT_DIR || join(__dirname, '../Client/bin/Release/net9.0/publish/wwwroot'),
  // Where to write pre-rendered files (same location to overwrite)
  outputDir: process.env.OUTPUT_DIR || join(__dirname, '../Client/bin/Release/net9.0/publish/wwwroot'),
  // Local server URL (started externally)
  baseUrl: process.env.BASE_URL || 'http://localhost:5050',
  // Timeout for page load
  timeout: 60000,
};

// Routes to pre-render — only SEO-valuable public pages
// Auth-gated pages (/history, /phrases) are excluded
const routes = [
  { path: '/', name: 'Home (Coach)' },
  { path: '/about-us', name: 'About Us' },
  { path: '/news', name: 'News' },
  { path: '/news/writing-prompts-and-clean-entry-tracking', name: 'News: Writing Prompts and Clean Entry Tracking' },
  { path: '/news/building-a-german-writing-coach', name: 'News: Building a German Writing Coach' },
  { path: '/privacy', name: 'Privacy Policy' },
  { path: '/terms', name: 'Terms of Service' },
];

async function waitForBlazorReady(page) {
  await page.waitForFunction(() => {
    // Check if Blazor's loading indicator is still visible
    const appEl = document.querySelector('#app');
    if (!appEl) return false;

    const loadingProgress = appEl.querySelector('.loading-progress');
    if (loadingProgress && loadingProgress.offsetParent !== null) {
      return false;
    }

    // Check if we have actual content rendered by Blazor
    const contentSelectors = [
      'h1',
      '.coach-container',
      '.about-container',
      '.news-container',
      '.news-article',
    ];

    return contentSelectors.some(sel => document.querySelector(sel) !== null);
  }, { timeout: config.timeout });

  // Extra wait for any animations/transitions and HeadContent to settle
  await page.waitForTimeout(1000);
}

function cleanupHtml(html, routePath) {
  // Strip Blazor component boundary markers (<!--!-->) everywhere.
  // Critical inside <title> where they render as visible text.
  html = html.replace(/<!--!-->/g, '');

  // Remove Blazor error UI
  html = html.replace(/<div id="blazor-error-ui"[\s\S]*?<\/div>\s*<\/div>/g, '');

  // Remove Blazor reconnection UI if present
  html = html.replace(/<div id="components-reconnect-modal"[\s\S]*?<\/div>/g, '');

  // Canonical should be deterministic per route. Remove any tags rendered
  // by prior app state and inject a single canonical URL for this page.
  html = html.replace(/<link\s+rel="canonical"[^>]*>\s*/gi, '');
  const canonicalUrl = routePath === '/'
    ? 'https://germanwritingcoach.com/'
    : `https://germanwritingcoach.com${routePath}`;
  html = html.replace('</head>', `  <link rel="canonical" href="${canonicalUrl}" />\n</head>`);

  // Add a comment indicating this is pre-rendered
  html = html.replace('</head>', '  <!-- Pre-rendered for SEO -->\n  </head>');

  return html;
}

async function prerenderRoute(browser, route) {
  const page = await browser.newPage();

  try {
    console.log(`  Rendering ${route.name} (${route.path})...`);

    const url = `${config.baseUrl}${route.path}`;
    await page.goto(url, { waitUntil: 'networkidle', timeout: config.timeout });

    // Wait for Blazor to fully render
    await waitForBlazorReady(page);

    // Get the fully rendered HTML
    let html = await page.content();

    // Clean up the HTML
    html = cleanupHtml(html, route.path);

    // Determine output path
    let outputPath;
    if (route.path === '/') {
      outputPath = join(config.outputDir, 'index.html');
    } else {
      // Create directory structure: /about-us -> /about-us/index.html
      const routeDir = join(config.outputDir, route.path);
      mkdirSync(routeDir, { recursive: true });
      outputPath = join(routeDir, 'index.html');
    }

    // Write the pre-rendered HTML
    writeFileSync(outputPath, html, 'utf8');
    console.log(`    ✓ Saved to ${outputPath}`);

    return { route, success: true };
  } catch (error) {
    console.error(`    ✗ Failed: ${error.message}`);
    return { route, success: false, error: error.message };
  } finally {
    await page.close();
  }
}

async function main() {
  console.log('📝 German Writing Coach Pre-rendering Script');
  console.log('=============================================\n');

  // Parse command line arguments
  const args = process.argv.slice(2);
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--output-dir' && args[i + 1]) {
      config.outputDir = args[++i];
    } else if (args[i] === '--base-url' && args[i + 1]) {
      config.baseUrl = args[++i];
    } else if (args[i] === '--blazor-output' && args[i + 1]) {
      config.blazorOutputDir = args[++i];
    }
  }

  console.log(`Configuration:`);
  console.log(`  Base URL: ${config.baseUrl}`);
  console.log(`  Output Dir: ${config.outputDir}`);
  console.log(`  Routes to render: ${routes.length}\n`);

  // Ensure output directory exists
  if (!existsSync(config.outputDir)) {
    console.error(`Error: Output directory does not exist: ${config.outputDir}`);
    console.error('Make sure to build the Blazor app first.');
    process.exit(1);
  }

  console.log('Launching browser...\n');
  const browser = await chromium.launch({
    headless: true,
  });

  const results = [];

  console.log('Pre-rendering routes:');
  for (const route of routes) {
    const result = await prerenderRoute(browser, route);
    results.push(result);
  }

  await browser.close();

  // Summary
  console.log('\n=============================================');
  console.log('Summary:');
  const successful = results.filter(r => r.success).length;
  const failed = results.filter(r => !r.success).length;
  console.log(`  ✓ Successful: ${successful}`);
  console.log(`  ✗ Failed: ${failed}`);

  if (failed > 0) {
    console.log('\nFailed routes:');
    results.filter(r => !r.success).forEach(r => {
      console.log(`  - ${r.route.path}: ${r.error}`);
    });
    process.exit(1);
  }

  console.log('\n🎉 Pre-rendering complete!');
}

main().catch(error => {
  console.error('Fatal error:', error);
  process.exit(1);
});

