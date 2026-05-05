"use client";

import React from "react";
import { JimboText } from "../ui/jimboText.js";

export type JamlSpeedometerStatus = "idle" | "booting" | "running" | "completed" | "cancelled" | "error";

export interface JamlSpeedometerProps {
  seedsPerSecond: number;
  totalSearched: bigint | number;
  matchingSeeds: bigint | number;
  status: JamlSpeedometerStatus;
  className?: string;
  style?: React.CSSProperties;
}

function formatCount(value: bigint | number): string {
  return Number(value).toLocaleString();
}

function formatSpeed(value: number): string {
  if (!Number.isFinite(value) || value <= 0) return "—";
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M/s`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K/s`;
  return `${Math.round(value)}/s`;
}

/**
 * Compact live-search stats strip — NOT a car speedometer.
 * Three stat cells in a row: speed | searched | matches.
 * Uses j-stat-grid CSS class from jimbo.css.
 */
export function JamlSpeedometer({
  seedsPerSecond,
  totalSearched,
  matchingSeeds,
  status,
  className = "",
  style,
}: JamlSpeedometerProps) {
  const active = status === "running" || status === "booting";
  const statusTone =
    status === "error"
      ? "red"
      : status === "completed"
        ? "green"
        : status === "cancelled"
          ? "orange"
          : active
            ? "gold"
            : "grey";
  const statusLabel =
    status === "booting"
      ? "booting"
      : status === "running"
        ? "searching"
        : status === "completed"
          ? "done"
          : status === "cancelled"
            ? "cancelled"
            : status === "error"
              ? "error"
              : "idle";
  const rootStyle: React.CSSProperties = {
    display: "flex",
    alignItems: "center",
    flexWrap: "wrap",
    gap: 8,
    padding: "8px 10px",
    borderRadius: 12,
    border: "1px solid var(--j-panel-edge, rgba(255,255,255,0.12))",
    background: "rgba(0,0,0,0.28)",
    ...style,
  };
  const dotStyle: React.CSSProperties = {
    width: 8,
    height: 8,
    borderRadius: 999,
    background:
      status === "error"
        ? "var(--j-red, #ff4c40)"
        : status === "completed"
          ? "var(--j-green, #429f79)"
          : status === "cancelled"
            ? "var(--j-orange, #ff9800)"
            : active
              ? "var(--j-gold, #e4b643)"
              : "var(--j-grey, #8b8b8b)",
    flexShrink: 0,
  };

  return (
    <div className={className} style={rootStyle}>
      <span style={dotStyle} aria-hidden="true" />
      <JimboText size="sm" tone={statusTone}>{statusLabel}</JimboText>
      <JimboText size="sm" tone={active ? "gold" : "grey"}>{formatSpeed(seedsPerSecond)}</JimboText>
      <JimboText size="sm" tone="white">{formatCount(totalSearched)} searched</JimboText>
      <JimboText size="sm" tone={Number(matchingSeeds) > 0 ? "green" : "grey"}>
        {formatCount(matchingSeeds)} matches
      </JimboText>
    </div>
  );
}
