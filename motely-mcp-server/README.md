# Motely MCP Server

Model Context Protocol (MCP) server for Balatro seed searching and JAML filter editing.

## Features

- **JAML File Management**: Read, write, and validate JAML filter files
- **Seed Searching**: Run JAML filters to find matching Balatro seeds
- **Seed Analysis**: Analyze specific seeds to see all items across all antes
- **JAML Generation**: Generate JAML filters from natural language prompts

## Installation

### Via npm (recommended)

```bash
npm install -g @balatroseedoracle/motely-mcp-server
```

### From source

```bash
git clone https://github.com/balatroseedoracle/Motely.git
cd Motely/motely-mcp-server
npm install
npm run build
```

## Requirements

- Node.js 18+
- .NET 10.0+ runtime (for full functionality)

## Usage

### With Claude Desktop / Cursor

Add to your MCP configuration:

```json
{
  "mcpServers": {
    "motely": {
      "command": "npx",
      "args": ["@balatroseedoracle/motely-mcp-server"],
      "env": {}
    }
  }
}
```

### With pre-built binary

If you have the pre-built Motely.MCP binary:

```json
{
  "mcpServers": {
    "motely": {
      "command": "motely-mcp-server",
      "env": {
        "MOTELY_MCP_PATH": "/path/to/Motely.MCP.exe"
      }
    }
  }
}
```

### Direct stdio mode

```bash
# Run directly
motely-mcp-server

# Or via npx
npx @balatroseedoracle/motely-mcp-server
```

## Available Tools

### JAML File Operations

- **read_jaml_file** - Read a JAML filter file
- **write_jaml_file** - Write content to a JAML filter file
- **validate_jaml** - Validate JAML syntax and schema
- **list_jaml_filters** - List all available JAML filter files

### Seed Operations

- **run_jaml_filter** - Run a JAML filter and return matching seeds
- **analyze_seed** - Analyze a specific seed to see all items
- **verify_seed** - Check if a seed matches a JAML filter

### AI-Assisted

- **generate_jaml_filter** - Generate a JAML filter from natural language

## JAML Schema

The JAML schema is available at `jaml://schema` resource or in the repository at `jaml.schema.json`.

## Development

```bash
# Build the .NET server
cd Motely.MCP
dotnet build -c Release

# Build the npm package
cd ../motely-mcp-server
npm run build

# Run in development
npm start
```

## License

MIT
