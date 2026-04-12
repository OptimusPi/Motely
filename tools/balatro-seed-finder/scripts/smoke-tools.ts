import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { bootPromise, registerTools } from "../api/tools.js";

await bootPromise;
const s = new McpServer({ name: "t", version: "1" });
registerTools(s);
console.log("registerTools ok");
