"use client";

import { type FC, useState } from "react";
import { FiAlertTriangle, FiChevronLeft, FiChevronRight } from "react-icons/fi";
import { Panel, Stack, Text, Badge, type BadgeTone } from "./layout.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboButton } from "../../ui/JimboButton.js";
import { JimboInnerPanel } from "../../ui/panel.js";
import { JimboGrid } from "../../ui/JimboGrid.js";
import { JimboStatusPill, type JimboStatus } from "../../ui/JimboStatusPill.js";
import { JimboErrorBlock } from "../../ui/JimboErrorBlock.js";
import { JimboRow } from "../../ui/JimboLayout.js";

/**
 * Domain components — SearchStats, ErrorBanner, LoadingPulse, SeedCard, etc.
 *
 * Balatro-specific json-render nodes, composed from Jimbo primitives.
 * Interactive controls are the real Jimbo button primitives; status, error,
 * and layout chrome come from src/ui/ so there is one grammar, not two.
 */

/* ─── SearchStats ─── */
export interface SearchStatsProps {
  status: "idle" | "running" | "completed" | "error";
  seedsSearched?: string;
  matchesFound?: number;
  seedsPerSecond?: number;
  elapsed?: string;
  className?: string;
}

const STATUS_PILL: Record<SearchStatsProps["status"], { pill: JimboStatus; label: string }> = {
  idle: { pill: "idle", label: "Ready" },
  running: { pill: "running", label: "Searching..." },
  completed: { pill: "ok", label: "Done" },
  error: { pill: "error", label: "Error" },
};

export const SearchStats: FC<SearchStatsProps> = ({
  status,
  seedsSearched,
  matchesFound,
  seedsPerSecond,
  elapsed,
  className = "",
}) => {
  const { pill, label } = STATUS_PILL[status];
  const stats: { label: string; value: string }[] = [];
  if (seedsSearched !== undefined) stats.push({ label: "Searched", value: seedsSearched });
  if (matchesFound !== undefined) stats.push({ label: "Matches", value: String(matchesFound) });
  if (seedsPerSecond !== undefined) stats.push({ label: "Speed", value: `${seedsPerSecond.toLocaleString()}/s` });
  if (elapsed) stats.push({ label: "Time", value: elapsed });

  return (
    <Panel className={className} variant="accent">
      <Stack gap={8}>
        <JimboRow gap="sm" justify="start">
          <JimboStatusPill status={pill} label={label} />
        </JimboRow>
        {stats.length > 0 && (
          <JimboGrid minColWidth={120} gap="lg">
            {stats.map((stat) => (
              <Stack key={stat.label} gap={2}>
                <Text body={stat.label} variant="muted" />
                <Text body={stat.value} variant="title" />
              </Stack>
            ))}
          </JimboGrid>
        )}
      </Stack>
    </Panel>
  );
};

/* ─── ErrorBanner ─── */
export interface ErrorBannerProps {
  message: string;
  onDismiss?: boolean;
  className?: string;
}

export const ErrorBanner: FC<ErrorBannerProps> = ({ message, onDismiss, className = "" }) => (
  <JimboErrorBlock className={className} onDismiss={onDismiss ? () => {} : undefined}>
    <JimboRow gap="sm" justify="start">
      <FiAlertTriangle aria-hidden />
      <Text body={message} variant="error" />
    </JimboRow>
  </JimboErrorBlock>
);

/* ─── LoadingPulse ─── */
export interface LoadingPulseProps {
  text?: string;
  className?: string;
}

export const LoadingPulse: FC<LoadingPulseProps> = ({ text = "Loading...", className = "" }) => (
  <JimboRow gap="md" justify="center" className={className}>
    <JimboStatusPill status="running" label={text} />
  </JimboRow>
);

/* ─── SeedCard ─── */
export interface SeedCardProps {
  seed: string;
  score?: number;
  rank?: number;
  highlights?: string[];
  jokers?: string[];
  edition?: string;
  onClick?: boolean; // semantic: card is interactive
  className?: string;
}

export const SeedCard: FC<SeedCardProps> = ({
  seed,
  score,
  rank,
  highlights,
  jokers,
  edition,
  onClick,
  className = "",
}) => {
  return (
    <JimboInnerPanel
      className={["j-stack", "j-stack--gap-sm", onClick ? "j-seed-card--interactive" : "", className]
        .filter(Boolean)
        .join(" ")}
      onClick={onClick ? () => navigator.clipboard?.writeText(seed) : undefined}
      role={onClick ? "button" : undefined}
      tabIndex={onClick ? 0 : undefined}
      title={onClick ? "Copy seed" : undefined}
    >
      <JimboRow justify="between" align="center">
        <JimboRow gap="sm" justify="start">
          {rank !== undefined && (
            <JimboText size="sm" tone="gold">
              #{rank}
            </JimboText>
          )}
          <Text body={seed} variant="accent" />
        </JimboRow>
        {score !== undefined && <Badge label={String(score)} tone="gold" />}
      </JimboRow>

      {edition && <Badge label={edition} tone="purple" />}

      {highlights && highlights.length > 0 && (
        <JimboRow wrap gap="sm" justify="start">
          {highlights.map((h, i) => (
            <Badge key={i} label={h} tone="green" />
          ))}
        </JimboRow>
      )}

      {jokers && jokers.length > 0 && (
        <JimboRow wrap gap="sm" justify="start">
          {jokers.map((j, i) => (
            <Badge key={i} label={j} tone="blue" />
          ))}
        </JimboRow>
      )}
    </JimboInnerPanel>
  );
};

/* ─── SeedList ─── */
export interface SeedListProps {
  seeds: string[];
  scores?: number[];
  total?: number;
  pageSize?: number;
  className?: string;
}

export const SeedList: FC<SeedListProps> = ({
  seeds,
  scores,
  total,
  pageSize = 20,
  className = "",
}) => {
  const [page, setPage] = useState(0);
  const maxPage = Math.ceil((total ?? seeds.length) / pageSize) - 1;
  const start = page * pageSize;
  const visible = seeds.slice(start, start + pageSize);

  return (
    <Stack gap={12} className={className}>
      {visible.map((seed, i) => (
        <SeedCard
          key={seed}
          seed={seed}
          score={scores?.[start + i]}
          rank={start + i + 1}
          onClick={true}
        />
      ))}

      {maxPage > 0 && (
        <JimboRow gap="md" justify="center">
          <JimboButton
            size="sm"
            tone="blue"
            disabled={page <= 0}
            onClick={() => setPage((p) => p - 1)}
          >
            <FiChevronLeft /> Prev
          </JimboButton>
          <Text body={`${page + 1} / ${maxPage + 1}`} variant="muted" />
          <JimboButton
            size="sm"
            tone="blue"
            disabled={page >= maxPage}
            onClick={() => setPage((p) => p + 1)}
          >
            Next <FiChevronRight />
          </JimboButton>
        </JimboRow>
      )}
    </Stack>
  );
};

/* ─── JokerBadge ─── */
export interface JokerBadgeProps {
  name: string;
  edition?: "Foil" | "Holographic" | "Polychrome" | "Negative";
  rarity?: "Common" | "Uncommon" | "Rare" | "Legendary";
  className?: string;
}

export const JokerBadge: FC<JokerBadgeProps> = ({ name, edition, rarity, className = "" }) => {
  const rarityTone: Record<string, BadgeTone> = {
    Common: "grey",
    Uncommon: "green",
    Rare: "red",
    Legendary: "gold",
  };

  const editionTone: Record<string, BadgeTone> = {
    Foil: "blue",
    Holographic: "green",
    Polychrome: "purple",
    Negative: "red",
  };

  return (
    <JimboRow gap="sm" justify="start" className={className}>
      <Badge label={name} tone={rarityTone[rarity ?? ""] ?? "grey"} />
      {edition && <Badge label={edition} tone={editionTone[edition] ?? "grey"} />}
    </JimboRow>
  );
};

/* ─── EditionBadge ─── */
export interface EditionBadgeProps {
  edition: "Foil" | "Holographic" | "Polychrome" | "Negative";
  className?: string;
}

export const EditionBadge: FC<EditionBadgeProps> = ({ edition, className = "" }) => {
  const map: Record<string, BadgeTone> = {
    Foil: "blue",
    Holographic: "green",
    Polychrome: "purple",
    Negative: "red",
  };

  return <Badge className={className} label={edition} tone={map[edition] ?? "grey"} />;
};
