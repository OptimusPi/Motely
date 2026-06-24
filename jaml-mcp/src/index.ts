import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { validate, getDiagnostics, getCompletions, getHover, Vocab } from "jaml-lang";

const server = new McpServer({
  name: "jaml-mcp",
  version: "1.0.0",
});

// ── validate ──────────────────────────────────────────────────────────────────

server.tool(
  "jaml_validate",
  "Validate a JAML filter string. Returns errors and warnings with line/character positions.",
  { text: z.string().describe("Full JAML filter text to validate.") },
  async ({ text }) => {
    const diags = getDiagnostics(text);
    if (diags.length === 0) {
      return { content: [{ type: "text", text: "✓ No errors." }] };
    }
    const lines = diags.map((d: ReturnType<typeof getDiagnostics>[number]) => {
      const sev = d.severity === 1 ? "error" : "warning";
      return `${sev} [${d.range.start.line + 1}:${d.range.start.character + 1}] ${d.message}`;
    });
    return { content: [{ type: "text", text: lines.join("\n") }] };
  },
);

// ── completions ───────────────────────────────────────────────────────────────

server.tool(
  "jaml_complete",
  "Get JAML completions at a cursor offset. Pass the full text and the byte offset.",
  {
    text:   z.string().describe("Full JAML filter text."),
    offset: z.number().int().describe("Character offset of the cursor."),
  },
  async ({ text, offset }) => {
    const items = getCompletions(text, offset);
    if (items.length === 0) {
      return { content: [{ type: "text", text: "No completions at this position." }] };
    }
    const lines = items.map((i) => `[${i.kind}] ${i.label}${i.detail ? "  — " + i.detail : ""}`);
    return { content: [{ type: "text", text: lines.join("\n") }] };
  },
);

// ── hover ─────────────────────────────────────────────────────────────────────

server.tool(
  "jaml_hover",
  "Get hover documentation for the token at a cursor offset.",
  {
    text:   z.string().describe("Full JAML filter text."),
    offset: z.number().int().describe("Character offset of the cursor."),
  },
  async ({ text, offset }) => {
    const info = getHover(text, offset);
    if (!info) return { content: [{ type: "text", text: "No hover info at this position." }] };
    return { content: [{ type: "text", text: info.markdown }] };
  },
);

// ── vocab ─────────────────────────────────────────────────────────────────────

server.tool(
  "jaml_vocab",
  "Dump the full JAML vocabulary: discriminators, clause keys, source keys, enum members. Use this to know what's valid before writing a filter.",
  {
    topic: z.enum(["discriminators", "sources", "enums", "all"]).default("all")
      .describe("Which part of the vocab to return."),
    discriminator: z.string().optional()
      .describe("If set, return clause+source keys for just this discriminator."),
  },
  async ({ topic, discriminator }) => {
    const lines: string[] = [];

    if (discriminator) {
      const canon = Vocab.Discriminators.find(
        (d) => d.toLowerCase() === discriminator.toLowerCase()
      );
      if (!canon) {
        return { content: [{ type: "text", text: `Unknown discriminator '${discriminator}'.` }] };
      }
      const clause = Vocab.DiscriminatorClauseKeys[canon] ?? [];
      const src    = Vocab.DiscriminatorSourceKeys[canon] ?? [];
      const valEnum = Vocab.DiscriminatorValueEnum[canon];
      const members = valEnum ? (Vocab.Enums[valEnum] ?? []) : [];
      lines.push(`discriminator: ${canon}`);
      if (valEnum) lines.push(`  value enum: ${valEnum} → [${members.join(", ")}]`);
      lines.push(`  clause keys: [${clause.join(", ")}]`);
      if (src.length) lines.push(`  source keys: [${src.join(", ")}]`);
    } else {
      if (topic === "discriminators" || topic === "all") {
        lines.push("=== discriminators ===");
        lines.push(Vocab.Discriminators.join(", "));
      }
      if (topic === "sources" || topic === "all") {
        lines.push("\n=== source keys per discriminator ===");
        for (const [disc, keys] of Object.entries(Vocab.DiscriminatorSourceKeys)) {
          lines.push(`  ${disc}: [${(keys as string[]).join(", ")}]`);
        }
      }
      if (topic === "enums" || topic === "all") {
        lines.push("\n=== enums ===");
        for (const [name, members] of Object.entries(Vocab.Enums)) {
          lines.push(`  ${name}: [${(members as string[]).join(", ")}]`);
        }
      }
    }

    return { content: [{ type: "text", text: lines.join("\n") }] };
  },
);

// ── boot ─────────────────────────────────────────────────────────────────────

const transport = new StdioServerTransport();
await server.connect(transport);
