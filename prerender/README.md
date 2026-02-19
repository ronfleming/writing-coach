# German Writing Coach Prerendering

This folder contains scripts to pre-render the Blazor WebAssembly app for SEO purposes.

## Why Pre-rendering?

Blazor WASM is a Single Page Application (SPA) - all routes return the same `index.html` file, and content is rendered by JavaScript. Search engines may not fully execute JavaScript, leading to:

- Missing page-specific content for crawlers
- Duplicate content issues (all pages look the same to Google)
- Missing per-page meta tags and canonical URLs
- Poor indexing

Pre-rendering generates static HTML files for each route at build time, so search engines see fully-rendered content.

## How It Works

1. **Build** - Blazor app is compiled with `dotnet publish -c Release`
2. **Serve** - A local HTTP server serves the built app
3. **Render** - Playwright (headless Chrome) navigates to each route and captures the rendered HTML
4. **Save** - HTML files are saved to the output directory, overwriting the original `index.html`

After pre-rendering, each route has its own complete HTML file:

```
wwwroot/
├── index.html              (pre-rendered home page)
├── about-us/
│   └── index.html          (pre-rendered about page)
├── privacy/
│   └── index.html          (pre-rendered privacy page)
├── terms/
│   └── index.html          (pre-rendered terms page)
└── _framework/             (Blazor runtime - unchanged)
```

## Local Testing

Local development is **unchanged**. Pre-rendering only runs during production builds.

To test pre-rendering locally:

```bash
# 1. Build the Blazor app
cd Client
dotnet publish -c Release

# 2. Install pre-rendering dependencies (first time only)
cd ../prerender
npm install
npx playwright install chromium

# 3. Run pre-rendering
node serve-and-prerender.mjs

# 4. Check the output
cat ../Client/bin/Release/net9.0/publish/wwwroot/about-us/index.html
# Should contain full HTML with <!-- Pre-rendered for SEO --> comment
```

## Scripts

### `prerender.mjs`

The main pre-rendering script. Requires a running server.

**Options:**
- `--base-url <url>` - Server URL (default: `http://localhost:5050`)
- `--output-dir <path>` - Where to save HTML files

**Usage:**
```bash
node prerender.mjs --base-url http://localhost:5050 --output-dir ../Client/bin/Release/net9.0/publish/wwwroot
```

### `serve-and-prerender.mjs`

Convenience script that starts a local server, runs pre-rendering, then stops the server.

**Environment variables:**
- `WWWROOT` - Path to the Blazor build output (default: `../Client/bin/Release/net9.0/publish/wwwroot`)

**Usage:**
```bash
node serve-and-prerender.mjs
```

## Routes Prerendered

The following routes are pre-rendered for SEO:

- `/` - Homepage (coach interface)
- `/about-us` - About page (most SEO-valuable)
- `/privacy` - Privacy policy
- `/terms` - Terms of service

**Not prerendered**: `/history`, `/phrases` (auth-gated, no public content)

## Adding New Routes

To add a new route to pre-rendering:

1. Add the route to `prerender.mjs` in the `routes` array:
   ```javascript
   { path: '/new-page', name: 'New Page' },
   ```

2. Add a rewrite rule to `Client/wwwroot/staticwebapp.config.json`:
   ```json
   {
     "route": "/new-page",
     "rewrite": "/new-page/index.html"
   }
   ```

3. Add the URL to `Client/wwwroot/sitemap.xml`

## Blazor Comment Markers Issue

**Important**: Blazor injects comment markers (`<!--Blazor:...-->`) into HTML when using `@` interpolation in `.razor` files. This can break JSON-LD structured data if placed inside `<script type="application/ld+json">` tags.

**Our approach (safe)**:
- JSON-LD is in **static `index.html`** (not in `.razor` files)
- No dynamic values or `@` interpolation
- Pre-rendering captures the final rendered HTML without Blazor markers

**Alternative approach** (if you need dynamic JSON-LD in `.razor` files):
- Generate JSON in C# code using `System.Text.Json.JsonSerializer`
- Return as raw HTML string
- See `wal-o-mat` project for reference implementation

## Verification

After pre-rendering, verify the output:

```bash
# Check that pre-rendered files exist
ls ../Client/bin/Release/net9.0/publish/wwwroot/about-us/

# Check that JSON-LD is valid (no Blazor markers)
cat ../Client/bin/Release/net9.0/publish/wwwroot/index.html | grep -A 20 "application/ld+json"

# Should NOT contain: <!--Blazor:...-->
# Should contain: <!-- Pre-rendered for SEO -->
```

## CI/CD Integration

Add to your GitHub Actions workflow after the build step:

```yaml
- name: Install Playwright
  run: |
    cd prerender
    npm install
    npx playwright install chromium

- name: Prerender Pages
  run: |
    cd prerender
    node serve-and-prerender.mjs
  env:
    WWWROOT: ../Client/bin/Release/net9.0/publish/wwwroot
```

## Troubleshooting

### Pre-rendering fails with timeout

The script waits for Blazor to fully render. If a page has slow loading data, increase the timeout in `prerender.mjs`:

```javascript
const config = {
  timeout: 60000, // Increase if needed
  // ...
};
```

### Content appears different after pre-rendering

Check that the pre-rendered HTML includes Blazor's boot script. The `_framework/blazor.webassembly.js` script should hydrate the page after load, making it interactive.

### JSON-LD contains Blazor comment markers

If you see `<!--Blazor:...-->` in your JSON-LD:
1. Move JSON-LD to static `index.html` (recommended)
2. OR generate JSON dynamically in C# code (see wal-o-mat example)

## Performance

Pre-rendering adds ~10-30 seconds to the build time (depends on number of routes and page complexity). This is acceptable for production builds and has zero runtime cost.

