# Build BSO and copy to wwwroot/BSO
# Adjust the BSO_PROJECT_PATH if needed

$BSO_PROJECT_PATH = "X:\BalatroSeedOracle\src\BalatroSeedOracle.Browser\BalatroSeedOracle.Browser.csproj"
$OUTPUT_DIR = "Motely.API\wwwroot\BSO"
$TEMP_PUBLISH = "publish\bso-temp"

Write-Host "Building BSO Browser..." -ForegroundColor Cyan

# Build and publish BSO
if (Test-Path $BSO_PROJECT_PATH) {
    dotnet publish $BSO_PROJECT_PATH -c Release -f net10.0-browser -o $TEMP_PUBLISH
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Copying files to $OUTPUT_DIR..." -ForegroundColor Cyan
        
        # Remove old BSO directory
        if (Test-Path $OUTPUT_DIR) {
            Remove-Item -Recurse -Force $OUTPUT_DIR
        }
        
        # Create directory
        New-Item -ItemType Directory -Force -Path $OUTPUT_DIR | Out-Null
        
        # Copy all published files
        Copy-Item -Recurse "$TEMP_PUBLISH\*" $OUTPUT_DIR -Force

        # ------------------------------------------------------------------
        # Flatten structure: Move wwwroot/* contents up to BSO/ root
        # Avalonia Browser publishes with wwwroot/, but we want flat structure
        # ------------------------------------------------------------------
        $BSO_WWWROOT = Join-Path $OUTPUT_DIR "wwwroot"
        if (Test-Path $BSO_WWWROOT) {
            Write-Host "Flattening BSO structure (moving wwwroot/* to BSO/)..." -ForegroundColor Cyan
            
            # Move all contents from wwwroot/ up to BSO/
            Get-ChildItem -Path $BSO_WWWROOT -Force | Move-Item -Destination $OUTPUT_DIR -Force
            
            # Remove empty wwwroot folder
            Remove-Item -Path $BSO_WWWROOT -Force -ErrorAction SilentlyContinue
            
            Write-Host "Structure flattened successfully" -ForegroundColor Green
        }

        # ------------------------------------------------------------------
        # PWA overlay: add manifest + service worker + icons so /BSO/ can be
        # installed and run fullscreen (standalone display).
        # Files are written directly to $OUTPUT_DIR (BSO/) after flattening.
        # ------------------------------------------------------------------
        if (Test-Path $OUTPUT_DIR) {
            # Ensure icons exist
            $ICON_DIR = Join-Path $OUTPUT_DIR "icons"
            New-Item -ItemType Directory -Force -Path $ICON_DIR | Out-Null

            # Generate simple PNG icons (192 / 512) - avoids shipping binaries in git
            try {
                Add-Type -AssemblyName System.Drawing

                function New-PwaIcon([int]$size, [string]$path) {
                    $bmp = New-Object System.Drawing.Bitmap $size, $size
                    $g = [System.Drawing.Graphics]::FromImage($bmp)
                    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

                    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
                    $bg1  = [System.Drawing.Color]::FromArgb(10, 10, 15)
                    $bg2  = [System.Drawing.Color]::FromArgb(26, 26, 46)
                    $bg   = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $bg1, $bg2, 45)
                    $g.FillRectangle($bg, $rect)

                    $glow  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90, 76, 201, 240))
                    $g.FillEllipse($glow, [int]($size * 0.06), [int]($size * 0.06), [int]($size * 0.88), [int]($size * 0.88))

                    $inner = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 18, 18, 26))
                    $g.FillEllipse($inner, [int]($size * 0.14), [int]($size * 0.14), [int]($size * 0.72), [int]($size * 0.72))

                    $fontSize = [float]($size * 0.28)
                    $font     = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
                    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 196, 48))

                    $format = New-Object System.Drawing.StringFormat
                    $format.Alignment = [System.Drawing.StringAlignment]::Center
                    $format.LineAlignment = [System.Drawing.StringAlignment]::Center

                    $g.DrawString("BSO", $font, $textBrush, (New-Object System.Drawing.RectangleF 0, 0, $size, $size), $format)
                    $g.Dispose()

                    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
                    $bmp.Dispose()
                }

                New-PwaIcon 512 (Join-Path $ICON_DIR "icon-512.png")
                New-PwaIcon 192 (Join-Path $ICON_DIR "icon-192.png")
            }
            catch {
                Write-Host "WARNING: Failed to generate PWA icons: $($_.Exception.Message)" -ForegroundColor Yellow
            }

            # Write manifest + SW files
            @'
{
  "name": "Balatro Seed Oracle",
  "short_name": "BSO",
  "description": "Balatro Seed Oracle (Avalonia UI running in WebAssembly)",
  "start_url": "./",
  "scope": "./",
  "display": "standalone",
  "display_override": ["standalone", "fullscreen", "minimal-ui", "browser"],
  "background_color": "#202020",
  "theme_color": "#202020",
  "orientation": "any",
  "icons": [
    { "src": "./icons/icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any maskable" },
    { "src": "./icons/icon-512.png", "sizes": "512x512", "type": "image/png", "purpose": "any maskable" }
  ]
}
'@ | Set-Content -Path (Join-Path $OUTPUT_DIR "manifest.json") -Encoding UTF8

            @'
(function () {
  if (!("serviceWorker" in navigator)) return;

  window.addEventListener("load", function () {
    navigator.serviceWorker
      .register("./sw.js", { scope: "./" })
      .then(function (registration) {
        registration.addEventListener("updatefound", function () {
          var sw = registration.installing;
          if (!sw) return;
          sw.addEventListener("statechange", function () {
            if (sw.state === "installed" && navigator.serviceWorker.controller) {
              console.log("[PWA] Update available (refresh to apply).");
            }
          });
        });
      })
      .catch(function (err) {
        console.warn("[PWA] Service worker registration failed:", err);
      });
  });
})();
'@ | Set-Content -Path (Join-Path $OUTPUT_DIR "pwa-register.js") -Encoding UTF8

            @'
/* Balatro Seed Oracle PWA service worker (scope: /BSO/) */
const CACHE_NAME = "bso-pwa-v1";
const CORE_ASSETS = [
  "./",
  "./index.html",
  "./manifest.json",
  "./pwa-register.js",
  "./icons/icon-192.png",
  "./icons/icon-512.png"
];

self.addEventListener("install", (event) => {
  event.waitUntil(
    (async () => {
      const cache = await caches.open(CACHE_NAME);
      await cache.addAll(CORE_ASSETS);
      self.skipWaiting();
    })()
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    (async () => {
      const keys = await caches.keys();
      await Promise.all(
        keys
          .filter((k) => k.startsWith("bso-pwa-") && k !== CACHE_NAME)
          .map((k) => caches.delete(k))
      );
      await self.clients.claim();
    })()
  );
});

function isSameOriginAndInScope(url) {
  return url.origin === self.location.origin && url.pathname.startsWith("/BSO/");
}

self.addEventListener("fetch", (event) => {
  const req = event.request;
  if (req.method !== "GET") return;

  const url = new URL(req.url);
  if (!isSameOriginAndInScope(url)) return;

  if (req.mode === "navigate") {
    event.respondWith(
      (async () => {
        try {
          const fresh = await fetch(req);
          const cache = await caches.open(CACHE_NAME);
          cache.put("./index.html", fresh.clone());
          return fresh;
        } catch {
          const cache = await caches.open(CACHE_NAME);
          return (await cache.match("./index.html")) || (await cache.match("./"));
        }
      })()
    );
    return;
  }

  const cacheable =
    url.pathname.includes("/BSO/_framework/") ||
    url.pathname.includes("/BSO/js/") ||
    url.pathname.includes("/BSO/Assets/") ||
    url.pathname.endsWith(".wasm") ||
    url.pathname.endsWith(".dll") ||
    url.pathname.endsWith(".dat") ||
    url.pathname.endsWith(".webcil") ||
    url.pathname.endsWith(".pdb") ||
    url.pathname.endsWith(".css") ||
    url.pathname.endsWith(".js") ||
    url.pathname.endsWith(".mjs") ||
    url.pathname.endsWith(".png") ||
    url.pathname.endsWith(".ico") ||
    url.pathname.endsWith(".json");

  if (!cacheable) return;

  event.respondWith(
    (async () => {
      const cache = await caches.open(CACHE_NAME);
      const cached = await cache.match(req);
      if (cached) return cached;

      const fresh = await fetch(req);
      if (fresh && fresh.ok) {
        cache.put(req, fresh.clone());
      }
      return fresh;
    })()
  );
});
'@ | Set-Content -Path (Join-Path $OUTPUT_DIR "sw.js") -Encoding UTF8

            # Patch index.html (idempotent)
            $INDEX_PATH = Join-Path $OUTPUT_DIR "index.html"
            if (Test-Path $INDEX_PATH) {
                $html = Get-Content -Path $INDEX_PATH -Raw

                if ($html -notmatch 'rel="manifest"') {
                    $html = $html -replace '<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">', @'
<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">
    <meta name="theme-color" content="#202020">
    <meta name="mobile-web-app-capable" content="yes">
    <meta name="apple-mobile-web-app-capable" content="yes">
    <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">
    <meta name="apple-mobile-web-app-title" content="BSO">
    <link rel="manifest" href="./manifest.json">
    <link rel="apple-touch-icon" href="./icons/icon-192.png">
    <link rel="icon" type="image/png" sizes="192x192" href="./icons/icon-192.png">
    <link rel="icon" type="image/png" sizes="512x512" href="./icons/icon-512.png">
'@
                }

                if ($html -notmatch 'pwa-register\.js') {
                    $html = $html -replace '(</script>\s*)\s*<!-- DuckDB-WASM interop module -->', @'
$1
    <!-- PWA Service Worker -->
    <script src="./pwa-register.js"></script>

    <!-- DuckDB-WASM interop module -->
'@
                }

                Set-Content -Path $INDEX_PATH -Value $html -Encoding UTF8
            }
        }
        else {
            Write-Host "WARNING: BSO output directory not found at $OUTPUT_DIR" -ForegroundColor Yellow
        }
        
        Write-Host "BSO files copied successfully!" -ForegroundColor Green
        Write-Host "BSO is now available at /BSO/" -ForegroundColor Green
    } else {
        Write-Host "Build failed!" -ForegroundColor Red
    }
} else {
    Write-Host "BSO project not found at: $BSO_PROJECT_PATH" -ForegroundColor Yellow
    Write-Host "Please update BSO_PROJECT_PATH in this script" -ForegroundColor Yellow
}
