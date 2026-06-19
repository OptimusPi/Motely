import { JsonNode } from "../engine.js";

/**
 * Build a json-render spec for search results.
 *
 * Pure function. Converts raw Motely search data into a JsonNode tree.
 * The tree renders into a full search UI via `render(spec, registry)`.
 */

export interface SearchResult {
  seed: string;
  score: number;
  highlights?: string[];
  jokers?: string[];
  edition?: string;
}

export interface SearchParams {
  status: "idle" | "running" | "completed" | "error";
  seedsSearched?: string;
  matchesFound?: number;
  seedsPerSecond?: number;
  elapsed?: string;
  error?: string | null;
  results: SearchResult[];
}

export function buildSearchSpec(params: SearchParams): JsonNode {
  const children: JsonNode[] = [];

  // ── Search Stats ──
  children.push({
    type: "SearchStats",
    props: {
      status: params.status,
      seedsSearched: params.seedsSearched,
      matchesFound: params.matchesFound,
      seedsPerSecond: params.seedsPerSecond,
      elapsed: params.elapsed,
    },
  });

  // ── Error Banner ──
  if (params.error) {
    children.push({
      type: "ErrorBanner",
      props: {
        message: params.error,
        onDismiss: true,
      },
    });
  }

  // ── Spacer ──
  children.push({ type: "Spacer", props: { size: 16 } });

  // ── Results ──
  if (params.results.length > 0) {
    const resultCards: JsonNode[] = params.results.map((r, i) => ({
      type: "SeedCard",
      props: {
        seed: r.seed,
        score: r.score,
        rank: i + 1,
        highlights: r.highlights,
        jokers: r.jokers,
        edition: r.edition,
        onClick: true,
      },
    }));

    children.push({
      type: "Panel",
      props: {
        title: `Results (${params.matchesFound ?? params.results.length})`,
        subtitle: params.status === "running" ? "More matches arriving…" : undefined,
      },
      children: [
        {
          type: "Grid",
          props: { columns: 1, gap: 12 },
          children: resultCards,
        },
      ],
    });
  } else if (params.status === "completed") {
    children.push({
      type: "Panel",
      props: { title: "No matches found", variant: "muted" },
      children: [
        {
          type: "Text",
          props: {
            body: "Try relaxing your filter or checking for typos.",
            variant: "muted",
          },
        },
      ],
    });
  } else if (params.status === "running") {
    children.push({
      type: "LoadingPulse",
      props: { text: "Searching seeds…" },
    });
  }

  return {
    type: "Stack",
    props: { gap: 16 },
    children,
  };
}
