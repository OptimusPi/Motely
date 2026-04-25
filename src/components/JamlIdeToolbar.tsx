"use client";

import React from "react";
import { JimboButton } from "../ui/panel.js";
import { JimboTabs } from "../ui/jimboTabs.js";
import { JimboColorOption } from "../ui/tokens.js";

export type JamlIdeMode = "visual" | "code" | "map" | "results";

export interface JamlIdeToolbarProps {
  mode: JamlIdeMode;
  onModeChange: (mode: JamlIdeMode) => void;
  resultCount?: number;
  className?: string;
  onSearch?: () => void;
  isSearching?: boolean;
}

export function JamlIdeToolbar({ mode, onModeChange, resultCount = 0, className = "", onSearch, isSearching = false }: JamlIdeToolbarProps) {
  const tabs = [
    { id: "visual", label: "Visual" },
    { id: "code", label: ".jaml" },
    { id: "map", label: "Map" },
    { id: "results", label: resultCount > 0 ? `Results (${resultCount})` : "Results" },
  ];

  return (
    <div
      className={className}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 8,
        padding: "10px 10px 6px",
        borderBottom: `1px solid ${JimboColorOption.PANEL_EDGE}`,
        background: JimboColorOption.DARKEST,
      }}
    >
      <JimboTabs
        tabs={tabs}
        activeTab={mode}
        onTabChange={(id) => onModeChange(id as JamlIdeMode)}
      />

      {onSearch ? (
        <JimboButton tone={isSearching ? "red" : "blue"} size="xs" onClick={onSearch}>
          {isSearching ? "Stop" : "Search"}
        </JimboButton>
      ) : null}
    </div>
  );
}
