import { type FC } from "react";
import { Badge, Panel, Stack, Text, Divider, type BadgeTone } from "./layout.js";
import { JimboText } from "../../ui/jimboText.js";
import {
  getJoker,
  getSynergies,
  type JokerInfo,
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
  type BossInfo,
  type BossCategory,
} from "../knowledge/bosses.js";

/**
 * Reference Components — Encyclopedia UI for Balatro entities.
 *
 * Grid layout only (no flex — host iframes reflow flex differently, CLAUDE.md
 * rule #1), real --j-* tokens only. Text is JimboText via the layout adapters.
 */

// Title on the left, badge(s) on the right — the no-flex version of the old
// space-between header row.
const HEADER: React.CSSProperties = {
  display: "grid",
  gridTemplateColumns: "1fr auto",
  alignItems: "center",
  gap: 8,
};
// A content-width row of chips.
const ROW: React.CSSProperties = {
  display: "grid",
  gridAutoFlow: "column",
  gridAutoColumns: "max-content",
  alignItems: "center",
  gap: 6,
};
// Pills that wrap without flex-wrap.
const PILLS: React.CSSProperties = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fit, minmax(64px, max-content))",
  gap: 6,
  justifyContent: "start",
};

/* ─── JokerCard ─── */
export interface JokerCardProps {
  name: string;
  showSynergies?: boolean;
  className?: string;
}

export const JokerCard: FC<JokerCardProps> = ({ name, showSynergies = true, className = "" }) => {
  const joker = getJoker(name);
  if (!joker) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Joker not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const categoryTone: Record<JokerCategory, BadgeTone> = {
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
        <div style={HEADER}>
          <Text body={joker.name} variant="title" />
          <div style={ROW}>
            <Badge
              label={joker.rarity}
              tone={
                joker.rarity === "Legendary"
                  ? "gold"
                  : joker.rarity === "Rare"
                    ? "blue"
                    : joker.rarity === "Uncommon"
                      ? "green"
                      : "grey"
              }
            />
            <Badge label={`$${joker.cost}`} tone="gold" />
          </div>
        </div>

        <div style={PILLS}>
          <Badge label={joker.category} tone={categoryTone[joker.category]} />
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
            <div style={PILLS}>
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

export const SynergyCard: FC<SynergyCardProps> = ({ name, className = "" }) => {
  const synergy = findSynergies(name).find((s) => s.name === name);
  if (!synergy) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Synergy not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const difficultyTone: Record<SynergyInfo["difficulty"], BadgeTone> = {
    Easy: "green",
    Medium: "orange",
    Hard: "red",
    Legendary: "gold",
  };

  return (
    <Panel className={className} variant="accent">
      <Stack gap={10}>
        <div style={HEADER}>
          <Text body={synergy.name} variant="title" />
          <Badge label={synergy.difficulty} tone={difficultyTone[synergy.difficulty]} />
        </div>

        <div style={PILLS}>
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
            <div
              key={i}
              style={{
                display: "grid",
                gridTemplateColumns: "auto 1fr",
                gap: 8,
                alignItems: "start",
              }}
            >
              <JimboText size="sm" tone="gold">
                {i + 1}.
              </JimboText>
              <Text body={step} variant="muted" />
            </div>
          ))}
        </Stack>

        <Divider />

        <Text body="Boss Counters" variant="accent" />
        <div style={PILLS}>
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

export const BossBlindCard: FC<BossBlindCardProps> = ({ name, className = "" }) => {
  const boss = getBoss(name);
  if (!boss) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Boss not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const threatTone: Record<BossInfo["threatLevel"], BadgeTone> = {
    Low: "green",
    Medium: "orange",
    High: "red",
    Lethal: "purple",
  };

  const categoryTone: Record<BossCategory, BadgeTone> = {
    Debuffer: "red",
    Restrictor: "orange",
    Obfuscator: "blue",
    Scaler: "purple",
    Economic: "gold",
  };

  return (
    <Panel className={className}>
      <Stack gap={8}>
        <div style={HEADER}>
          <Text body={boss.name} variant="title" />
          <div style={ROW}>
            <Badge label={boss.category} tone={categoryTone[boss.category]} />
            <Badge label={boss.threatLevel} tone={threatTone[boss.threatLevel]} />
          </div>
        </div>

        <Badge label={boss.jamlKey} tone="grey" />

        <Text body={boss.effect} variant="body" />

        <Divider />

        <Text body="Counters" variant="accent" />
        <div style={PILLS}>
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
            borderRadius: "var(--j-radius-lg)",
            fontFamily: "var(--j-font-code)",
            fontSize: 12,
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

export const DeckCard: FC<DeckCardProps> = ({ name, className = "" }) => {
  const deck = getDeck(name);
  if (!deck) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Deck not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const difficultyTone: Record<DeckInfo["difficulty"], BadgeTone> = {
    Easy: "green",
    Medium: "orange",
    Hard: "red",
  };

  return (
    <Panel className={className}>
      <Stack gap={8}>
        <div style={HEADER}>
          <Text body={deck.name} variant="title" />
          <Badge label={deck.difficulty} tone={difficultyTone[deck.difficulty]} />
        </div>

        <Badge label={deck.jamlKey} tone="grey" />

        <Text body={deck.effect} variant="body" />

        <Divider />

        <Text body="Strategy" variant="accent" />
        <Text body={deck.strategy} variant="muted" />

        <Divider />

        <Text body="Synergies" variant="accent" />
        <div style={PILLS}>
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

export const StakeCard: FC<StakeCardProps> = ({ name, className = "" }) => {
  const stake = getStake(name);
  if (!stake) {
    return (
      <Panel className={className} variant="muted">
        <Text body={`Stake not found: "${name}"`} variant="error" />
      </Panel>
    );
  }

  const difficultyTone: Record<StakeInfo["difficulty"], BadgeTone> = {
    Easy: "green",
    Medium: "orange",
    Hard: "red",
    Expert: "purple",
  };

  return (
    <Panel className={className}>
      <Stack gap={8}>
        <div style={HEADER}>
          <Text body={stake.name} variant="title" />
          <Badge label={stake.difficulty} tone={difficultyTone[stake.difficulty]} />
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

export const StrategyAdvisor: FC<StrategyAdvisorProps> = ({ jokers, className = "" }) => {
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
            <div style={PILLS}>
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
                  <div style={{ ...PILLS, marginTop: 4 }}>
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
            <div style={PILLS}>
              {Array.from(bossWarnings).map((b) => (
                <Badge key={b} label={b} tone="red" />
              ))}
            </div>
          </>
        )}

        {foundJokers.length === 0 && recommended.length === 0 && (
          <Text
            body="No recognized jokers or synergies found. Add jokers to get strategy advice."
            variant="muted"
          />
        )}
      </Stack>
    </Panel>
  );
};
