/**
 * Serve the built Blazor app and run pre-rendering
 *
 * This script:
 * 1. Starts a local HTTP server serving the built Blazor app
 * 2. Runs the pre-rendering script
 * 3. Shuts down the server
 */

import { spawn } from 'child_process';
import { createServer } from 'http';
import { readFileSync, existsSync, statSync } from 'fs';
import { join, extname, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));

// Configuration
const PORT = 5050;
const WWWROOT = process.env.WWWROOT || join(__dirname, '../Client/bin/Release/net9.0/publish/wwwroot');

// MIME types for common files
const mimeTypes = {
  '.html': 'text/html',
  '.js': 'application/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.gif': 'image/gif',
  '.webp': 'image/webp',
  '.svg': 'image/svg+xml',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.eot': 'application/vnd.ms-fontobject',
  '.wasm': 'application/wasm',
  '.dll': 'application/octet-stream',
  '.blat': 'application/octet-stream',
  '.dat': 'application/octet-stream',
  '.ico': 'image/x-icon',
  '.xml': 'application/xml',
  '.webmanifest': 'application/manifest+json',
};

function getMimeType(filePath) {
  const ext = extname(filePath).toLowerCase();
  return mimeTypes[ext] || 'application/octet-stream';
}

function serveFile(res, filePath) {
  try {
    const content = readFileSync(filePath);
    const mimeType = getMimeType(filePath);
    res.writeHead(200, { 'Content-Type': mimeType });
    res.end(content);
  } catch (error) {
    res.writeHead(404);
    res.end('Not found');
  }
}

async function main() {
  console.log('📝 German Writing Coach Pre-render Server\n');

  // Check if wwwroot exists
  if (!existsSync(WWWROOT)) {
    console.error(`Error: Build output not found at ${WWWROOT}`);
    console.error('Run "dotnet publish -c Release" in the Client folder first.');
    process.exit(1);
  }

  console.log(`Serving from: ${WWWROOT}`);
  console.log(`Port: ${PORT}\n`);

  // Create simple HTTP server that mimics SPA behavior
  const server = createServer((req, res) => {
    let urlPath = req.url.split('?')[0]; // Remove query string

    // Try to serve the exact file
    let filePath = join(WWWROOT, urlPath);

    // If it's a directory, look for index.html
    if (existsSync(filePath) && statSync(filePath).isDirectory()) {
      filePath = join(filePath, 'index.html');
    }

    // If file exists, serve it
    if (existsSync(filePath) && statSync(filePath).isFile()) {
      serveFile(res, filePath);
      return;
    }

    // SPA fallback: serve index.html for all non-file routes
    const indexPath = join(WWWROOT, 'index.html');
    if (existsSync(indexPath)) {
      serveFile(res, indexPath);
      return;
    }

    res.writeHead(404);
    res.end('Not found');
  });

  // Start server
  await new Promise((resolve, reject) => {
    server.listen(PORT, (err) => {
      if (err) reject(err);
      else resolve();
    });
  });

  console.log(`Server running at http://localhost:${PORT}\n`);
  console.log('Starting pre-rendering...\n');

  // Run pre-render script
  try {
    const prerenderProcess = spawn('node', ['prerender.mjs', '--base-url', `http://localhost:${PORT}`, '--output-dir', WWWROOT], {
      cwd: __dirname,
      stdio: 'inherit',
    });

    await new Promise((resolve, reject) => {
      prerenderProcess.on('close', (code) => {
        if (code === 0) resolve();
        else reject(new Error(`Pre-render exited with code ${code}`));
      });
      prerenderProcess.on('error', reject);
    });
  } finally {
    // Shutdown server
    console.log('\nShutting down server...');
    server.close();
  }
}

main().catch(error => {
  console.error('Error:', error.message);
  process.exit(1);
});

