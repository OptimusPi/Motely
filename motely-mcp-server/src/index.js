"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const index_js_1 = require("@modelcontextprotocol/sdk/server/index.js");
const stdio_js_1 = require("@modelcontextprotocol/sdk/server/stdio.js");
const types_js_1 = require("@modelcontextprotocol/sdk/types.js");
const axios_1 = __importDefault(require("axios"));
const server = new index_js_1.Server({
    name: "motely-mcp-server",
    version: "1.0.0",
}, {
    capabilities: {
        tools: {},
    },
});
// List available tools
server.setRequestHandler(types_js_1.ListToolsRequestSchema, async () => ({
    tools: [
        {
            name: "generate_jaml",
            description: "Generate JAML filter from natural language description",
            inputSchema: {
                type: "object",
                properties: {
                    prompt: {
                        type: "string",
                        description: "Natural language description of the Balatro filter you want",
                    },
                },
                required: ["prompt"],
            },
        },
        {
            name: "search_seeds",
            description: "Search for Balatro seeds using a JAML filter",
            inputSchema: {
                type: "object",
                properties: {
                    jaml: {
                        type: "string",
                        description: "JAML filter string to search with",
                    },
                    limit: {
                        type: "number",
                        description: "Maximum number of results to return (default: 10)",
                        default: 10,
                    },
                },
                required: ["jaml"],
            },
        },
    ],
}));
// Handle tool calls
server.setRequestHandler(types_js_1.CallToolRequestSchema, async (request) => {
    const { name, arguments: args } = request.params;
    try {
        if (name === "generate_jaml") {
            const { prompt } = args;
            const response = await axios_1.default.post("https://jamlgenie-minimal.divine-violet-0a93.workers.dev", {
                prompt,
            });
            return {
                content: [
                    {
                        type: "text",
                        text: `Generated JAML:\n\n${response.data.jaml}`,
                    },
                ],
            };
        }
        if (name === "search_seeds") {
            const { jaml, limit = 10 } = args;
            // TODO: Implement actual seed search against Motely API
            return {
                content: [
                    {
                        type: "text",
                        text: `Seed search not yet implemented. Would search with JAML:\n\n${jaml}\n\nLimit: ${limit}`,
                    },
                ],
            };
        }
        throw new Error(`Unknown tool: ${name}`);
    }
    catch (error) {
        return {
            content: [
                {
                    type: "text",
                    text: `Error: ${error instanceof Error ? error.message : String(error)}`,
                },
            ],
            isError: true,
        };
    }
});
async function main() {
    const transport = new stdio_js_1.StdioServerTransport();
    await server.connect(transport);
    console.error("Motely MCP server running on stdio");
}
main().catch((error) => {
    console.error("Server error:", error);
    process.exit(1);
});
//# sourceMappingURL=index.js.map