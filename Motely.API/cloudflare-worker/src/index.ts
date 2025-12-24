/**
 * Balatro Seed Oracle MCP Server - Cloudflare Worker
 * 
 * Implements MCP (Model Context Protocol) 2024-11-05
 * Proxies requests to the Motely.API backend
 * 
 * @see https://spec.modelcontextprotocol.io/
 */

interface Env {
	// Backend API URL (your Motely.API instance)
	MOTELY_API_URL: string;
	
	// Optional: API key for authentication
	API_KEY?: string;
}

// JSON-RPC 2.0 Types
interface JsonRpcRequest {
	jsonrpc: "2.0";
	id: string | number | null;
	method: string;
	params?: unknown;
}

interface JsonRpcResponse {
	jsonrpc: "2.0";
	id: string | number | null;
	result?: unknown;
	error?: {
		code: number;
		message: string;
		data?: unknown;
	};
}

// MCP Protocol Methods
const MCP_METHODS = {
	INITIALIZE: "initialize",
	TOOLS_LIST: "tools/list",
	TOOLS_CALL: "tools/call",
	RESOURCES_LIST: "resources/list",
	RESOURCES_READ: "resources/read",
	PROMPTS_LIST: "prompts/list",
	PROMPTS_GET: "prompts/get",
} as const;

/**
 * Handle MCP protocol requests
 */
async function handleMcpRequest(
	request: JsonRpcRequest,
	env: Env
): Promise<JsonRpcResponse> {
	try {
		switch (request.method) {
			case MCP_METHODS.INITIALIZE:
				return handleInitialize(request);
			
			case MCP_METHODS.TOOLS_LIST:
				return handleToolsList(request);
			
			case MCP_METHODS.TOOLS_CALL:
				return await handleToolCall(request, env);
			
			case MCP_METHODS.RESOURCES_LIST:
				return handleResourcesList(request);
			
			case MCP_METHODS.RESOURCES_READ:
				return await handleResourceRead(request, env);
			
			case MCP_METHODS.PROMPTS_LIST:
				return handlePromptsList(request);
			
			case MCP_METHODS.PROMPTS_GET:
				return await handlePromptGet(request, env);
			
			default:
				return createErrorResponse(
					request.id,
					-32601,
					`Method not found: ${request.method}`
				);
		}
	} catch (error) {
		console.error(`Error handling MCP request ${request.method}:`, error);
		return createErrorResponse(
			request.id,
			-32603,
			`Internal error: ${error instanceof Error ? error.message : String(error)}`
		);
	}
}

/**
 * MCP Initialize - Protocol handshake
 */
function handleInitialize(request: JsonRpcRequest): JsonRpcResponse {
	return createSuccessResponse(request.id, {
		protocolVersion: "2024-11-05",
		capabilities: {
			tools: {},
			resources: {},
			prompts: {},
		},
		serverInfo: {
			name: "balatro-seed-oracle",
			version: "1.0.0",
		},
	});
}

/**
 * List available MCP tools
 */
function handleToolsList(request: JsonRpcRequest): JsonRpcResponse {
	const tools = [
		{
			name: "generate_jaml_filter",
			description:
				"Generate a JAML (Joker Artifact Markup Language) filter from natural language prompt. Returns ONLY the JAML config (no seed search). Use search_seeds tool separately to find seeds.",
			inputSchema: {
				type: "object",
				properties: {
					prompt: {
						type: "string",
						description:
							"Natural language description of desired Balatro seed (e.g., 'Blueprint and Brainstorm in Ante 1', 'Negative Perkeo with Observatory voucher')",
					},
					deck: {
						type: "string",
						description:
							"Optional: Deck name (Red, Blue, Yellow, Green, Black, Ghost, Abandoned, Checkered, Anaglyph, Plasma, Erratic). Defaults to Red.",
						enum: [
							"Red",
							"Blue",
							"Yellow",
							"Green",
							"Black",
							"Ghost",
							"Abandoned",
							"Checkered",
							"Anaglyph",
							"Plasma",
							"Erratic",
						],
					},
					stake: {
						type: "string",
						description:
							"Optional: Stake level (White, Yellow, Orange, Red, Green, Blue, Purple, Gold, Black). Defaults to White.",
						enum: [
							"White",
							"Yellow",
							"Orange",
							"Red",
							"Green",
							"Blue",
							"Purple",
							"Gold",
							"Black",
						],
					},
				},
				required: ["prompt"],
			},
		},
		{
			name: "search_seeds",
			description:
				"Search for Balatro seeds matching a JAML filter. Returns search ID and initial results. Use get_search_status to check progress.",
			inputSchema: {
				type: "object",
				properties: {
					jaml: {
						type: "string",
						description:
							"JAML filter configuration (from generate_jaml_filter or user-provided)",
					},
					deck: {
						type: "string",
						description: "Deck name (defaults to filter's deck or Red)",
					},
					stake: {
						type: "string",
						description: "Stake level (defaults to filter's stake or White)",
					},
					seedCount: {
						type: "integer",
						description:
							"Optional: Number of random seeds to search (default: 1,000,000). Set to 0 for unlimited sequential search.",
					},
				},
				required: ["jaml"],
			},
		},
		{
			name: "get_search_status",
			description:
				"Get status and results of a running or completed seed search.",
			inputSchema: {
				type: "object",
				properties: {
					searchId: {
						type: "string",
						description: "Search ID returned from search_seeds",
					},
				},
				required: ["searchId"],
			},
		},
		{
			name: "analyze_seed",
			description:
				"Analyze a specific Balatro seed to see all items (jokers, vouchers, tags, packs, etc.) across all antes.",
			inputSchema: {
				type: "object",
				properties: {
					seed: {
						type: "string",
						description: "Balatro seed string (e.g., 'ALEEB', '12345678')",
					},
					deck: {
						type: "string",
						description: "Optional: Deck name (defaults to Red)",
					},
					stake: {
						type: "string",
						description: "Optional: Stake level (defaults to White)",
					},
				},
				required: ["seed"],
			},
		},
	];

	return createSuccessResponse(request.id, { tools });
}

/**
 * Call an MCP tool - proxies to backend API
 */
async function handleToolCall(
	request: JsonRpcRequest,
	env: Env
): Promise<JsonRpcResponse> {
	const params = request.params as { name?: string; arguments?: Record<string, unknown> };
	
	if (!params?.name) {
		return createErrorResponse(request.id, -32602, "Invalid tool call parameters");
	}

	const toolName = params.name;
	const args = params.arguments || {};

	try {
		// Proxy tool calls to backend /mcp endpoint
		const backendUrl = env.MOTELY_API_URL.replace(/\/$/, ""); // Remove trailing slash
		const backendRequest = new Request(`${backendUrl}/mcp`, {
			method: "POST",
			headers: {
				"Content-Type": "application/json",
				...(env.API_KEY && { Authorization: `Bearer ${env.API_KEY}` }),
			},
			body: JSON.stringify({
				jsonrpc: "2.0",
				id: request.id,
				method: "tools/call",
				params: {
					name: toolName,
					arguments: args,
				},
			}),
		});

		const backendResponse = await fetch(backendRequest);
		
		if (!backendResponse.ok) {
			throw new Error(
				`Backend API error: ${backendResponse.status} ${backendResponse.statusText}`
			);
		}

		const backendResult = (await backendResponse.json()) as JsonRpcResponse;
		
		// Return the backend's response (it should already be in MCP format)
		return backendResult;
	} catch (error) {
		console.error(`Error calling tool ${toolName}:`, error);
		return createErrorResponse(
			request.id,
			-32603,
			`Tool execution failed: ${error instanceof Error ? error.message : String(error)}`
		);
	}
}

/**
 * List available resources
 */
function handleResourcesList(request: JsonRpcRequest): JsonRpcResponse {
	const resources = [
		{
			uri: "jaml://templates",
			name: "JAML Templates",
			description: "Example JAML filter templates",
			mimeType: "application/yaml",
		},
		{
			uri: "jaml://game-mechanics",
			name: "Game Mechanics",
			description: "Balatro game mechanics and rules documentation",
			mimeType: "text/markdown",
		},
	];

	return createSuccessResponse(request.id, { resources });
}

/**
 * Read a resource - proxies to backend
 */
async function handleResourceRead(
	request: JsonRpcRequest,
	env: Env
): Promise<JsonRpcResponse> {
	const params = request.params as { uri?: string };
	
	if (!params?.uri) {
		return createErrorResponse(request.id, -32602, "Invalid resource read parameters");
	}

	// Proxy to backend
	const backendUrl = env.MOTELY_API_URL.replace(/\/$/, "");
	const backendRequest = new Request(`${backendUrl}/mcp`, {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			...(env.API_KEY && { Authorization: `Bearer ${env.API_KEY}` }),
		},
		body: JSON.stringify({
			jsonrpc: "2.0",
			id: request.id,
			method: "resources/read",
			params: { uri: params.uri },
		}),
	});

	try {
		const backendResponse = await fetch(backendRequest);
		const backendResult = (await backendResponse.json()) as JsonRpcResponse;
		return backendResult;
	} catch (error) {
		return createErrorResponse(
			request.id,
			-32601,
			`Resource reading not yet implemented: ${error instanceof Error ? error.message : String(error)}`
		);
	}
}

/**
 * List available prompts
 */
function handlePromptsList(request: JsonRpcRequest): JsonRpcResponse {
	const prompts = [
		{
			name: "find_joker_build",
			description: "Find Balatro seeds with specific joker combinations",
			arguments: [
				{
					name: "jokers",
					description:
						"Comma-separated list of joker names (e.g., 'Blueprint, Brainstorm, Perkeo')",
					required: true,
				},
				{
					name: "antes",
					description:
						"Optional: Antes to search in (e.g., '1-3' or '1,2,3'). Defaults to all antes.",
					required: false,
				},
			],
		},
		{
			name: "find_economy_build",
			description:
				"Find Balatro seeds with economy items (money-generating jokers, vouchers, tarot cards)",
			arguments: [
				{
					name: "focus",
					description:
						"Optional: Focus area - 'early' (antes 1-3), 'mid' (antes 4-6), or 'late' (antes 7-8). Defaults to early.",
					required: false,
				},
			],
		},
	];

	return createSuccessResponse(request.id, { prompts });
}

/**
 * Get a prompt - proxies to backend
 */
async function handlePromptGet(
	request: JsonRpcRequest,
	env: Env
): Promise<JsonRpcResponse> {
	const params = request.params as { name?: string; arguments?: Record<string, string> };
	
	if (!params?.name) {
		return createErrorResponse(request.id, -32602, "Invalid prompt get parameters");
	}

	// Proxy to backend
	const backendUrl = env.MOTELY_API_URL.replace(/\/$/, "");
	const backendRequest = new Request(`${backendUrl}/mcp`, {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			...(env.API_KEY && { Authorization: `Bearer ${env.API_KEY}` }),
		},
		body: JSON.stringify({
			jsonrpc: "2.0",
			id: request.id,
			method: "prompts/get",
			params: {
				name: params.name,
				arguments: params.arguments || {},
			},
		}),
	});

	try {
		const backendResponse = await fetch(backendRequest);
		const backendResult = (await backendResponse.json()) as JsonRpcResponse;
		return backendResult;
	} catch (error) {
		return createErrorResponse(
			request.id,
			-32601,
			`Prompt generation not yet implemented: ${error instanceof Error ? error.message : String(error)}`
		);
	}
}

/**
 * Create a JSON-RPC success response
 */
function createSuccessResponse(
	id: string | number | null,
	result: unknown
): JsonRpcResponse {
	return {
		jsonrpc: "2.0",
		id,
		result,
	};
}

/**
 * Create a JSON-RPC error response
 */
function createErrorResponse(
	id: string | number | null,
	code: number,
	message: string,
	data?: unknown
): JsonRpcResponse {
	return {
		jsonrpc: "2.0",
		id,
		error: {
			code,
			message,
			...(data && { data }),
		},
	};
}

/**
 * Main Worker handler
 */
export default {
	async fetch(request: Request, env: Env): Promise<Response> {
		// Handle CORS preflight
		if (request.method === "OPTIONS") {
			return new Response(null, {
				status: 204,
				headers: {
					"Access-Control-Allow-Origin": "*",
					"Access-Control-Allow-Methods": "POST, OPTIONS",
					"Access-Control-Allow-Headers": "Content-Type, Authorization",
				},
			});
		}

		// Only accept POST requests
		if (request.method !== "POST") {
			return Response.json(
				{ error: "Method not allowed. Use POST for MCP requests." },
				{ status: 405 }
			);
		}

		// Validate backend URL is configured
		if (!env.MOTELY_API_URL) {
			return Response.json(
				{
					jsonrpc: "2.0",
					id: null,
					error: {
						code: -32603,
						message: "MOTELY_API_URL not configured",
					},
				},
				{ status: 500 }
			);
		}

		try {
			// Parse JSON-RPC request
			const jsonRpcRequest = (await request.json()) as JsonRpcRequest;

			// Validate JSON-RPC format
			if (jsonRpcRequest.jsonrpc !== "2.0") {
				return Response.json(
					createErrorResponse(
						jsonRpcRequest.id,
						-32600,
						"Invalid Request: jsonrpc must be '2.0'"
					),
					{ status: 400 }
				);
			}

			// Handle MCP request
			const response = await handleMcpRequest(jsonRpcRequest, env);

			// Return JSON-RPC response with CORS headers
			return Response.json(response, {
				headers: {
					"Content-Type": "application/json",
					"Access-Control-Allow-Origin": "*",
				},
			});
		} catch (error) {
			console.error("Error processing request:", error);
			return Response.json(
				createErrorResponse(
					null,
					-32700,
					`Parse error: ${error instanceof Error ? error.message : String(error)}`
				),
				{ status: 400 }
			);
		}
	},
} satisfies ExportedHandler<Env>;

