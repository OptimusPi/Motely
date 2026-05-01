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
  const statusTone = status === "error" ? "red" : active ? "green" : "grey";

  return (
    <div className={`j-stat-grid ${className}`} style={style}>
      <div>
        <div className="j-stat-grid__value">
          <JimboText size="md" tone={active ? "gold" : "grey"}>{formatSpeed(seedsPerSecond)}</JimboText>
        </div>
        <div className="j-stat-grid__label">speed</div>
      </div>
      <div>
        <div className="j-stat-grid__value">
          <JimboText size="md" tone="white">{formatCount(totalSearched)}</JimboText>
        </div>
        <div className="j-stat-grid__label">searched</div>
      </div>
      <div>
        <div className="j-stat-grid__value">
          <JimboText size="md" tone={Number(matchingSeeds) > 0 ? "green" : "grey"}>{formatCount(matchingSeeds)}</JimboText>
        </div>
        <div className="j-stat-grid__label">matches</div>
      </div>
    </div>
  );
}
