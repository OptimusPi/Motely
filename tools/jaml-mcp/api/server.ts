import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";

// Set MCP_API_KEY to require Bearer token auth on the HTTP endpoint.
const API_KEY = process.env.MCP_API_KEY;

const MCP_LITE = /^1\s*$/.test(process.env.MCP_LITE ?? "");

async function createTransport(): Promise<StreamableHTTPServerTransport> {
  const transport = new StreamableHTTPServerTransport({
    sessionIdGenerator: undefined,
    enableJsonResponse: true,
  });
  const server = new McpServer(
    { name: "jaml-mcp", version: "1.0.0" },
    { capabilities: { tools: {} } }
  );
  if (MCP_LITE) {
    server.registerTool(
      "ping",
      { description: "Health check (MCP_LITE=1)." },
      async () => ({
        content: [{ type: "text" as const, text: "pong" }],
      })
    );
  } else {
    const { registerTools } = await import("./tools.js");
    registerTools(server);
  }
  await server.connect(transport);
  return transport;
}

function verifyBearer(req: { headers?: Record<string, string | string[] | undefined> }): boolean {
  if (!API_KEY) return true;
  const raw = req.headers?.authorization;
  const auth = Array.isArray(raw) ? raw[0] : raw;
  if (!auth || typeof auth !== "string" || !auth.startsWith("Bearer ")) return false;
  return auth.slice(7) === API_KEY;
}

/** Vercel may set `req.body`; avoid double-consuming the stream (hang). */
async function jsonBodyForMcp(req: any): Promise<unknown | undefined> {
  if (req.method !== "POST" && req.method !== "DELETE") return undefined;
  if (req.body != null) {
    if (typeof req.body === "string") {
      try {
        return JSON.parse(req.body);
      } catch {
        return undefined;
      }
    }
    if (Buffer.isBuffer(req.body)) {
      const s = req.body.toString("utf8");
      return s ? JSON.parse(s) : undefined;
    }
    return req.body;
  }
  const chunks: Buffer[] = [];
  for await (const chunk of req) {
    chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  }
  const raw = Buffer.concat(chunks).toString("utf8");
  return raw ? JSON.parse(raw) : undefined;
}

/** Vercel Node serverless: `IncomingMessage` + `ServerResponse` (same shape as `api/search.ts`). */
async function mcpNodeHandler(req: any, res: any): Promise<void> {
  if (!verifyBearer(req)) {
    res.statusCode = 401;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: "Unauthorized" }));
    return;
  }
  try {
    const transport = await createTransport();
    const parsedBody = await jsonBodyForMcp(req);
    await transport.handleRequest(req, res, parsedBody);
  } catch (err) {
    console.error(err);
    if (!res.headersSent) {
      res.statusCode = 500;
      res.setHeader("Content-Type", "application/json");
      res.end(JSON.stringify({ error: "Internal Server Error" }));
    }
  }
}

export default mcpNodeHandler;
export { mcpNodeHandler as GET, mcpNodeHandler as POST, mcpNodeHandler as DELETE };
