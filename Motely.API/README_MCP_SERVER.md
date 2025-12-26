# Balatro Seed Oracle MCP Server

> **MCP (Model Context Protocol) server for searching Balatro seeds using natural language**

[![Docker](https://img.shields.io/badge/docker-ready-blue)](https://hub.docker.com/r/yourusername/balatro-seed-oracle-mcp)
[![MCP](https://img.shields.io/badge/MCP-2024--11--05-green)](https://spec.modelcontextprotocol.io/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## 🎯 What is this?

An MCP server that lets AI assistants (Claude, Cursor, Copilot) search for Balatro seeds using natural language. Just ask:

> "Find me a seed with Blueprint and Brainstorm in Ante 1"

And the AI will:
1. Generate a JAML filter from your request
2. Search millions of seeds
3. Return matching results

## 🚀 Quick Start

### Option 1: Docker (Recommended)

```bash
# Pull and run
docker run -i balatro-seed-oracle-mcp:latest
```

### Option 2: Standalone Binary

Download from [GitHub Releases](https://github.com/yourusername/balatro-seed-oracle-mcp/releases)

### Option 3: npm (Wrapper)

```bash
npm install -g balatro-seed-oracle-mcp
balatro-seed-oracle-mcp
```

## 📦 Installation

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "balatro-seed-oracle-mcp:latest"]
    }
  }
}
```

Or use HTTP transport:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "http://localhost:3141/mcp"
    }
  }
}
```

See [CLAUDE_DESKTOP_SETUP.md](CLAUDE_DESKTOP_SETUP.md) for detailed instructions.

### Cursor IDE

Add to Cursor settings:

```json
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "url": "http://localhost:3141/mcp"
    }
  }
}
```

## 🛠️ Available Tools

The MCP server provides 4 tools:

1. **`generate_jaml_filter`** - Convert natural language to JAML filter
2. **`search_seeds`** - Search for seeds matching a JAML filter
3. **`get_search_status`** - Check progress of a running search
4. **`analyze_seed`** - Analyze a specific seed to see all items

## 📖 Usage Examples

### Example 1: Find Jokers

> "Use balatro-seed-oracle to find a seed with Blueprint and Brainstorm in Ante 1"

### Example 2: Find Economy Build

> "Find me a seed with good economy items like Temperance and GoldenTicket"

### Example 3: Find Specific Boss

> "Search for seeds with TheGoad boss blind"

## 🔧 Configuration

### Environment Variables

- `Cloudflare__WorkersAI__WorkerUrl` - Cloudflare Worker URL for AI (required for JamlGenie)
- `Cloudflare__WorkersAI__Model` - AI model (default: `@cf/meta/llama-3.1-8b-instruct-fp8`)
- `ASPNETCORE_URLS` - Server URLs (default: `http://0.0.0.0:3141`)

### Volume Mounts (Docker)

- `/app/JamlFilters` - JAML filter files
- `/app/WordLists` - Seed wordlists
- `/app/SearchDatabases` - Search result databases
- `/app/appsettings.json` - Configuration file

## 📚 Documentation

- [Installation Guide](DOCKER_README.md) - Docker setup
- [Claude Desktop Setup](CLAUDE_DESKTOP_SETUP.md) - Detailed Claude Desktop instructions
- [MCP Clients](MCP_CLIENTS.md) - Supported AI clients
- [Distribution Plan](DISTRIBUTION_PLAN.md) - How to share this server

## 🤝 Contributing

Contributions welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## 📄 License

MIT License - see [LICENSE](LICENSE) file

## 🙏 Acknowledgments

- Built with [Motely](https://github.com/yourusername/motely) search engine
- Uses [Cloudflare Workers AI](https://developers.cloudflare.com/workers-ai/) for natural language processing
- Implements [MCP Protocol 2024-11-05](https://spec.modelcontextprotocol.io/)

---

**Made for the Balatro community** 🎰

