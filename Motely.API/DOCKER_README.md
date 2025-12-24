# Docker Installation Guide

## Quick Start

### Option 1: Docker Run (Stdio Mode for Claude Desktop)

```bash
docker run -i \
  -v $(pwd)/JamlFilters:/app/JamlFilters \
  -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
  balatro-seed-oracle-mcp:latest
```

### Option 2: Docker Compose (HTTP Mode)

```bash
cd Motely.API
docker-compose up -d
```

The server will be available at `http://localhost:3141/mcp`

## Building the Image

### From Source

```bash
# Build from current directory
docker build -f Motely.API/Dockerfile -t balatro-seed-oracle-mcp:latest ..

# Or use docker-compose
cd Motely.API
docker-compose build
```

### From Docker Hub (Once Published)

```bash
docker pull balatro-seed-oracle-mcp:latest
```

## Usage Modes

### Stdio Mode (MCP Clients)

For Claude Desktop and other stdio-based MCP clients:

```bash
docker run -i balatro-seed-oracle-mcp:latest
```

The container will read JSON-RPC from stdin and write to stdout.

### HTTP Mode

For HTTP-based MCP clients (Cursor, Copilot):

```bash
docker run -p 3141:3141 \
  -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
  balatro-seed-oracle-mcp:latest \
  --urls http://0.0.0.0:3141
```

## Configuration

### Environment Variables

- `ASPNETCORE_ENVIRONMENT`: `Production` (default) or `Development`
- `ASPNETCORE_URLS`: Server URLs (default: `http://0.0.0.0:3141`)
- `Cloudflare__WorkersAI__WorkerUrl`: Cloudflare Worker URL for JamlGenie
- `Cloudflare__WorkersAI__Model`: AI model (default: `@cf/meta/llama-3.1-8b-instruct-fp8`)

### Volume Mounts

- `/app/JamlFilters`: JAML filter files
- `/app/WordLists`: Seed wordlists
- `/app/SearchDatabases`: Search result databases
- `/app/appsettings.json`: Configuration file

### Example with Volumes

```bash
docker run -i \
  -v $(pwd)/JamlFilters:/app/JamlFilters \
  -v $(pwd)/WordLists:/app/WordLists \
  -v $(pwd)/SearchDatabases:/app/SearchDatabases \
  -v $(pwd)/appsettings.json:/app/appsettings.json:ro \
  -e Cloudflare__WorkersAI__WorkerUrl=https://your-worker.workers.dev \
  balatro-seed-oracle-mcp:latest
```

## Claude Desktop Configuration

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "docker",
      "args": [
        "run",
        "-i",
        "--rm",
        "balatro-seed-oracle-mcp:latest"
      ]
    }
  }
}
```

## Troubleshooting

### Container exits immediately

- Check logs: `docker logs balatro-seed-oracle-mcp`
- Ensure stdin is open: Use `-i` flag
- Check environment variables

### Can't connect via HTTP

- Verify port mapping: `-p 3141:3141`
- Check firewall settings
- Verify container is running: `docker ps`

### Missing filters/wordlists

- Mount volumes correctly
- Check file permissions
- Verify paths in container

## Development

### Build with Debug Symbols

```bash
docker build -f Motely.API/Dockerfile \
  --build-arg BUILD_CONFIGURATION=Debug \
  -t balatro-seed-oracle-mcp:dev ..
```

### Run with Debugging

```bash
docker run -i \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -v $(pwd):/app/src \
  balatro-seed-oracle-mcp:dev
```

