# Deploy JamlGenie to Cloudflare Pages (BalatroGenie.app)

## Quick Deploy Steps

### 1. Connect Repository to Cloudflare Pages

1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com) → **Pages**
2. Click **Create a project**
3. Connect your Git repository
4. Select the repository containing this code
5. Configure build settings:
   - **Framework preset:** None (or Static)
   - **Build command:** (leave empty - static files)
   - **Build output directory:** `Motely.API/wwwroot/JamlGenie`
   - **Root directory:** (leave empty or set to repository root)

### 2. Set Environment Variables

In Cloudflare Pages project settings, add:

- **Variable name:** `API_BASE_URL`
- **Value:** Your backend API URL (e.g., `https://your-api-server.com` or `http://localhost:3141` for development)

**Important:** The frontend needs to connect to your backend API server. Make sure:
- Your backend API is publicly accessible
- CORS is configured to allow requests from `balatrogenie.app`
- SignalR WebSocket connections are allowed

### 3. Custom Domain

1. In Cloudflare Pages project settings → **Custom domains**
2. Add `balatrogenie.app` (or `www.balatrogenie.app`)
3. Cloudflare will automatically configure DNS

### 4. Deploy

- **Automatic:** Push to your main branch (Cloudflare will auto-deploy)
- **Manual:** Use `wrangler pages deploy` (see below)

## Manual Deploy with Wrangler

```bash
cd Motely.API/wwwroot/JamlGenie
npx wrangler pages deploy . --project-name=balatrogenie
```

## Configuration Options

### Option 1: Environment Variable (Recommended)
Set `API_BASE_URL` in Cloudflare Pages environment variables.

### Option 2: Meta Tag
Edit `index.html` and add:
```html
<meta name="api-base-url" content="https://your-api-server.com">
```

### Option 3: Hardcode (Not Recommended)
Edit `app.js` and change:
```javascript
const apiBaseUrl = "https://your-api-server.com";
```

## Backend API Requirements

Your backend API must:
1. Be publicly accessible
2. Have CORS configured for `balatrogenie.app`
3. Support SignalR WebSocket connections
4. Have the following endpoints:
   - `POST /mcp/prompt` - Process wish/prompt
   - `GET /search?id=...` - Get search results
   - `GET /JAML/?search=...` - View full search results
   - SignalR Hub at `/searchHub`

## CORS Configuration

In your backend API (`Program.cs` or `Startup.cs`), ensure CORS allows:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBalatroGenie", policy =>
    {
        policy.WithOrigins("https://balatrogenie.app", "https://www.balatrogenie.app")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
    });
});
```

## Testing

1. Deploy to Cloudflare Pages
2. Visit `https://balatrogenie.app`
3. Open browser console (F12)
4. Check for API connection errors
5. Try making a wish

## Troubleshooting

### "Failed to fetch" errors
- Check `API_BASE_URL` is set correctly
- Verify backend API is accessible
- Check CORS configuration

### SignalR connection fails
- Ensure WebSocket connections are allowed
- Check backend SignalR hub is configured
- Verify `AllowCredentials()` is set in CORS

### 404 errors
- Check `_redirects` file is present
- Verify build output directory is correct
- Ensure all static files are in the build output




