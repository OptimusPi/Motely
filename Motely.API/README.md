# Motely.API

ASP.NET Core API for JAML search (REST + SignalR), static hosting for BSO and JamlUI, and optional MCP/Cloudflare integration.

- **Run (standalone):** From repo root: `dotnet run --project Motely.API -- --urls http://localhost:3141` (or omit `--urls` to use default port). Swagger: http://localhost:3141/swagger . Alternatively use Motely.TUI “Host API” if the TUI is working.
- **Hosting (BSO, JamlUI, tunnel):** See [HOSTING.md](HOSTING.md).
- **Docker:** See [DOCKER_README.md](DOCKER_README.md).
- **Reference:** [ITEM_NAMES.md](ITEM_NAMES.md) for item/card names; [Knowledge/](Knowledge/) for game mechanics and JAML syntax.
