"use client";

import { useState, useCallback } from "react";
import { JimboApp, JimboBackground } from "jaml-ui/ui";
import {
  Renderer,
  StateProvider,
  ActionProvider,
  VisibilityProvider,
  ValidationProvider,
} from "@json-render/react";
import { registry } from "@/lib/registry";
import { buildErraticSpec, buildLoadingSpec } from "@/lib/spec-builder";
import type { SpecType } from "@json-render/react";

// Erratic deck has 52 cards with random rank/suit combinations
// We simulate analysis for demo purposes
function analyzeErraticDeck(seed: string): {
  cards: Array<{ rank: string; suit: string }>;
  suits: Record<string, number>;
  ranks: Record<string, number>;
  erraticScore: number;
} {
  // Deterministic pseudo-random from seed string for demo
  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = ((hash << 5) - hash + seed.charCodeAt(i)) | 0;
  }
  const rng = () => {
    hash = ((hash * 16807) % 2147483647) | 0;
    return (hash & 0x7fffffff) / 2147483647;
  };

  const RANKS = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"];
  const SUITS = ["Spades", "Hearts", "Diamonds", "Clubs"];

  const cards: Array<{ rank: string; suit: string }> = [];
  const suits: Record<string, number> = {};
  const ranks: Record<string, number> = {};

  for (let i = 0; i < 52; i++) {
    const rank = RANKS[Math.floor(rng() * RANKS.length)];
    const suit = SUITS[Math.floor(rng() * SUITS.length)];
    cards.push({ rank, suit });
    suits[suit] = (suits[suit] || 0) + 1;
    ranks[rank] = (ranks[rank] || 0) + 1;
  }

  // Erratic score: lower = more uniform (less erratic)
  // Higher max concentration = more erratic (interesting)
  const maxSuit = Math.max(...Object.values(suits));
  const maxRank = Math.max(...Object.values(ranks));
  const erraticScore = Math.round((maxSuit / 52 + maxRank / 52) * 100);

  return { cards, suits, ranks, erraticScore };
}

export default function ErraticPage() {
  const [seed, setSeed] = useState("");
  const [compareSeeds, setCompareSeeds] = useState("");
  const [result, setResult] = useState<ReturnType<typeof analyzeErraticDeck> | null>(null);
  const [compareResults, setCompareResults] = useState<Array<ReturnType<typeof analyzeErraticDeck> & { seed: string }>>([]);

  const handleAnalyze = useCallback(() => {
    if (!seed.trim()) return;
    setResult(analyzeErraticDeck(seed.trim()));
  }, [seed]);

  const handleCompare = useCallback(() => {
    const seeds = compareSeeds.split(/\s+/).filter(Boolean).slice(0, 10);
    if (seeds.length === 0) return;
    const results = seeds.map((s) => ({ ...analyzeErraticDeck(s), seed: s }));
    setCompareResults(results);
  }, [compareSeeds]);

  const spec: SpecType = (() => {
    if (!result) {
      return buildLoadingSpec("Enter a seed and click Analyze to see the erratic deck composition.");
    }
    return buildErraticSpec({
      seed: seed.trim(),
      cards: result.cards,
      suits: result.suits,
      ranks: result.ranks,
      erraticScore: result.erraticScore,
    });
  })();

  return (
    <>
      <JimboBackground />
      <JimboApp>
        <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-8 md:py-12">
          <header className="flex flex-col gap-2">
            <h1 className="font-pixel text-3xl" style={{ color: "var(--j-accent)" }}>
              Erratic Deck Lab
            </h1>
            <p className="text-sm" style={{ color: "var(--j-muted)" }}>
              Analyze erratic deck compositions. Find the least erratic seeds. Compare side-by-side.
            </p>
          </header>

          {/* Single Seed Analysis */}
          <section className="rounded-lg border p-5" style={{ borderColor: "var(--j-border)" }}>
            <h2 className="mb-4 font-bold" style={{ color: "var(--j-foreground)" }}>
              Single Seed Analysis
            </h2>
            <div className="flex flex-wrap gap-3">
              <input
                type="text"
                className="flex-1 min-w-[200px] rounded border px-3 py-2 font-mono text-sm uppercase"
                style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)", color: "var(--j-foreground)" }}
                placeholder="XEQH7CP9"
                value={seed}
                onChange={(e) => setSeed(e.target.value.toUpperCase())}
                onKeyDown={(e) => e.key === "Enter" && handleAnalyze()}
              />
              <button
                className="rounded px-5 py-2 text-sm font-semibold"
                style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
                onClick={handleAnalyze}
                disabled={!seed.trim()}
              >
                Analyze
              </button>
            </div>

            {result && (
              <div className="mt-4">
                <div className="mb-2 text-xs" style={{ color: "var(--j-muted)" }}>
                  Erratic Score: {result.erraticScore}/100 (lower = more uniform)
                </div>
                <div className="grid grid-cols-4 gap-2 text-center text-xs mb-3">
                  {Object.entries(result.suits).map(([suit, count]) => (
                    <div key={suit} className="rounded border p-2" style={{ borderColor: "var(--j-border)" }}>
                      <div className="font-semibold" style={{ color: "var(--j-foreground)" }}>{suit}</div>
                      <div className="font-mono" style={{ color: "var(--j-accent)" }}>{count}</div>
                    </div>
                  ))}
                </div>
                <div className="grid grid-cols-7 gap-1 text-center text-xs">
                  {Object.entries(result.ranks).map(([rank, count]) => (
                    <div key={rank} className="rounded border p-1" style={{ borderColor: "var(--j-border)" }}>
                      <div style={{ color: "var(--j-foreground)" }}>{rank}</div>
                      <div className="font-mono" style={{ color: "var(--j-accent)" }}>{count}</div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>

          {/* Comparison */}
          <section className="rounded-lg border p-5" style={{ borderColor: "var(--j-border)" }}>
            <h2 className="mb-4 font-bold" style={{ color: "var(--j-foreground)" }}>
              Compare Seeds
            </h2>
            <div className="flex flex-wrap gap-3">
              <input
                type="text"
                className="flex-1 min-w-[200px] rounded border px-3 py-2 font-mono text-sm"
                style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)", color: "var(--j-foreground)" }}
                placeholder="SEED1 SEED2 SEED3 ..."
                value={compareSeeds}
                onChange={(e) => setCompareSeeds(e.target.value.toUpperCase())}
              />
              <button
                className="rounded px-5 py-2 text-sm font-semibold"
                style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
                onClick={handleCompare}
                disabled={!compareSeeds.trim()}
              >
                Compare
              </button>
            </div>

            {compareResults.length > 0 && (
              <div className="mt-4 overflow-x-auto">
                <table className="w-full text-sm" style={{ borderCollapse: "collapse" }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid var(--j-border)" }}>
                      <th className="px-3 py-2 text-left text-xs" style={{ color: "var(--j-muted)" }}>Seed</th>
                      <th className="px-3 py-2 text-right text-xs" style={{ color: "var(--j-muted)" }}>Erratic</th>
                      <th className="px-3 py-2 text-left text-xs" style={{ color: "var(--j-muted)" }}>Top Suit</th>
                      <th className="px-3 py-2 text-right text-xs" style={{ color: "var(--j-muted)" }}>Top Suit %</th>
                      <th className="px-3 py-2 text-left text-xs" style={{ color: "var(--j-muted)" }}>Top Rank</th>
                      <th className="px-3 py-2 text-right text-xs" style={{ color: "var(--j-muted)" }}>Top Rank %</th>
                    </tr>
                  </thead>
                  <tbody>
                    {compareResults.map((r) => {
                      const topSuit = Object.entries(r.suits).sort((a, b) => b[1] - a[1])[0];
                      const topRank = Object.entries(r.ranks).sort((a, b) => b[1] - a[1])[0];
                      return (
                        <tr key={r.seed} style={{ borderBottom: "1px solid var(--j-border)" }}>
                          <td className="px-3 py-2 font-mono font-semibold" style={{ color: "var(--j-accent)" }}>
                            {r.seed}
                          </td>
                          <td className="px-3 py-2 text-right font-mono font-semibold" style={{ color: "var(--j-foreground)" }}>
                            {r.erraticScore}
                          </td>
                          <td className="px-3 py-2 text-xs" style={{ color: "var(--j-muted)" }}>
                            {topSuit[0]}
                          </td>
                          <td className="px-3 py-2 text-right text-xs font-mono" style={{ color: "var(--j-accent)" }}>
                            {Math.round((topSuit[1] / 52) * 100)}%
                          </td>
                          <td className="px-3 py-2 text-xs" style={{ color: "var(--j-muted)" }}>
                            {topRank[0]}
                          </td>
                          <td className="px-3 py-2 text-right text-xs font-mono" style={{ color: "var(--j-accent)" }}>
                            {Math.round((topRank[1] / 52) * 100)}%
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* json-render Erratic View */}
          <section>
            <div className="mb-3 flex items-center gap-2">
              <span className="font-bold text-sm" style={{ color: "var(--j-foreground)" }}>
                AI-Rendered Deck
              </span>
              <span className="text-xs" style={{ color: "var(--j-muted)" }}>
                via json-render
              </span>
            </div>
            <div
              className="rounded-lg border p-4"
              style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
            >
              <StateProvider initialState={{}}>
                <VisibilityProvider>
                  <ActionProvider
                    handlers={{
                      copySeed: async (args) => {
                        if (args.seed) await navigator.clipboard.writeText(args.seed);
                      },
                    }}
                  >
                    <ValidationProvider>
                      <Renderer spec={spec} registry={registry} />
                    </ValidationProvider>
                  </ActionProvider>
                </VisibilityProvider>
              </StateProvider>
            </div>
          </section>
        </main>
      </JimboApp>
    </>
  );
}
