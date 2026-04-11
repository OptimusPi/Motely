#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { registerTools, bootPromise } from "./api/tools.js";

const server = new McpServer({
  name: "balatro-seed-mcp",
  version: "8.0.0",
});

registerTools(server);

async function main() {
  await bootPromise;
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main();
