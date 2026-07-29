"use client";

import React from "react";
import { JimboButton } from "../ui/panel.js";
import { JimboTabs } from "../ui/jimboTabs.js";

export type JamlIdeMode = "visual" | "code" | "map" | "results" | "jamlyzer";

export interface JamlIdeToolbarProps {
  mode: JamlIdeMode;
  onModeChange: (mode: JamlIdeMode) => void;
  resultCount?: number;
  showResultsTab?: boolean;
  showJamlyzerTab?: boolean;
  className?: string;
  onSearch?: () => void;
  isSearching?: boolean;
  onLoadFile?: () => void;
  isLoadingFile?: boolean;
}

export function JamlIdeToolbar({
  mode,
  onModeChange,
  resultCount = 0,
  showResultsTab = false,
  showJamlyzerTab = false,
  className = "",
  onSearch,
  isSearching = false,
  onLoadFile,
  isLoadingFile = false,
}: JamlIdeToolbarProps) {
  const tabs: Array<{ id: JamlIdeMode; label: string }> = [
    { id: "visual", label: "Visual" },
    { id: "code", label: "JAML" },
    { id: "map", label: "Map" },
  ];

  if (showResultsTab) {
    tabs.push({ id: "results", label: resultCount > 0 ? `Results (${resultCount})` : "Results" });
  }

  if (showJamlyzerTab) {
    tabs.push({ id: "jamlyzer", label: "Test" });
  }

  return (
    <div className={`j-ide-toolbar ${className}`.trim()}>
      <div className="j-ide-toolbar__tabs">
        <JimboTabs
          tabs={tabs}
          activeTab={mode}
          onTabChange={(id) => onModeChange(id as JamlIdeMode)}
        />
      </div>

      {onSearch && (
        <div className="j-ide-toolbar__action">
          <JimboButton tone="red" size="sm" onClick={onSearch}>
            {isSearching ? "Stop" : "Search"}
          </JimboButton>
        </div>
      )}

      {onLoadFile && (
        <div className="j-ide-toolbar__action">
          <JimboButton tone="blue" size="sm" onClick={onLoadFile} disabled={isLoadingFile}>
            {isLoadingFile ? "Loading..." : "Load File"}
          </JimboButton>
        </div>
      )}
    </div>
  );
}
