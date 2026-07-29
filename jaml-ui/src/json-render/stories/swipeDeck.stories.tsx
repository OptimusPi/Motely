import type { Meta, StoryObj } from "@storybook/react-vite";
import type React from "react";
import { useEffect, useState } from "react";
import {
  MotelyJaml,
  MotelyJamlyzer,
  MotelyBossBlind,
  MotelyVoucher,
  type MotelyJamlyzerSeedResult,
} from "motely-wasm";
import { ensureMotelyReady } from "../../lib/motely/runtime.js";
import { JimboApp } from "../../ui/JimboApp.js";
import { JimboPanel } from "../../ui/JimboPanel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboSwipeDeck } from "../../ui/JimboSwipeDeck.js";
import {
  resolveAnalyzerShopItem,
  JamlGameCard,
  JamlBoss,
  JamlVoucher,
} from "../../components/GameCard.js";
import { BOSSES, VOUCHERS } from "../../sprites/spriteData.js";

/**
 * Live triage: motely-wasm boots IN the browser, analyzes real seeds, and each
 * card is a real MotelyJamlyzerSeedResult — score, boss, voucher and decoded
 * shop-item sprites straight off the engine. Nothing here is frozen JSON; this
 * is the client-side analysis path the MCP app runs, which is exactly why it
 * belongs in a dev server you can drive from a phone.
 */

// Real seeds from the community library (list_seeds). analyzeSeeds reads the
// seeds off the JAML's own `seeds:` field and returns per-ante analysis.
const TRIAGE_JAML = `deck: Anaglyph
stake: White
should:
  - joker: Perkeo
    score: 10
  - joker: Blueprint
    score: 5
  - joker: Brainstorm
    score: 5
seeds:
  - H95HQCVY
  - BQ6MGFG8
  - ILJYQ7NG
  - RQ18NZ7U
  - 7KHAAHL5
  - NAT1GH8W
  - 5WP4U311
  - 3LOVEOOG
  - POAYQFL1
  - LOLAEFGT
`;

function bossName(v: MotelyBossBlind): string {
  const key = MotelyBossBlind[v];
  if (!key) return "Small Blind";
  const norm = key.toLowerCase();
  return BOSSES.find((b) => b.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === norm)?.name ?? key;
}

function voucherName(v: MotelyVoucher): string {
  const key = MotelyVoucher[v];
  if (!key) return "";
  const norm = key.toLowerCase();
  return VOUCHERS.find((x) => x.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === norm)?.name ?? key;
}

function ShopItem({ value, scale = 0.4 }: { value: number; scale?: number }) {
  const resolved = resolveAnalyzerShopItem({ id: String(value), name: "", value }, scale);
  if (resolved.kind === "voucher") return <JamlVoucher voucherName={resolved.voucherName} scale={scale} />;
  if (resolved.kind === "joker" || resolved.kind === "consumable" || resolved.kind === "playing")
    return <JamlGameCard card={resolved.card} type={resolved.type} />;
  return (
    <div
      className="j-game-card j-game-card--unknown"
      style={{ "--j-card-width": `${71 * scale}px` } as React.CSSProperties}
    >
      <JimboText size="micro" tone="grey">?</JimboText>
    </div>
  );
}

/** One real analyzed seed, painted as a triage card. */
function SeedCard({ row }: { row: MotelyJamlyzerSeedResult }) {
  const isMatch = (row.score ?? 0) >= 1;
  const ante1 = row.antes?.find((a) => a.ante === 1);

  return (
    <JimboPanel style={{ width: "100%", height: "100%", boxSizing: "border-box" }}>
      <div style={{ display: "grid", gap: 10, justifyItems: "center" }}>
        <JimboText size="lg" tone="white">{row.seed}</JimboText>
        <JimboText size="sm" tone={isMatch ? "green" : "red"}>
          {isMatch ? `Match · score ${row.score}` : `Miss · score ${row.score}`}
        </JimboText>

        {ante1 ? (
          <>
            <div style={{ display: "grid", gridAutoFlow: "column", gap: 12, justifyContent: "center" }}>
              <div style={{ display: "grid", gap: 4, justifyItems: "center" }}>
                <JamlBoss bossName={bossName(ante1.boss)} scale={0.42} />
                <JimboText size="micro" tone="grey">{bossName(ante1.boss)}</JimboText>
              </div>
              <div style={{ display: "grid", gap: 4, justifyItems: "center" }}>
                <JamlVoucher voucherName={voucherName(ante1.voucher)} scale={0.42} />
                <JimboText size="micro" tone="grey">{voucherName(ante1.voucher)}</JimboText>
              </div>
            </div>

            <JimboText size="micro" tone="gold">Ante 1 shop</JimboText>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(4, auto)",
                gap: 4,
                justifyContent: "center",
              }}
            >
              {(ante1.shopItems ?? []).slice(0, 8).map((item, i) => (
                <ShopItem key={i} value={item.value} />
              ))}
            </div>
          </>
        ) : (
          <JimboText size="xs" tone="grey">No ante-1 analysis</JimboText>
        )}
      </div>
    </JimboPanel>
  );
}

type LoadState =
  | { status: "loading" }
  | { status: "ready"; rows: readonly MotelyJamlyzerSeedResult[]; ms: number }
  | { status: "error"; message: string };

function LiveTriage() {
  const [load, setLoad] = useState<LoadState>({ status: "loading" });

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        await ensureMotelyReady();
        const t0 = performance.now();
        const rows = MotelyJamlyzer.analyzeSeeds(MotelyJaml.fromYaml(TRIAGE_JAML.trim()));
        const ms = performance.now() - t0;
        if (!cancelled) setLoad({ status: "ready", rows, ms });
      } catch (e) {
        if (!cancelled) setLoad({ status: "error", message: e instanceof Error ? e.message : String(e) });
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  if (load.status === "loading")
    return (
      <JimboPanel>
        <JimboText size="sm" tone="white">Booting motely-wasm, analyzing…</JimboText>
      </JimboPanel>
    );

  if (load.status === "error")
    return (
      <JimboPanel>
        <JimboText size="xs" tone="red">{load.message}</JimboText>
      </JimboPanel>
    );

  return (
    <div style={{ display: "grid", gap: 8, justifyItems: "center" }}>
      <JimboText size="micro" tone="grey">
        {load.ms.toFixed(0)} ms · {load.rows.length} seeds analyzed live
      </JimboText>
      <JimboSwipeDeck width={300} height={400}>
        {load.rows.map((row) => (
          <SeedCard key={row.seed} row={row} />
        ))}
      </JimboSwipeDeck>
    </div>
  );
}

const meta: Meta = {
  title: "Wire Format/SwipeDeck",
  parameters: { layout: "centered" },
  decorators: [
    (Story) => (
      <JimboApp>
        <Story />
      </JimboApp>
    ),
  ],
};

export default meta;
type Story = StoryObj;

/**
 * Drag left to pass, right to keep. Arrow keys and backspace-to-undo also work.
 * Every card is a seed run through motely-wasm live in this browser tab.
 */
export const LiveTriage_: Story = {
  name: "Live Triage",
  render: () => <LiveTriage />,
};
