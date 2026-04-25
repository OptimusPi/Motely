"use client";

import React from "react";
import { JimboColorOption } from "../ui/tokens.js";
import { JimboText } from "../ui/jimboText.js";
import type { SearchStatus } from "../hooks/useSearch.js";

export interface JamlSpeedometerProps {
  seedsPerSecond: number;
  totalSearched: bigint;
  matchingSeeds: bigint;
  status: SearchStatus;
  className?: string;
  style?: React.CSSProperties;
}

function formatCount(n: bigint): string {
  if (n >= 1_000_000_000n) return `${(Number(n / 1_000_000n) / 1000).toFixed(1)}B`;
  if (n >= 1_000_000n) return `${(Number(n / 1_000n) / 1000).toFixed(1)}M`;
  if (n >= 1_000n) return `${(Number(n) / 1000).toFixed(1)}K`;
  return n.toString();
}

function formatSpeed(sps: number): string {
  if (sps >= 1_000_000) return `${(sps / 1_000_000).toFixed(1)}M`;
  if (sps >= 1_000) return `${(sps / 1_000).toFixed(0)}K`;
  return sps.toString();
}

function needleAngle(sps: number): number {
  if (sps <= 0) return -90;
  const maxLog = Math.log10(5_000_000);
  const clamped = Math.min(sps, 5_000_000);
  const pct = Math.log10(Math.max(clamped, 1)) / maxLog;
  return -90 + pct * 180;
}

export function JamlSpeedometer({
  seedsPerSecond,
  totalSearched,
  matchingSeeds,
  status,
  className,
  style,
}: JamlSpeedometerProps) {
  const isActive = status === "running" || status === "booting";
  const angle = needleAngle(seedsPerSecond);
  const speedColor = seedsPerSecond >= 500_000
    ? JimboColorOption.GREEN
    : seedsPerSecond >= 100_000
      ? JimboColorOption.GOLD
      : seedsPerSecond > 0
        ? JimboColorOption.ORANGE
        : JimboColorOption.GREY;

  return (
    <div
      className={className}
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        gap: 6,
        padding: "12px 16px",
        borderRadius: 10,
        background: `${JimboColorOption.DARKEST}cc`,
        border: `1px solid ${JimboColorOption.PANEL_EDGE}`,
        ...style,
      }}
    >
      {/* Gauge */}
      <div style={{ position: "relative", width: 120, height: 68, overflow: "hidden" }}>
        <svg viewBox="0 0 120 68" width={120} height={68}>
          {/* Track */}
          <path
            d="M 10 65 A 50 50 0 0 1 110 65"
            fill="none"
            stroke={JimboColorOption.DARK_GREY}
            strokeWidth={6}
            strokeLinecap="round"
          />
          {/* Active arc */}
          {isActive && (
            <path
              d="M 10 65 A 50 50 0 0 1 110 65"
              fill="none"
              stroke={speedColor}
              strokeWidth={6}
              strokeLinecap="round"
              strokeDasharray="157"
              strokeDashoffset={157 - (157 * ((angle + 90) / 180))}
              style={{ transition: "stroke-dashoffset 300ms ease, stroke 300ms ease" }}
            />
          )}
          {/* Needle */}
          <line
            x1={60} y1={65} x2={60} y2={20}
            stroke={isActive ? JimboColorOption.RED : JimboColorOption.GREY}
            strokeWidth={2}
            strokeLinecap="round"
            style={{
              transformOrigin: "60px 65px",
              transform: `rotate(${angle}deg)`,
              transition: "transform 300ms ease",
            }}
          />
          <circle cx={60} cy={65} r={4} fill={JimboColorOption.RED} />
        </svg>
      </div>

      {/* Speed readout */}
      <div style={{ textAlign: "center" }}>
        <div style={{ fontSize: 20, fontWeight: 900, fontFamily: "m6x11plus, monospace", color: isActive ? speedColor : JimboColorOption.GREY }}>
          {isActive ? formatSpeed(seedsPerSecond) : "---"}
        </div>
        <JimboText size="xs" tone="grey" uppercase>seeds / sec</JimboText>
      </div>

      {/* Stats row */}
      <div style={{ display: "flex", gap: 16, marginTop: 2 }}>
        <div style={{ textAlign: "center" }}>
          <div style={{ fontSize: 13, fontWeight: 700, fontFamily: "m6x11plus, monospace", color: JimboColorOption.WHITE }}>
            {formatCount(totalSearched)}
          </div>
          <JimboText size="xs" tone="grey" uppercase>searched</JimboText>
        </div>
        <div style={{ textAlign: "center" }}>
          <div style={{ fontSize: 13, fontWeight: 700, fontFamily: "m6x11plus, monospace", color: JimboColorOption.GREEN_TEXT }}>
            {formatCount(matchingSeeds)}
          </div>
          <JimboText size="xs" tone="grey" uppercase>matches</JimboText>
        </div>
      </div>
    </div>
  );
}
