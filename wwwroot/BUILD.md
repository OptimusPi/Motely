# JamlUI3 Build Instructions

## Development

From `frontend/JamlUI3/`:

```bash
npm install    # Install dependencies (first time only)
npm run dev    # Start Vite dev server on http://localhost:5173
```

The dev server proxies API requests to `http://192.168.0.171:3141`.

## Production Build

From `frontend/JamlUI3/`:

```bash
npm run build  # Builds to ../../wwwroot/JamlUI3/
```

The build output goes directly to `wwwroot/JamlUI3/` where it will be served by the ASP.NET Core API.

## File Structure

- **Source files**: `frontend/JamlUI3/`
- **Built output**: `wwwroot/JamlUI3/`
- **Served at**: `http://localhost:3141/JamlUI3/`

## Notes

- The `base: '/JamlUI3/'` in vite.config.js ensures assets are loaded from the correct path
- After building, the `wwwroot/JamlUI3/` folder contains only the built files ready to serve
- Source files (src/, node_modules/, etc.) are kept separate in `frontend/JamlUI3/`

