# Hosting Motely.API and frontends

How to run the API and host the BSO browser app + JamlUI (including as a PWA) so everything works behind your tunnel and with COOP/COEP for WASM.

## Common mistakes

- **404 for /JamlUI-Vue3-Vite/** — The API only serves **/JamlUI-v99/** (lowercase `v`, number `99`). Use **https://motelyjaml-pi.8pi.me/JamlUI-v99/**.
- **“Wrong wwwroot”** — The tunnel must point at the **Motely.API** process (e.g. port 3141). The API serves **Motely.API/wwwroot/** (not the repo root `public/`). So BSO goes in `Motely.API/wwwroot/BSO/`, JamlUI in `Motely.API/wwwroot/JamlUI-v99/`.
- **JamlUI loads but filter list empty** — JamlUI gets filters from the API (`GET /filters`). Open JamlUI from the **same origin** as the API (e.g. https://motelyjaml-pi.8pi.me/JamlUI-v99/) and ensure the API is running so requests to `/filters` succeed.

## Tunnel (existing)

- **Cloudflare tunnel:** `motelyjaml-pi.8pi.me` → `192.168.0.171:3141` (Motely.API on the Pi).
- Run the API on port 3141 (e.g. via Motely.TUI “Launch API” or `dotnet run` on Motely.API with the right URL/port). The API sets **COOP/COEP** on all responses so WASM (motely-wasm, BSO browser) works without CORS/COEP issues.

## What the API serves

| Path | Content |
|------|--------|
| `/BSO/` | BSO (Balatro Seed Oracle) Avalonia browser build. Put publish output in `wwwroot/BSO/`. See `wwwroot/BSO/README.md`. |
| `/JAML/` | JAML editor (existing static app in wwwroot). |
| `/JamlUI-v99/` | JamlUI (Vue) – mobile-friendly control panel. Build from `jamluiv99`, copy `dist/` into `wwwroot/JamlUI-v99/`. Can be installed as PWA. |
| `/swagger` | Swagger UI. |
| `/searchHub` | SignalR hub for live search (multiplayer / shared searches). |

## 1. Hosting the BSO browser app at /BSO/

1. In the **BalatroSeedOracle** repo, publish the browser target:
   ```bash
   dotnet publish -c Release -f net10.0-browser
   ```
2. Copy the publish folder contents into **Motely.API/wwwroot/BSO/** (see `wwwroot/BSO/README.md`).
3. Users open **https://motelyjaml-pi.8pi.me/BSO/** (or your API base + `/BSO/`).

No extra config: the API already rewrites `/BSO` and `/BSO/` to `/BSO/index.html` and serves static files with COOP/COEP.

## 2. Hosting JamlUI (JAML UI / mobile control panel) at /JamlUI-v99/

JamlUI is the Vue app in `jamluiv99/`. It talks to the same API (SignalR, REST) and is a good “mobile control panel” when hosted by the API.

### Build and copy

```bash
# From repo root
cd jamluiv99
npm ci
npm run build
# Copy dist contents into Motely.API wwwroot:
# e.g. xcopy /E /Y dist\* ..\Motely.API\wwwroot\JamlUI-v99\
# (Windows) or cp -r dist/* ../Motely.API/wwwroot/JamlUI-v99/
```

Ensure `wwwroot/JamlUI-v99/` contains `index.html` and `assets/`. Then open **https://motelyjaml-pi.8pi.me/JamlUI-v99/**.

### PWA (installable)

JamlUI includes a `manifest.json` and can be “Add to home screen” when served from the API. Install from the browser menu when on `/JamlUI-v99/`; the app will use the same API origin (no CORS issues).

### API base URL

If JamlUI runs on the same origin as the API (e.g. motelyjaml-pi.8pi.me), SignalR and fetch use relative URLs. If you ever host it elsewhere, set the API base URL in the app config so it points at `https://motelyjaml-pi.8pi.me`.

## 3. v0 app and “magically navigate to BSO”

- **Same origin:** If the v0 app is served by the same Motely.API (e.g. at `/` or another path), add a link or redirect to `/BSO/` so users can open the full BSO browser app.
- **Different origin:** Link to `https://motelyjaml-pi.8pi.me/BSO/` from the v0 app.

## 4. Multiplayer / shared searches

SignalR hub at `/searchHub` is unchanged. All clients (TUI, JamlUI, any frontend using the same API) share the same search state when connected to the same Motely.API instance. No extra steps for “multiplayer” beyond running the API and connecting clients to it (e.g. via the tunnel URL).

## 5. Quick checklist

- [ ] API runs on 3141 (or your port) and tunnel points to it.
- [ ] BSO browser build copied to `wwwroot/BSO/` → **https://motelyjaml-pi.8pi.me/BSO/**.
- [ ] JamlUI built and copied to `wwwroot/JamlUI-v99/` → **https://motelyjaml-pi.8pi.me/JamlUI-v99/** (optional PWA).
- [ ] COOP/COEP already set by the API; no extra CORS/COEP config needed for WASM.
