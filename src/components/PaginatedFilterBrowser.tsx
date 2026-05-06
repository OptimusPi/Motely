import React, { useState } from "react";
import { JimboButton, JimboPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboFlankNav } from "../ui/jimboFlankNav.js";
import { JimboColorOption } from "../ui/tokens.js";

const C = JimboColorOption;

export interface FilterItem {
  id: string;
  name: string;
  description: string;
  authorText?: string;
  dateText?: string;
  statsText?: string;
}

export interface PaginatedFilterBrowserProps {
  filters: FilterItem[];
  itemsPerPage?: number;
  onSelectFilter?: (filter: FilterItem) => void;
  onMainAction?: (filter: FilterItem) => void;
  onSecondaryAction?: (filter: FilterItem) => void;
  onDeleteFilter?: (filter: FilterItem) => void;
  mainActionText?: string;
  secondaryActionText?: string;
  showSecondary?: boolean;
  showDelete?: boolean;
  emptyText?: string;
}

export function PaginatedFilterBrowser({
  filters,
  itemsPerPage = 120, // matching Balatro challenges layout pages
  onSelectFilter,
  onMainAction,
  onSecondaryAction,
  onDeleteFilter,
  mainActionText = "SELECT",
  secondaryActionText = "EDIT",
  showSecondary = false,
  showDelete = false,
  emptyText = "No items found.",
}: PaginatedFilterBrowserProps) {
  const [currentPage, setCurrentPage] = useState(0);
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const totalPages = Math.max(1, Math.ceil(filters.length / itemsPerPage));
  // Bound current page just in case filters array shrinks
  const safePage = Math.min(currentPage, totalPages - 1);
  const pageFilters = filters.slice(safePage * itemsPerPage, (safePage + 1) * itemsPerPage);

  const selectedFilter = filters.find((f) => f.id === selectedId) || null;

  const handlePrevPage = () => setCurrentPage((p) => Math.max(0, p - 1));
  const handleNextPage = () => setCurrentPage((p) => Math.min(totalPages - 1, p + 1));

  return (
    <div
      className="j-flex-col j-gap-md"
      style={{
        width: "100%",
        height: "100%",
        boxSizing: "border-box",
        overflow: "hidden",
      }}
    >
      {/* Scrollable List Panel */}
      <JimboPanel style={{ flex: 1, minHeight: 0, display: "flex", flexDirection: "column" }}>
        <div
          style={{ flex: 1, overflowY: "auto", position: "relative" }}
          className="hide-scrollbar j-flex-col j-items-center j-gap-xs"
        >
          {pageFilters.map((filter) => {
            const isSelected = selectedId === filter.id;
            return (
              <div
                key={filter.id}
                style={{
                  position: "relative",
                  width: "100%",
                  maxWidth: 240, // Matches filter-list-item MinWidth=210 approx
                  display: "flex",
                  justifyContent: "center",
                }}
              >
                {/* Triangle Indicator */}
                {isSelected && (
                  <div
                    className="j-animate-jimbo-bounce"
                    style={{
                      position: "absolute",
                      left: -20, // push out to left
                      top: "50%",
                      transform: "translateY(-50%)",
                      width: 0,
                      height: 0,
                      borderTop: "8px solid transparent",
                      borderBottom: "8px solid transparent",
                      borderLeft: `12px solid ${C.GOLD}`, // Balatro usually uses gold/white for selection triangle
                      filter: `drop-shadow(2px 2px 2px rgba(0,0,0,0.5))`,
                    }}
                  />
                )}
                
                <div style={{ width: "100%" }}>
                  <JimboButton
                    tone={isSelected ? "red" : "grey"}
                    size="sm"
                    style={{ width: "100%" }}
                    onClick={() => {
                      setSelectedId(filter.id);
                      onSelectFilter?.(filter);
                    }}
                  >
                    {filter.name}
                  </JimboButton>
                </div>
              </div>
            );
          })}

          {pageFilters.length === 0 && (
            <div className="j-p-md">
              <JimboText size="sm" tone="grey" className="j-text-center">
                {emptyText}
              </JimboText>
            </div>
          )}
        </div>

        {/* Pagination Controls */}
        <div className="j-flex j-justify-center j-mt-md" style={{ flexShrink: 0 }}>
          <JimboFlankNav
            canPrev={safePage > 0}
            canNext={safePage < totalPages - 1}
            onPrev={handlePrevPage}
            onNext={handleNextPage}
          >
            <div
              style={{
                background: C.RED,
                padding: "8px 24px",
                borderRadius: 8,
                minWidth: 100,
                boxShadow: `inset 0 2px 4px rgba(255,255,255,0.2), 0 4px 8px rgba(0,0,0,0.4)`,
              }}
              className="j-flex j-justify-center j-items-center"
            >
              <JimboText size="sm" tone="white" className="j-text-center" style={{ textShadow: "1px 2px 2px rgba(0,0,0,0.5)" }}>
                {safePage + 1} / {totalPages}
              </JimboText>
            </div>
          </JimboFlankNav>
        </div>
      </JimboPanel>

      {/* Filter Details Panel (Bottom Context) */}
      <JimboPanel style={{ flexShrink: 0, minHeight: 180, display: "flex", flexDirection: "column", justifyContent: "center" }}>
        {!selectedFilter ? (
          <JimboText size="md" tone="grey" className="j-text-center">
            Select an item to view details
          </JimboText>
        ) : (
          <div className="j-flex-col j-gap-sm j-items-center" style={{ width: "100%" }}>
            <JimboText size="lg" tone="gold" className="j-text-center">
              {selectedFilter.name}
            </JimboText>
            
            <JimboText size="sm" tone="white" className="j-text-center">
              {selectedFilter.description}
            </JimboText>

            <div className="j-flex-col j-gap-xs j-items-center j-mt-xs" style={{ opacity: 0.7 }}>
              {selectedFilter.authorText && <JimboText size="xs" tone="grey">{selectedFilter.authorText}</JimboText>}
              {selectedFilter.dateText && <JimboText size="xs" tone="grey">{selectedFilter.dateText}</JimboText>}
              {selectedFilter.statsText && <JimboText size="xs" tone="grey">{selectedFilter.statsText}</JimboText>}
            </div>

            <div className="j-flex j-gap-sm j-mt-md j-justify-center j-flex-wrap">
              <JimboButton tone="blue" size="sm" onClick={() => onMainAction?.(selectedFilter)}>
                {mainActionText}
              </JimboButton>
              {showSecondary && onSecondaryAction && (
                <JimboButton tone="orange" size="sm" onClick={() => onSecondaryAction(selectedFilter)}>
                  {secondaryActionText}
                </JimboButton>
              )}
              {showDelete && onDeleteFilter && (
                <JimboButton tone="red" size="sm" onClick={() => onDeleteFilter(selectedFilter)}>
                  DELETE
                </JimboButton>
              )}
            </div>
          </div>
        )}
      </JimboPanel>
    </div>
  );
}
