"use client";

import React from "react";
import { JimboColorOption } from "../ui/tokens.js";

const C = JimboColorOption;

export type JamlSpeedometerStatus = "idle" | "booting" | "running" | "completed" | "cancelled" | "error";

export interface JamlSpeedometerProps {
  seedsPerSecond: number;
  totalSearched: bigint | number;
  matchingSeeds: bigint | number;
  status: JamlSpeedometerStatus;
}

function formatCount(value: bigint | number): string {
  return Number(value).toLocaleString();
}

function formatSpeed(value: number): string {
  if (!Number.isFinite(value) || value <= 0) return "0/s";
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M/s`;
  if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K/s`;
  return `${Math.round(value)}/s`;
}

/**
 * Compact live-search stats strip for MCP/app chrome.
 */
export function JamlSpeedometer({
  seedsPerSecond,
  totalSearched,
  matchingSeeds,
  status,
}: JamlSpeedometerProps) {
  const active = status === "running" || status === "booting";
  const tone = status === "error" ? C.RED : active ? C.GOLD : C.GREY;

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        color: tone,
        fontSize: 11,
        fontFamily: "var(--font-sans, m6x11plus), monospace",
        whiteSpace: "nowrap",
      }}
    >
      <span>{status}</span>
      <span>{formatSpeed(seedsPerSecond)}</span>
      <span>{formatCount(totalSearched)} searched</span>
      <span>{formatCount(matchingSeeds)} matches</span>
    </div>
  );
}
