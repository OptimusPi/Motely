"use client";

import React, { useState } from "react";
import { FiCheck, FiCopy } from "react-icons/fi";

export interface JimboSeedCopyChipProps {
  value: string;
  placeholder?: string;
  disabled?: boolean;
  copiedLabel?: string;
  copiedDurationMs?: number;
  onCopy?: (value: string) => void;
  className?: string;
  style?: React.CSSProperties;
}

/**
 * Harvested from WeeJoker SeedCard — tap-to-copy seed row with icon + value.
 * Idle: copy icon + seed. Copied: green check + brief confirmation label.
 */
export function JimboSeedCopyChip({
  value,
  placeholder = "--------",
  disabled = false,
  copiedLabel = "Copied!",
  copiedDurationMs = 2000,
  onCopy,
  className = "",
  style,
}: JimboSeedCopyChipProps) {
  const [copied, setCopied] = useState(false);
  const display = value.trim();
  const canCopy = !disabled && display.length > 0;

  const handleCopy = async () => {
    if (!canCopy) return;
    try {
      if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(display);
      }
    } catch {
      // Non-fatal in Storybook or restricted clipboard contexts.
    }
    setCopied(true);
    onCopy?.(display);
    window.setTimeout(() => setCopied(false), copiedDurationMs);
  };

  const rootClass = ["j-seed-copy", className].filter(Boolean).join(" ");

  return (
    <button
      type="button"
      className={rootClass}
      style={style}
      onClick={handleCopy}
      disabled={!canCopy}
      data-copied={copied}
      aria-label={canCopy ? `Copy seed ${display}` : "Seed unavailable"}
    >
      <span className="j-seed-copy__icon" aria-hidden>
        {copied ? <FiCheck size={18} /> : <FiCopy size={18} />}
      </span>
      <span className="j-seed-copy__text">
        {copied ? copiedLabel : display || placeholder}
      </span>
    </button>
  );
}
