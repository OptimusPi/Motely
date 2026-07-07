"use client";

import React, { useMemo, useState } from "react";
import {
  MotelyJamlyzerSeedResult,
  MotelyJamlyzerAnteResult,
  MotelyBoosterPack,
  MotelyBossBlind,
  MotelyTag,
  MotelyVoucher,
  MotelyDeck,
  MotelyStake,
  MotelyItem,
} from "motely-wasm";
import {
  JamlGameCard,
  JamlVoucher,
  JamlTag,
  JamlBoss,
} from "./GameCard.js";
import { decodeMotelyItemToJamlCard } from "../decode/motelyItemDecoder.js";

export interface JamlyzerViewProps {
  result: MotelyJamlyzerSeedResult;
  deck?: MotelyDeck;
  stake?: MotelyStake;
  maxAnte?: number;
}

function splitCamelCase(key: string): string {
  return key.replace(/([A-Z])/g, " $1").trim();
}

function packDisplayName(pack: MotelyBoosterPack): string {
  return splitCamelCase(MotelyBoosterPack[pack]);
}

function bossDisplayName(boss: MotelyBossBlind): string {
  return splitCamelCase(MotelyBossBlind[boss]);
}

function tagDisplayName(tag: MotelyTag): string {
  return splitCamelCase(MotelyTag[tag]);
}

function voucherDisplayName(voucher: MotelyVoucher): string {
  return splitCamelCase(MotelyVoucher[voucher]);
}

function deckDisplayName(deck: MotelyDeck): string {
  return splitCamelCase(MotelyDeck[deck]);
}

function stakeDisplayName(stake: MotelyStake): string {
  return splitCamelCase(MotelyStake[stake]);
}

function ItemCard({ item }: { item: MotelyItem }) {
  const resolved = useMemo(
    () => decodeMotelyItemToJamlCard(item, 0.85),
    [item]
  );
  if (!resolved) return <div className="j-analyzer-unknown">?</div>;
  return (
    <JamlGameCard
      type={resolved.type}
      card={resolved.card}
      hoverTilt
    />
  );
}

function PackSection({ pack }: { pack: MotelyJamlyzerAnteResult["packs"][number] }) {
  return (
    <div className="j-analyzer-pack">
      <div className="j-analyzer-pack-header">
        <span className="j-analyzer-pack-name">{packDisplayName(pack.pack)}</span>
        <span className="j-analyzer-pack-count">{pack.items.length} cards</span>
      </div>
      <div className="j-analyzer-card-row">
        {pack.items.map((item, i) => (
          <ItemCard key={i} item={item} />
        ))}
      </div>
    </div>
  );
}

export function JamlyzerView({ result, deck, stake, maxAnte = 8 }: JamlyzerViewProps) {
  const [selectedAnte, setSelectedAnte] = useState(1);

  const ante = useMemo(
    () => result.antes.find((a) => a.ante === selectedAnte) ?? result.antes[0],
    [result.antes, selectedAnte]
  );

  const anteNumbers = useMemo(
    () => Array.from({ length: maxAnte }, (_, i) => i + 1),
    [maxAnte]
  );

  if (!ante) return <div className="j-analyzer-empty">No ante data available.</div>;

  return (
    <div className="j-analyzer">
      <style>{`
        .j-analyzer {
          --j-analyzer-bg: #1a1b26;
          --j-analyzer-panel: #24283b;
          --j-analyzer-border: #414868;
          --j-analyzer-text: #c0caf5;
          --j-analyzer-muted: #565f89;
          --j-analyzer-accent: #7aa2f7;
          --j-analyzer-radius: 8px;
          display: grid;
          grid-template-columns: 80px 1fr;
          gap: 12px;
          background: var(--j-analyzer-bg);
          color: var(--j-analyzer-text);
          font-family: system-ui, sans-serif;
          padding: 12px;
          border-radius: var(--j-analyzer-radius);
          min-height: 0;
        }
        @media (max-width: 640px) {
          .j-analyzer {
            grid-template-columns: 1fr;
            grid-template-rows: auto 1fr;
          }
          .j-analyzer-antes {
            flex-direction: row !important;
            overflow-x: auto;
            padding-bottom: 4px;
          }
        }
        .j-analyzer-antes {
          display: flex;
          flex-direction: column;
          gap: 6px;
        }
        .j-analyzer-ante-btn {
          background: var(--j-analyzer-panel);
          border: 1px solid var(--j-analyzer-border);
          color: var(--j-analyzer-text);
          border-radius: var(--j-analyzer-radius);
          padding: 8px 4px;
          cursor: pointer;
          font-size: 13px;
          text-align: center;
        }
        .j-analyzer-ante-btn:hover {
          border-color: var(--j-analyzer-accent);
        }
        .j-analyzer-ante-btn.active {
          background: var(--j-analyzer-accent);
          color: #1a1b26;
          border-color: var(--j-analyzer-accent);
          font-weight: 600;
        }
        .j-analyzer-main {
          display: flex;
          flex-direction: column;
          gap: 12px;
          min-width: 0;
        }
        .j-analyzer-header {
          background: var(--j-analyzer-panel);
          border: 1px solid var(--j-analyzer-border);
          border-radius: var(--j-analyzer-radius);
          padding: 12px;
        }
        .j-analyzer-header h2 {
          margin: 0 0 6px;
          font-size: 16px;
        }
        .j-analyzer-meta {
          display: flex;
          flex-wrap: wrap;
          gap: 12px;
          font-size: 13px;
          color: var(--j-analyzer-muted);
        }
        .j-analyzer-meta span strong {
          color: var(--j-analyzer-text);
        }
        .j-analyzer-section {
          background: var(--j-analyzer-panel);
          border: 1px solid var(--j-analyzer-border);
          border-radius: var(--j-analyzer-radius);
          padding: 12px;
        }
        .j-analyzer-section-title {
          margin: 0 0 10px;
          font-size: 13px;
          text-transform: uppercase;
          letter-spacing: 0.05em;
          color: var(--j-analyzer-accent);
        }
        .j-analyzer-blinds {
          display: grid;
          grid-template-columns: repeat(3, 1fr);
          gap: 12px;
        }
        @media (max-width: 640px) {
          .j-analyzer-blinds {
            grid-template-columns: 1fr;
          }
        }
        .j-analyzer-blind {
          display: flex;
          align-items: center;
          gap: 10px;
          background: rgba(26, 27, 38, 0.5);
          border-radius: var(--j-analyzer-radius);
          padding: 8px;
        }
        .j-analyzer-blind-label {
          font-size: 12px;
          color: var(--j-analyzer-muted);
          min-width: 70px;
        }
        .j-analyzer-card-row {
          display: flex;
          flex-wrap: wrap;
          gap: 8px;
        }
        .j-analyzer-pack {
          margin-bottom: 12px;
        }
        .j-analyzer-pack:last-child {
          margin-bottom: 0;
        }
        .j-analyzer-pack-header {
          display: flex;
          align-items: center;
          gap: 10px;
          margin-bottom: 8px;
        }
        .j-analyzer-pack-name {
          font-size: 13px;
          font-weight: 600;
        }
        .j-analyzer-pack-count {
          font-size: 11px;
          background: var(--j-analyzer-accent);
          color: #1a1b26;
          padding: 2px 6px;
          border-radius: 999px;
        }
        .j-analyzer-unknown {
          width: 60px;
          height: 80px;
          display: grid;
          place-items: center;
          background: var(--j-analyzer-bg);
          border: 1px solid var(--j-analyzer-border);
          border-radius: var(--j-analyzer-radius);
          color: var(--j-analyzer-muted);
          font-size: 12px;
        }
        .j-analyzer-empty {
          padding: 24px;
          color: var(--j-analyzer-muted);
        }
      `}</style>

      <div className="j-analyzer-antes">
        {anteNumbers.map((n) => (
          <button
            key={n}
            className={`j-analyzer-ante-btn ${n === selectedAnte ? "active" : ""}`}
            onClick={() => setSelectedAnte(n)}
          >
            Ante {n}
          </button>
        ))}
      </div>

      <div className="j-analyzer-main">
        <div className="j-analyzer-header">
          <h2>Seed: {result.seed}</h2>
          <div className="j-analyzer-meta">
            <span>
              Score: <strong>{result.score}</strong>
            </span>
            {deck !== undefined && (
              <span>
                Deck: <strong>{deckDisplayName(deck)}</strong>
              </span>
            )}
            {stake !== undefined && (
              <span>
                Stake: <strong>{stakeDisplayName(stake)}</strong>
              </span>
            )}
          </div>
        </div>

        <div className="j-analyzer-section">
          <h3 className="j-analyzer-section-title">Blinds</h3>
          <div className="j-analyzer-blinds">
            <div className="j-analyzer-blind">
              <span className="j-analyzer-blind-label">Small Blind</span>
              <JamlTag tagName={tagDisplayName(ante.smallBlindTag)} scale={0.75} />
            </div>
            <div className="j-analyzer-blind">
              <span className="j-analyzer-blind-label">Big Blind</span>
              <JamlTag tagName={tagDisplayName(ante.bigBlindTag)} scale={0.75} />
            </div>
            <div className="j-analyzer-blind">
              <span className="j-analyzer-blind-label">Boss Blind</span>
              <JamlBoss bossName={bossDisplayName(ante.boss)} scale={0.75} />
            </div>
          </div>
        </div>

        <div className="j-analyzer-section">
          <h3 className="j-analyzer-section-title">Voucher</h3>
          <div className="j-analyzer-card-row">
            <JamlVoucher voucherName={voucherDisplayName(ante.voucher)} scale={0.9} />
          </div>
        </div>

        <div className="j-analyzer-section">
          <h3 className="j-analyzer-section-title">Shop</h3>
          <div className="j-analyzer-card-row">
            {ante.shopItems.map((item, i) => (
              <ItemCard key={i} item={item} />
            ))}
          </div>
        </div>

        {ante.packs.length > 0 && (
          <div className="j-analyzer-section">
            <h3 className="j-analyzer-section-title">Packs</h3>
            {ante.packs.map((pack, i) => (
              <PackSection key={i} pack={pack} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
