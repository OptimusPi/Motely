#!/usr/bin/env node
/**
 * Motely MCP Server - Node.js wrapper for the .NET MCP server
 * 
 * This spawns the compiled .NET executable and forwards stdio for MCP communication.
 * Can also run in pure JS mode for basic operations if .NET runtime is unavailable.
 */

import { spawn, ChildProcess } from 'child_process';
import { createInterface, Interface } from 'readline';
import { existsSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

interface JsonRpcRequest {
  jsonrpc: string;
  id: string | number | null;
  method: string;
  params?: unknown;
}

interface JsonRpcResponse {
  jsonrpc: string;
  id: string | number | null;
  result?: unknown;
  error?: {
    code: number;
    message: string;
    data?: unknown;
  };
}

class MotelyMcpServer {
  private dotnetProcess: ChildProcess | null = null;
  private dotnetPath: string | null = null;
  private rl: Interface | null = null;
  private pendingRequests: Map<string | number, (response: JsonRpcResponse) => void> = new Map();

  constructor() {
    this.findDotnetExecutable();
  }

  private findDotnetExecutable(): void {
    // Look for the .NET executable in various locations
    const possiblePaths = [
      // Relative to this script (npm package bin folder)
      join(__dirname, '..', 'bin', 'Motely.MCP.exe'),
      join(__dirname, '..', 'bin', 'Motely.MCP'),
      // Development paths
      join(__dirname, '..', '..', 'Motely.MCP', 'bin', 'Release', 'net10.0', 'Motely.MCP.exe'),
      join(__dirname, '..', '..', 'Motely.MCP', 'bin', 'Debug', 'net10.0', 'Motely.MCP.exe'),
      join(__dirname, '..', '..', 'Motely.MCP', 'publish', 'Motely.MCP.exe'),
      // Linux/Mac paths
      join(__dirname, '..', '..', 'Motely.MCP', 'bin', 'Release', 'net10.0', 'Motely.MCP'),
      join(__dirname, '..', '..', 'Motely.MCP', 'bin', 'Debug', 'net10.0', 'Motely.MCP'),
    ];

    // Check MOTELY_MCP_PATH env var first
    const envPath = process.env.MOTELY_MCP_PATH;
    if (envPath && existsSync(envPath)) {
      this.dotnetPath = envPath;
      return;
    }

    for (const p of possiblePaths) {
      if (existsSync(p)) {
        this.dotnetPath = p;
        return;
      }
    }

    // Fall back to running dotnet directly
    this.dotnetPath = null;
  }

  async start(): Promise<void> {
    if (this.dotnetPath) {
      await this.startDotnetProcess();
    } else {
      // Run in fallback JS mode
      this.startJsMode();
    }
  }

  private async startDotnetProcess(): Promise<void> {
    console.error(`[motely-mcp] Starting .NET MCP server: ${this.dotnetPath}`);

    this.dotnetProcess = spawn(this.dotnetPath!, ['--mcp-stdio'], {
      env: { ...process.env, MCP_MODE: 'stdio' },
      stdio: ['pipe', 'pipe', 'pipe'],
    });

    // Forward stdout from .NET process to our stdout
    this.dotnetProcess.stdout?.on('data', (data: Buffer) => {
      process.stdout.write(data);
    });

    // Forward stderr from .NET process to our stderr (for logging)
    this.dotnetProcess.stderr?.on('data', (data: Buffer) => {
      process.stderr.write(data);
    });

    // Forward stdin to .NET process
    process.stdin.pipe(this.dotnetProcess.stdin!);

    // Handle process exit
    this.dotnetProcess.on('exit', (code) => {
      console.error(`[motely-mcp] .NET process exited with code ${code}`);
      process.exit(code ?? 1);
    });

    this.dotnetProcess.on('error', (err) => {
      console.error(`[motely-mcp] Failed to start .NET process: ${err.message}`);
      console.error('[motely-mcp] Falling back to JS mode...');
      this.startJsMode();
    });
  }

  private startJsMode(): void {
    console.error('[motely-mcp] Running in JavaScript fallback mode (limited functionality)');

    this.rl = createInterface({
      input: process.stdin,
      output: process.stdout,
      terminal: false,
    });

    this.rl.on('line', async (line: string) => {
      if (!line.trim()) return;

      try {
        const request = JSON.parse(line) as JsonRpcRequest;
        const response = await this.handleRequest(request);
        console.log(JSON.stringify(response));
      } catch (err) {
        const errorResponse: JsonRpcResponse = {
          jsonrpc: '2.0',
          id: null,
          error: {
            code: -32700,
            message: `Parse error: ${(err as Error).message}`,
          },
        };
        console.log(JSON.stringify(errorResponse));
      }
    });

    this.rl.on('close', () => {
      process.exit(0);
    });
  }

  private async handleRequest(request: JsonRpcRequest): Promise<JsonRpcResponse> {
    const { method, id, params } = request;

    try {
      switch (method) {
        case 'initialize':
          return this.handleInitialize(id);
        case 'tools/list':
          return this.handleToolsList(id);
        case 'tools/call':
          return await this.handleToolCall(id, params);
        case 'resources/list':
          return this.handleResourcesList(id);
        case 'prompts/list':
          return this.handlePromptsList(id);
        default:
          return {
            jsonrpc: '2.0',
            id,
            error: {
              code: -32601,
              message: `Method not found: ${method}`,
            },
          };
      }
    } catch (err) {
      return {
        jsonrpc: '2.0',
        id,
        error: {
          code: -32603,
          message: `Internal error: ${(err as Error).message}`,
        },
      };
    }
  }

  private handleInitialize(id: string | number | null): JsonRpcResponse {
    return {
      jsonrpc: '2.0',
      id,
      result: {
        protocolVersion: '2024-11-05',
        capabilities: {
          tools: {},
          resources: {},
          prompts: {},
        },
        serverInfo: {
          name: 'motely-mcp-server',
          version: '1.0.0',
        },
      },
    };
  }

  private handleToolsList(id: string | number | null): JsonRpcResponse {
    return {
      jsonrpc: '2.0',
      id,
      result: {
        tools: [
          {
            name: 'read_jaml_file',
            description: 'Read a JAML filter file and return its contents',
            inputSchema: {
              type: 'object',
              properties: {
                path: {
                  type: 'string',
                  description: 'Path to the JAML file (absolute or relative to JamlFilters directory)',
                },
              },
              required: ['path'],
            },
          },
          {
            name: 'write_jaml_file',
            description: 'Write content to a JAML filter file',
            inputSchema: {
              type: 'object',
              properties: {
                path: {
                  type: 'string',
                  description: 'Path to write the JAML file',
                },
                content: {
                  type: 'string',
                  description: 'JAML content to write',
                },
              },
              required: ['path', 'content'],
            },
          },
          {
            name: 'validate_jaml',
            description: 'Validate JAML syntax and schema. Returns errors if invalid.',
            inputSchema: {
              type: 'object',
              properties: {
                content: {
                  type: 'string',
                  description: 'JAML content to validate',
                },
              },
              required: ['content'],
            },
          },
          {
            name: 'list_jaml_filters',
            description: 'List all available JAML filter files',
            inputSchema: {
              type: 'object',
              properties: {},
            },
          },
        ],
      },
    };
  }

  private async handleToolCall(id: string | number | null, params: unknown): Promise<JsonRpcResponse> {
    const { name, arguments: args } = params as { name: string; arguments: Record<string, unknown> };

    // In JS fallback mode, we can only do basic file operations
    return {
      jsonrpc: '2.0',
      id,
      error: {
        code: -32603,
        message: 'Tool calls require the .NET runtime. Please ensure Motely.MCP is built and accessible.',
      },
    };
  }

  private handleResourcesList(id: string | number | null): JsonRpcResponse {
    return {
      jsonrpc: '2.0',
      id,
      result: {
        resources: [
          {
            uri: 'jaml://schema',
            name: 'JAML Schema',
            description: 'JSON Schema for JAML filter files',
            mimeType: 'application/json',
          },
        ],
      },
    };
  }

  private handlePromptsList(id: string | number | null): JsonRpcResponse {
    return {
      jsonrpc: '2.0',
      id,
      result: { prompts: [] },
    };
  }

  stop(): void {
    if (this.dotnetProcess) {
      this.dotnetProcess.kill();
      this.dotnetProcess = null;
    }
    if (this.rl) {
      this.rl.close();
      this.rl = null;
    }
  }
}

// Main entry point
const server = new MotelyMcpServer();

process.on('SIGINT', () => {
  server.stop();
  process.exit(0);
});

process.on('SIGTERM', () => {
  server.stop();
  process.exit(0);
});

server.start().catch((err) => {
  console.error(`[motely-mcp] Fatal error: ${err.message}`);
  process.exit(1);
});
