import React from "react";
import { Badge, Panel, Stack, Text, Divider } from "./layout.js";
import {
  getJoker,
  getSynergies,
  type JokerInfo,
  type JokerRarity,
  type JokerCategory,
} from "../knowledge/jokers.js";
import {
  findSynergies,
  type SynergyInfo,
} from "../knowledge/synergies.js";
import {
  getDeck,
  getStake,
  type DeckInfo,
  type StakeInfo,
} from "../knowledge/decks.js";
import {
  getBoss,
  getBossesByCategory,
  type BossInfo,
  type BossCategory,
} from "../knowledge/bosses.js";

/**
 * Reference Components — Encyclopedia UI for Balatro entities.
 *
 * All components query the knowledge base and render rich info cards.
 * Zero external dependencies. Pure React + CSS tokens.
 */

/* ─── JokerCard ─── */
export interface JokerCardProps {
  name: string;
  showSynergies?: boolean;
  className?: string;
}

export const JokerCard: React.FC<JokerCardProps> = ({
  name,
  showSynergies = true,
  className = "",
}) => {
  const joker = getJoker(name);
  if (!joker) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Joker not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const rarityColor: Record<JokerRarity, string> = {
    Common: "var(--j-grey)",
    Uncommon: "var(--j-green)",
    Rare: "var(--j-blue)",
    Legendary: "var(--j-gold)",
  };

  const categoryTone: Record<JokerCategory, string> = {
    Copy: "blue",
    "X-Mult": "purple",
    Flat: "green",
    Economy: "gold",
    Retrigger: "orange",
    Utility: "grey",
  };

  const synergies = showSynergies ? getSynergies(name) : [];

  return (
    <Panel className={className}>
      <Stack gap={8}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: 8,
          }}
        >
          <Text body={joker.name} variant="title" />
          <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
            <Badge
              label={joker.rarity}
              tone={joker.rarity === "Legendary" ? "gold" : joker.rarity === "Rare" ? "blue" : joker.rarity === "Uncommon" ? "green" : "grey"}
            />
            <Badge label={`$${joker.cost}`} tone="gold" />
          </div>
        </div>

        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          <Badge label={joker.category} tone={categoryTone[joker.category] as any} />
          <Badge label={joker.jamlKey} tone="grey" />
        </div>

        <Text body={joker.effect} variant="body" />

        <Divider />

        <Text body="Strategy" variant="accent" />
        <Text body={joker.strategy} variant="muted" />

        {synergies.length > 0 && (
          <>
            <Divider />
            <Text body="Synergies" variant="accent" />
            <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
              {synergies.map((s) => (
                <Badge key={s.name} label={s.name} tone="purple" />
              ))}
            </div>
          </>
        )}
      </Stack>
    </Panel>
  );
};

/* ─── SynergyCard ─── */
export interface SynergyCardProps {
  name: string;
  className?: string;
}

export const SynergyCard: React.FC<SynergyCardProps> = ({
  name,
  className = "",
}) => {
  const synergy = findSynergies(name).find((s) => s.name === name);
  if (!synergy) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Synergy not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const difficultyTone: Record<SynergyInfo["difficulty"], string> = {
    Easy: "green",
    Medium: "orange",
    Hard: "red",
    Legendary: "gold",
  };

  return (
    <Panel className={className} variant="accent">
      <Stack gap={10}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: 8,
          }}
        >
          <Text body={synergy.name} variant="title" />
          <Badge label={synergy.difficulty} tone={difficultyTone[synergy.difficulty] as any} />
        </div>

        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {synergy.jokers.map((j) => (
            <Badge key={j} label={j} tone="blue" />
          ))}
        </div>

        <Text body={synergy.description} variant="body" />

        <Divider />

        <Text body="The Math" variant="accent" />
        <Text body={synergy.math} variant="muted" />

        <Divider />

        <Text body="Setup Steps" variant="accent" />
        <Stack gap={6}>
          {synergy.setup.map((step, i) => (
            <div key={i} style={{ display: "flex", gap: 8, alignItems: "flex-start" }}>
              <span style={{ color: "var(--j-gold)", fontWeight: 700, minWidth: 20 }}>
                {i + 1}.
              </span>
              <Text body={step} variant="muted" />
            </div>
          ))}
        </Stack>

        <Divider />

        <Text body="Boss Counters" variant="accent" />
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {synergy.bossCounters.map((b) => (
            <Badge key={b} label={b} tone="red" />
          ))}
        </div>
      </Stack>
    </Panel>
  );
};

/* ─── BossBlindCard ─── */
export interface BossBlindCardProps {
  name: string;
  className?: string;
}

export const BossBlindCard: React.FC<BossBlindCardProps> = ({
  name,
  className = "",
}) => {
  const boss = getBoss(name);
  if (!boss) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Boss not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const threatTone: Record<BossInfo["threatLevel"], string> = {
    Low: "green",
    Medium: "orange",
    High: "red",
    Lethal: "purple",
  };

  const categoryTone: Record<BossCategory, string> = {
    Debuffer: "red",
    Restrictor: "orange",
    Obfuscator: "blue",
    Scaler: "purple",
    Economic: "gold",
  };

  return (
    <Panel className={className}>
      <Stack gap={8}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: 8,
          }}
        >
          <Text body={boss.name} variant="title" />
          <div style={{ display: "flex", gap: 6 }}>
            <Badge label={boss.category} tone={categoryTone[boss.category] as any} />
            <Badge label={boss.threatLevel} tone={threatTone[boss.threatLevel] as any} />
          </div>
        </div>

        <Badge label={boss.jamlKey} tone="grey" />

        <Text body={boss.effect} variant="body" />

        <Divider />

        <Text body="Counters" variant="accent" />
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {boss.counters.map((c) => (
            <Badge key={c} label={c} tone="green" />
          ))}
        </div>

        <Divider />

        <Text body="JAML Filter" variant="accent" />
        <div
          style={{
            background: "var(--j-surface-inset)",
            padding: "8px 12px",
            borderRadius: "var(--j-radius)",
            fontFamily: "var(--j-font-code)",
            fontSize: "var(--j-text-sm)",
            color: "var(--j-grey)",
          }}
        >
          {boss.jamlAvoid}
        </div>
      </Stack>
    </Panel>
  );
};

/* ─── DeckCard ─── */
export interface DeckCardProps {
  name: string;
  className?: string;
}

export const DeckCard: React.FC<DeckCardProps> = ({
  name,
  className = "",
}) => {
  const deck = getDeck(name);
  if (!deck) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Deck not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const difficultyTone: Record<DeckInfo["difficulty"], string> = {
    Easy: "green",
    Medium: "orange",
    Hard: "red",
  };

  return (
    <Panel className={className}>
      <Stack gap={8}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: 8,
          }}
        >
          <Text body={deck.name} variant="title" />
          <Badge label={deck.difficulty} tone={difficultyTone[deck.difficulty] as any} />
        </div>

        <Badge label={deck.jamlKey} tone="grey" />

        <Text body={deck.effect} variant="body" />

        <Divider />

        <Text body="Strategy" variant="accent" />
        <Text body={deck.strategy} variant="muted" />

        <Divider />

        <Text body="Synergies" variant="accent" />
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {deck.synergies.map((s) => (
            <Badge key={s} label={s} tone="blue" />
          ))}
        </div>
      </Stack>
    </Panel>
  );
};

/* ─── StakeCard ─── */
export interface StakeCardProps {
  name: string;
  className?: string;
}

export const StakeCard: React.FC<StakeCardProps> = ({
  name,
  className = "",
}) => {
  const stake = getStake(name);
  if (!stake) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Stake not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const difficultyTone: Record<StakeInfo["difficulty"], string> = {
    Easy: "green",
    Medium: "orange",
    Hard: "red",
    Expert: "purple",
  };

  return (
    <Panel className={className}>
      <Stack gap={8}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            flexWrap: "wrap",
            gap: 8,
          }}
        >
          <Text body={stake.name} variant="title" />
          <Badge label={stake.difficulty} tone={difficultyTone[stake.difficulty] as any} />
        </div>

        <Badge label={stake.jamlKey} tone="grey" />

        <Text body={stake.effect} variant="body" />

        <Divider />

        <Text body="Strategy" variant="accent" />
        <Text body={stake.strategy} variant="muted" />
      </Stack>
    </Panel>
  );
};

/* ─── StrategyAdvisor ─── */
export interface StrategyAdvisorProps {
  jokers: string[];
  className?: string;
}

export const StrategyAdvisor: React.FC<StrategyAdvisorProps> = ({
  jokers,
  className = "",
}) => {
  const foundJokers = jokers
    .map((name) => getJoker(name))
    .filter((j): j is JokerInfo => j !== undefined);

  const foundSynergies = findSynergies(jokers.join(" "));

  const recommended = foundSynergies.filter((s) =>
    s.jokers.some((j) => jokers.map((n) => n.toLowerCase()).includes(j.toLowerCase()))
  );

  const bossWarnings = new Set<string>();
  recommended.forEach((s) => s.bossCounters.forEach((b) => bossWarnings.add(b)));

  return (
    <Panel className={className} variant="accent">
      <Stack gap={12}>
        <Text body={`Strategy Advisor (${foundJokers.length} jokers)`} variant="title" />

        {foundJokers.length > 0 && (
          <>
            <Text body="Detected Jokers" variant="accent" />
            <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
              {foundJokers.map((j) => (
                <Badge key={j.name} label={j.name} tone="blue" />
              ))}
            </div>
          </>
        )}

        {recommended.length > 0 && (
          <>
            <Divider />
            <Text body="Recommended Strategies" variant="accent" />
            <Stack gap={8}>
              {recommended.slice(0, 3).map((s) => (
                <div key={s.name}>
                  <Text body={s.name} variant="body" />
                  <Text body={s.description} variant="muted" />
                  <div style={{ display: "flex", gap: 4, flexWrap: "wrap", marginTop: 4 }}>
                    {s.jokers.map((j) => (
                      <Badge key={j} label={j} tone="purple" />
                    ))}
                  </div>
                </div>
              ))}
            </Stack>
          </>
        )}

        {bossWarnings.size > 0 && (
          <>
            <Divider />
            <Text body="Boss Blind Warnings" variant="accent" />
            <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
              {Array.from(bossWarnings).map((b) => (
                <Badge key={b} label={b} tone="red" />
              ))}
            </div>
          </>
        )}

        {foundJokers.length === 0 && recommended.length === 0 && (
          <Text body="No recognized jokers or synergies found. Add jokers to get strategy advice." variant="muted" />
        )}
      </Stack>
    </Panel>
  );
};
