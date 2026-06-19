"use client";

import Link from "next/link";
import { JimboBackground } from "jaml-ui/ui";
import { JimboBalatroFooter } from "jaml-ui/ui";

/**
 * Home Page + Filters Browser — App 2
 *
 * The landing page and community hub:
 * - Hero with animated Jimbo background
 * - Featured Filters gallery (community JAML filters)
 * - Recent Seeds (last viewed / analyzed)
 * - Stats Dashboard (global community stats)
 * - Quick links to all 4 apps
 */

const FEATURED_FILTERS = [
  {
    name: "Blueprint + DNA Run",
    jaml: "must:\n  - joker: Blueprint\n  - joker: DNA\n    antes: [1,2,3]",
    author: "pifreak",
    likes: 42,
    tags: ["blueprint", "dna", "op"],
  },
  {
    name: "The Soul First Shop",
    jaml: "must:\n  - joker: The Soul\n    antes: [1]\n    shop: [1,2]",
    author: "seed_hunter",
    likes: 128,
    tags: ["the-soul", "ante-1", "legendary"],
  },
  {
    name: "Perishable Negative",
    jaml: "must:\n  - joker: Any\n    edition: Negative\n    perishable: true",
    author: "deck_builder",
    likes: 23,
    tags: ["negative", "perishable", "meme"],
  },
  {
    name: "Erratic Flush Dream",
    jaml: "must:\n  - deck: Erratic\n  - flush: 5\n    suit: Hearts",
    author: "erratic_fan",
    likes: 67,
    tags: ["erratic", "flush", "hearts"],
  },
];

const COMMUNITY_STATS = {
  seedsSearched: "1.2 billion",
  matchesFound: 3849201,
  activeUsers: 1847,
  filtersShared: 342,
};

export function JamlSeedLabHomePage() {
  return (
    <>
      <JimboBackground />
      
        <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-10 px-4 py-8 md:py-12">
          {/* Hero */}
          <section className="flex flex-col items-center gap-4 text-center">
            <h1
              className="font-pixel text-balance text-4xl md:text-6xl"
              style={{ color: "var(--j-blue)" }}
            >
              JAML Seed Lab
            </h1>
            <p className="max-w-xl text-balance text-sm leading-relaxed" style={{ color: "var(--j-grey)" }}>
              Search 2.3 trillion Balatro seeds with JAML filters. Analyze full routes.
              Discover erratic deck compositions. Powered by motely-wasm + json-render.
            </p>
          </section>

          {/* App Nav Cards */}
          <section className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-4">
            <AppCard
              href="/ide"
              title="JAML IDE"
              description="Write, preview, and validate JAML filters with LSP diagnostics."
              icon="📝"
              accent
            />
            <AppCard
              href="/finder"
              title="Seed Finder"
              description="Load a JAML filter and search 2.3 trillion seeds on your CPU."
              icon="🔍"
            />
            <AppCard
              href="/analyzer"
              title="JAMLYZER"
              description="Deep analyze any seed. Full 8-ante route, jokers, bosses, shops."
              icon="🔬"
            />
            <AppCard
              href="/erratic"
              title="Erratic Lab"
              description="Specialized erratic deck tools. Compare compositions."
              icon="🎲"
            />
          </section>

          {/* Stats Dashboard */}
          <section className="grid grid-cols-2 gap-4 md:grid-cols-4">
            <StatCard label="Seeds Searched" value={COMMUNITY_STATS.seedsSearched} />
            <StatCard label="Matches Found" value={COMMUNITY_STATS.matchesFound.toLocaleString()} />
            <StatCard label="Active Users" value={COMMUNITY_STATS.activeUsers.toLocaleString()} />
            <StatCard label="Filters Shared" value={COMMUNITY_STATS.filtersShared.toLocaleString()} />
          </section>

          {/* Featured Filters */}
          <section>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="font-bold text-lg" style={{ color: "var(--j-white)" }}>
                Featured Filters
              </h2>
              <Link
                href="/ide"
                className="text-sm font-semibold"
                style={{ color: "var(--j-blue)" }}
              >
                Browse all →
              </Link>
            </div>
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              {FEATURED_FILTERS.map((filter) => (
                <div
                  key={filter.name}
                  className="flex flex-col gap-3 rounded-lg border p-4"
                  style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)" }}
                >
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-sm" style={{ color: "var(--j-white)" }}>
                      {filter.name}
                    </span>
                    <span className="text-xs" style={{ color: "var(--j-grey)" }}>
                      ❤ {filter.likes}
                    </span>
                  </div>
                  <pre
                    className="rounded border p-2 font-mono text-xs"
                    style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-surface-inset)", color: "var(--j-white)" }}
                  >
                    {filter.jaml}
                  </pre>
                  <div className="flex flex-wrap gap-1">
                    {filter.tags.map((tag) => (
                      <span
                        key={tag}
                        className="rounded px-1.5 py-0.5 text-xs"
                        style={{ backgroundColor: "var(--j-dark-blue)", color: "var(--j-blue)" }}
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-xs" style={{ color: "var(--j-grey)" }}>
                      by {filter.author}
                    </span>
                    <Link
                      href={`/finder?jaml=${encodeURIComponent(filter.jaml)}`}
                      className="rounded px-2 py-1 text-xs font-semibold"
                      style={{ backgroundColor: "var(--j-blue)", color: "#000" }}
                    >
                      Try it →
                    </Link>
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Footer */}
          <footer className="flex flex-col items-center gap-2 border-t pt-6" style={{ borderColor: "var(--j-panel-edge)" }}>
            <p className="text-center text-xs" style={{ color: "var(--j-grey)" }}>
              Powered by{" "}
              <a href="https://github.com/OptimusPi/MotelyJAML" target="_blank" rel="noreferrer" className="underline" style={{ color: "var(--j-blue)" }}>
                MotelyJAML
              </a>
              {" — "}a vectorized SIMD seed engine forked from{" "}
              <a href="https://github.com/Tacodiva/Motely" target="_blank" rel="noreferrer" className="underline" style={{ color: "var(--j-blue)" }}>
                Tacodiva&apos;s Motely
              </a>
            </p>
            <p className="text-center text-xs" style={{ color: "var(--j-grey)" }}>
              Not affiliated with LocalThunk or PlayStack. Made with{" "}
              <span style={{ color: "var(--j-blue)" }}>♥</span> for the Balatro community.
            </p>
          </footer>
        </main>
      <JimboBalatroFooter />
    </>
  );
}

function AppCard({
  href,
  title,
  description,
  icon,
  accent = false,
}: {
  href: string;
  title: string;
  description: string;
  icon: string;
  accent?: boolean;
}) {
  return (
    <Link
      href={href}
      className="group flex flex-col gap-3 rounded-lg border p-5 transition-colors hover:border-[var(--j-blue)]"
      style={{
        borderColor: "var(--j-panel-edge)",
        backgroundColor: accent ? "var(--j-dark-blue)" : "var(--j-dark-grey)",
      }}
    >
      <div className="text-2xl">{icon}</div>
      <div className="font-bold text-sm" style={{ color: accent ? "var(--j-blue)" : "var(--j-white)" }}>
        {title}
      </div>
      <p className="text-sm leading-relaxed" style={{ color: "var(--j-grey)" }}>
        {description}
      </p>
      <div className="mt-auto text-sm font-semibold" style={{ color: "var(--j-blue)" }}>
        Open →
      </div>
    </Link>
  );
}

function StatCard({ label, value }: { label: string; value: string }) {
  return (
    <div
      className="flex flex-col gap-1 rounded-lg border p-4 text-center"
      style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)" }}
    >
      <div className="font-mono text-xl font-bold" style={{ color: "var(--j-blue)" }}>
        {value}
      </div>
      <div className="text-xs" style={{ color: "var(--j-grey)" }}>
        {label}
      </div>
    </div>
  );
}
