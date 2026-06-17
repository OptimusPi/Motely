"use client";

import Link from "next/link";
import { JimboApp, JimboBackground } from "jaml-ui/ui";

export default function HomePage() {
  return (
    <>
      <JimboBackground />
      <JimboApp>
        <main className="mx-auto flex w-full max-w-4xl flex-1 flex-col gap-12 px-4 py-12 md:py-16">
          {/* Hero */}
          <section className="flex flex-col items-center gap-4 text-center">
            <h1
              className="font-pixel text-balance text-4xl md:text-6xl"
              style={{ color: "var(--j-accent)" }}
            >
              Balatro Seed Lab
            </h1>
            <p className="max-w-xl text-balance text-sm leading-relaxed" style={{ color: "var(--j-muted)" }}>
              Search 2.3 trillion Balatro seeds with JAML filters. Analyze full routes.
              Discover erratic deck compositions. All in your browser.
            </p>
          </section>

          {/* Nav Cards */}
          <section className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <NavCard
              href="/find"
              title="Find Seeds"
              description="Write a JAML filter or describe what you want in plain English. The Motely engine grinds seeds on your CPU."
              icon="🔍"
              accent
            />
            <NavCard
              href="/analyze"
              title="Analyze Seed"
              description="Paste any seed and see the full route — shop queues, jokers, bosses, tags, and packs for every ante."
              icon="🔬"
            />
            <NavCard
              href="/erratic"
              title="Erratic Decks"
              description="Analyze erratic deck compositions. Find the least erratic seeds. Compare side-by-side."
              icon="🎲"
            />
          </section>

          {/* Stats / Features */}
          <section className="rounded-lg border p-6" style={{ borderColor: "var(--j-border)" }}>
            <h2 className="mb-4 font-pixel text-xl" style={{ color: "var(--j-foreground)" }}>
              What makes this different
            </h2>
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <Feature
                title="AI-Generated UI"
                description="Results render as structured UI components (cards, tables, charts) via json-render — not just text dumps."
              />
              <Feature
                title="2.3 Trillion Seeds"
                description="The MotelyJAML engine runs SIMD-vectorized, multi-threaded searches directly in your browser via WebAssembly."
              />
              <Feature
                title="Full Route Analysis"
                description="See every shop slot, booster pack, joker, voucher, tag, and boss blind for all 8 antes + endless."
              />
              <Feature
                title="Erratic Deck Tools"
                description="Specialized erratic deck analysis: suit/rank distribution, comparison tables, and 'least erratic' scoring."
              />
            </div>
          </section>

          {/* Footer */}
          <footer className="flex flex-col items-center gap-2 border-t pt-6" style={{ borderColor: "var(--j-border)" }}>
            <p className="text-center text-xs" style={{ color: "var(--j-muted)" }}>
              Powered by{" "}
              <a href="https://github.com/OptimusPi/MotelyJAML" target="_blank" rel="noreferrer" className="underline" style={{ color: "var(--j-accent)" }}>
                MotelyJAML
              </a>
              {" — "}a vectorized SIMD seed engine forked from{" "}
              <a href="https://github.com/Tacodiva/Motely" target="_blank" rel="noreferrer" className="underline" style={{ color: "var(--j-accent)" }}>
                Tacodiva&apos;s Motely
              </a>
            </p>
            <p className="text-center text-xs" style={{ color: "var(--j-muted)" }}>
              Not affiliated with LocalThunk or PlayStack. Made with{" "}
              <span style={{ color: "var(--j-accent)" }}>♥</span> for the Balatro community.
            </p>
          </footer>
        </main>
      </JimboApp>
    </>
  );
}

function NavCard({
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
      className="group flex flex-col gap-3 rounded-lg border p-5 transition-colors hover:border-[var(--j-accent)]"
      style={{
        borderColor: "var(--j-border)",
        backgroundColor: accent ? "var(--j-accent-muted)" : "var(--j-surface)",
      }}
    >
      <div className="text-2xl">{icon}</div>
      <div className="font-bold" style={{ color: accent ? "var(--j-accent)" : "var(--j-foreground)" }}>
        {title}
      </div>
      <p className="text-sm leading-relaxed" style={{ color: "var(--j-muted)" }}>
        {description}
      </p>
      <div className="mt-auto text-sm font-semibold" style={{ color: "var(--j-accent)" }}>
        Go →
      </div>
    </Link>
  );
}

function Feature({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col gap-1">
      <div className="font-semibold text-sm" style={{ color: "var(--j-foreground)" }}>
        {title}
      </div>
      <p className="text-sm leading-relaxed" style={{ color: "var(--j-muted)" }}>
        {description}
      </p>
    </div>
  );
}
