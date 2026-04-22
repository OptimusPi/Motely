"use client";

import React from "react";
import { JimboColorOption } from "../ui/tokens.js";

export type JamlIdeMode = "code" | "map" | "results";

export interface JamlIdeToolbarProps {
  mode: JamlIdeMode;
  onModeChange: (mode: JamlIdeMode) => void;
  resultCount?: number;
  className?: string;
  onSearch?: () => void;
  isSearching?: boolean;
}

const TABS: Array<{ id: JamlIdeMode; label: string }> = [
  { id: "code", label: "Code" },
  { id: "map", label: "Map" },
  { id: "results", label: "Results" },
];

export function JamlIdeToolbar({ mode, onModeChange, resultCount = 0, className = "", onSearch, isSearching = false }: JamlIdeToolbarProps) {
  return (
    <div
      className={className}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 8,
        padding: "6px 10px",
        borderBottom: `1px solid ${JimboColorOption.PANEL_EDGE}`,
        background: JimboColorOption.DARKEST,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
        {TABS.map((tab) => {
          const selected = mode === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => onModeChange(tab.id)}
              style={{
                cursor: "pointer",
                borderRadius: 6,
                border: selected ? `1px solid ${JimboColorOption.GOLD}` : "1px solid transparent",
                background: selected ? `${JimboColorOption.GOLD}22` : "transparent",
                color: selected ? JimboColorOption.GOLD_TEXT : JimboColorOption.GREY,
                padding: "5px 10px",
                fontSize: 11,
                fontWeight: 700,
                fontFamily: "m6x11plus, monospace",
                transition: "background 120ms, color 120ms",
              }}
            >
              {tab.label}
              {tab.id === "results" && resultCount > 0 ? (
                <span
                  style={{
                    marginLeft: 6,
                    borderRadius: 999,
                    background: `${JimboColorOption.GOLD}33`,
                    color: JimboColorOption.GOLD_TEXT,
                    padding: "1px 6px",
                    fontSize: 10,
                  }}
                >
                  {resultCount}
                </span>
              ) : null}
            </button>
          );
        })}
      </div>

      {onSearch ? (
        <button
          type="button"
          onClick={onSearch}
          style={{
            cursor: "pointer",
            borderRadius: 6,
            border: isSearching
              ? `1px solid ${JimboColorOption.DARK_RED}`
              : `1px solid ${JimboColorOption.GREEN}`,
            background: isSearching
              ? `${JimboColorOption.RED}22`
              : `${JimboColorOption.GREEN}22`,
            color: isSearching ? JimboColorOption.RED : JimboColorOption.GREEN_TEXT,
            padding: "5px 14px",
            fontSize: 11,
            fontWeight: 700,
            fontFamily: "m6x11plus, monospace",
          }}
        >
          {isSearching ? "Stop" : "Search"}
        </button>
      ) : null}
    </div>
  );
}
