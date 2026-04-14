#!/usr/bin/env node
import { createRequire } from "node:module";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { bootPromise, registerTools } from "./api/tools.js";

const pkg = createRequire(import.meta.url)("./package.json") as {
  name: string;
  version: string;
};

const server = new McpServer({
  name: pkg.name,
  version: pkg.version,
});

registerTools(server);

async function main() {
  await bootPromise;
  const transport = new StdioServerTransport();
  await server.connect(transport);
}

main();
