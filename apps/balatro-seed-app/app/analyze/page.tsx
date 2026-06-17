"use client";

import { useState, useCallback } from "react";
import { useAnalyzer } from "jaml-ui";
import { JimboApp, JimboBackground } from "jaml-ui/ui";
import {
  Renderer,
  StateProvider,
  ActionProvider,
  VisibilityProvider,
  ValidationProvider,
} from "@json-render/react";
import { registry } from "@/lib/registry";
import { buildAnalyzeSpec, buildLoadingSpec } from "@/lib/spec-builder";
import type { SpecType } from "@json-render/react";

export default function AnalyzePage() {
  const [seed, setSeed] = useState("");
  const [deck, setDeck] = useState("Red");
  const [stake, setStake] = useState("White");
  const analyzer = useAnalyzer();

  const handleAnalyze = useCallback(() => {
    if (!seed.trim()) return;
    analyzer.analyze(seed, deck, stake);
  }, [analyzer, seed, deck, stake]);

  // Build spec from analyzer result
  const spec: SpecType = (() => {
    if (!analyzer.result) {
      return buildLoadingSpec("Enter a seed and click Analyze to see the full route.");
    }
    // Parse the analyzer result to build antes array
    // For now, create a simplified representation
    const antes = Array.from({ length: 8 }, (_, i) => ({
      ante: i + 1,
      boss: "Unknown", // Would come from real analyzer result
      shopCount: 2,
      packCount: 1,
    }));
    return buildAnalyzeSpec({
      seed: seed.trim(),
      deck,
      stake,
      antes,
    });
  })();

  return (
    <>
      <JimboBackground />
      <JimboApp>
        <main className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-8 px-4 py-8 md:py-12">
          <header className="flex flex-col gap-2">
            <h1 className="font-pixel text-3xl" style={{ color: "var(--j-accent)" }}>
              Analyze Seed
            </h1>
            <p className="text-sm" style={{ color: "var(--j-muted)" }}>
              Paste any seed to see the full route — shop queues, jokers, bosses, tags, and packs.
            </p>
          </header>

          {/* Controls */}
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex-1 min-w-[200px]">
              <label className="mb-1 block text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
                Seed
              </label>
              <input
                type="text"
                className="w-full rounded border px-3 py-2 font-mono text-sm uppercase"
                style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)", color: "var(--j-foreground)" }}
                placeholder="XEQH7CP9"
                value={seed}
                onChange={(e) => setSeed(e.target.value.toUpperCase())}
                onKeyDown={(e) => e.key === "Enter" && handleAnalyze()}
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
                Deck
              </label>
              <select
                className="rounded border px-3 py-2 text-sm"
                style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)", color: "var(--j-foreground)" }}
                value={deck}
                onChange={(e) => setDeck(e.target.value)}
              >
                <option>Red</option>
                <option>Blue</option>
                <option>Black</option>
                <option>Magic</option>
                <option>Nebula</option>
                <option>Ghost</option>
                <option>Abandoned</option>
                <option>Checkered</option>
                <option>Zodiac</option>
                <option>Painted</option>
                <option>Anaglyph</option>
                <option>Plasma</option>
                <option>Erratic</option>
              </select>
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
                Stake
              </label>
              <select
                className="rounded border px-3 py-2 text-sm"
                style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)", color: "var(--j-foreground)" }}
                value={stake}
                onChange={(e) => setStake(e.target.value)}
              >
                <option>White</option>
                <option>Red</option>
                <option>Green</option>
                <option>Black</option>
                <option>Blue</option>
                <option>Purple</option>
                <option>Orange</option>
                <option>Gold</option>
              </select>
            </div>
            <button
              className="rounded px-5 py-2 text-sm font-semibold"
              style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
              onClick={handleAnalyze}
              disabled={analyzer.status === "running" || !seed.trim()}
            >
              {analyzer.status === "running" ? "Analyzing…" : "Analyze"}
            </button>
          </div>

          {/* Results via json-render */}
          <section>
            <div className="mb-3 flex items-center gap-2">
              <span className="font-bold text-sm" style={{ color: "var(--j-foreground)" }}>
                Route Analysis
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
                      showAnte: async (args) => {
                        console.log("Show ante", args);
                      },
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

          {/* Raw result (fallback) */}
          {analyzer.result && (
            <section>
              <div className="mb-2 text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
                Raw Result
              </div>
              <pre
                className="rounded-lg border p-4 text-xs overflow-auto max-h-96"
                style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface-muted)", color: "var(--j-foreground)" }}
              >
                {JSON.stringify(analyzer.result, null, 2)}
              </pre>
            </section>
          )}
        </main>
      </JimboApp>
    </>
  );
}
