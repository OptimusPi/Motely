import { z } from "zod";
import { defineCatalog } from "@json-render/core";
import { schema } from "@json-render/react/schema";

/**
 * Balatro Seed App — json-render Catalog
 *
 * Defines the component vocabulary the AI is allowed to generate.
 * All components are constrained to Balatro-specific UI patterns.
 */
export const balatroCatalog = defineCatalog(schema, {
  components: {
    // ── Layout ──
    Panel: {
      props: z.object({
        title: z.string().optional(),
        subtitle: z.string().optional(),
        variant: z.enum(["default", "accent", "muted"]).default("default"),
      }),
      description: "A framed panel with optional title for grouping content",
    },
    Stack: {
      props: z.object({
        gap: z.number().default(12),
        align: z.enum(["start", "center", "end", "stretch"]).default("stretch"),
      }),
      description: "Vertical flex container with configurable gap",
    },
    Grid: {
      props: z.object({
        columns: z.number().min(1).max(4).default(3),
        gap: z.number().default(16),
      }),
      description: "Grid layout for cards and results",
    },

    // ── Seed Results ──
    SeedCard: {
      props: z.object({
        seed: z.string(),
        score: z.number().optional(),
        rank: z.number().optional(),
        highlights: z.array(z.string()).optional(),
        jokers: z.array(z.string()).optional(),
        edition: z.string().optional(),
      }),
      description: "A single seed result card showing seed code, score, and highlights",
    },
    SeedList: {
      props: z.object({
        seeds: z.array(z.string()),
        scores: z.array(z.number()).optional(),
        total: z.number().optional(),
      }),
      description: "Paginated list of seed results with scores",
    },
    SearchStats: {
      props: z.object({
        status: z.enum(["idle", "running", "completed", "error"]).default("idle"),
        seedsSearched: z.string().optional(),
        matchesFound: z.number().optional(),
        seedsPerSecond: z.number().optional(),
        elapsed: z.string().optional(),
      }),
      description: "Search progress panel with metrics",
    },

    // ── Balatro Cards ──
    JokerCard: {
      props: z.object({
        name: z.string(),
        edition: z.string().optional(),
        seal: z.string().optional(),
        eternal: z.boolean().optional(),
        perishable: z.boolean().optional(),
        rental: z.boolean().optional(),
      }),
      description: "A Balatro joker card with edition, seal, and modifiers",
    },
    TarotCard: {
      props: z.object({
        name: z.string(),
        edition: z.string().optional(),
      }),
      description: "A Tarot card",
    },
    PlanetCard: {
      props: z.object({
        name: z.string(),
        edition: z.string().optional(),
      }),
      description: "A Planet card",
    },
    SpectralCard: {
      props: z.object({
        name: z.string(),
        edition: z.string().optional(),
      }),
      description: "A Spectral card",
    },
    PlayingCard: {
      props: z.object({
        rank: z.string(),
        suit: z.string(),
        enhancement: z.string().optional(),
        seal: z.string().optional(),
        edition: z.string().optional(),
      }),
      description: "A standard playing card (A-K, 4 suits) with enhancement/seal/edition",
    },

    // ── Shop & Route ──
    ShopQueue: {
      props: z.object({
        ante: z.number().min(1).max(8),
        items: z.array(
          z.object({
            type: z.enum(["joker", "tarot", "planet", "spectral", "pack", "voucher"]),
            name: z.string(),
            edition: z.string().optional(),
          })
        ),
        rerollCost: z.number().optional(),
      }),
      description: "Shop queue for a specific ante showing available items",
    },
    BossBlind: {
      props: z.object({
        name: z.string(),
        ante: z.number().min(1).max(8),
        description: z.string().optional(),
        debuff: z.string().optional(),
      }),
      description: "Boss blind card for an ante with debuff description",
    },
    AnteRoute: {
      props: z.object({
        ante: z.number().min(1).max(8),
        boss: z.string(),
        shopItems: z.number().default(2),
        packTypes: z.array(z.string()).optional(),
        tags: z.array(z.string()).optional(),
      }),
      description: "Summary of a single ante in the route",
    },
    FullRoute: {
      props: z.object({
        seed: z.string(),
        antes: z.array(
          z.object({
            ante: z.number(),
            boss: z.string(),
            shopCount: z.number().optional(),
            packCount: z.number().optional(),
          })
        ),
      }),
      description: "Full 8-ante route summary for a seed",
    },

    // ── Erratic Deck ──
    ErraticDeck: {
      props: z.object({
        seed: z.string(),
        cards: z.array(
          z.object({
            rank: z.string(),
            suit: z.string(),
          })
        ),
        suits: z.record(z.string(), z.number()).optional(),
        ranks: z.record(z.string(), z.number()).optional(),
        erraticScore: z.number().optional(),
      }),
      description: "Erratic deck composition with suit/rank distribution stats",
    },
    ErraticComparison: {
      props: z.object({
        seeds: z.array(
          z.object({
            seed: z.string(),
            erraticScore: z.number(),
            dominantSuit: z.string().optional(),
            dominantRank: z.string().optional(),
          })
        ),
      }),
      description: "Side-by-side comparison of erratic deck seeds",
    },

    // ── JAML & Filter ──
    JamlFilter: {
      props: z.object({
        jaml: z.string(),
        description: z.string().optional(),
        isValid: z.boolean().optional(),
      }),
      description: "A JAML filter display with syntax highlighting",
    },
    FilterSuggestion: {
      props: z.object({
        suggestion: z.string(),
        reason: z.string().optional(),
        jaml: z.string().optional(),
      }),
      description: "AI-generated JAML filter suggestion with explanation",
    },

    // ── Chat & Input ──
    ChatMessage: {
      props: z.object({
        role: z.enum(["user", "assistant", "system"]),
        content: z.string(),
        timestamp: z.string().optional(),
      }),
      description: "Chat message bubble from user or assistant",
    },
    ChatInput: {
      props: z.object({
        placeholder: z.string().optional(),
        disabled: z.boolean().optional(),
      }),
      description: "Chat input field with send button",
    },
    ActionButton: {
      props: z.object({
        label: z.string(),
        variant: z.enum(["primary", "secondary", "danger", "ghost"]).default("primary"),
        icon: z.string().optional(),
      }),
      description: "A button that triggers an action when clicked",
    },

    // ── Typography ──
    Heading: {
      props: z.object({
        text: z.string(),
        level: z.number().min(1).max(4).default(2),
        color: z.enum(["default", "accent", "muted"]).default("default"),
      }),
      description: "Section heading with configurable level and color",
    },
    Text: {
      props: z.object({
        body: z.string(),
        variant: z.enum(["default", "muted", "accent", "code"]).default("default"),
      }),
      description: "Paragraph text with styling variants",
    },
    Badge: {
      props: z.object({
        label: z.string(),
        variant: z.enum(["default", "success", "warning", "error", "info"]).default("default"),
      }),
      description: "Small badge tag for statuses, categories, etc.",
    },
  },
  actions: {
    searchSeeds: {
      description: "Run a JAML filter search against the seed database",
      params: z.object({
        jaml: z.string(),
        seedCount: z.number().optional(),
        mode: z.enum(["random", "seedlist", "aesthetic"]).optional(),
      }),
    },
    analyzeSeed: {
      description: "Analyze a specific seed to show its full shop queue, jokers, and bosses",
      params: z.object({
        seed: z.string(),
        deck: z.string().optional(),
        stake: z.string().optional(),
      }),
    },
    analyzeErratic: {
      description: "Analyze a seed's erratic deck composition",
      params: z.object({
        seed: z.string(),
      }),
    },
    compareErraticSeeds: {
      description: "Compare multiple erratic deck seeds side-by-side",
      params: z.object({
        seeds: z.array(z.string()),
      }),
    },
    copySeed: {
      description: "Copy a seed code to the clipboard",
      params: z.object({
        seed: z.string(),
      }),
    },
    rerunSearch: {
      description: "Rerun the last search with the same parameters",
    },
    cancelSearch: {
      description: "Cancel the currently running search",
    },
    showAnte: {
      description: "Show detailed shop queue and boss for a specific ante",
      params: z.object({
        seed: z.string(),
        ante: z.number(),
      }),
    },
    suggestFilter: {
      description: "Ask the AI to suggest a JAML filter based on a description",
      params: z.object({
        description: z.string(),
      }),
    },
    applyFilter: {
      description: "Apply a suggested JAML filter to the search",
      params: z.object({
        jaml: z.string(),
      }),
    },
  },
});

export type BalatroCatalog = typeof balatroCatalog;
