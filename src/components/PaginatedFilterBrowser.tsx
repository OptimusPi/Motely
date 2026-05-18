import React, { useState } from "react";
import { JimboButton, JimboPanel } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboFlankNav } from "../ui/jimboFlankNav.js";

export interface FilterItem {
  id: string;
  name: string;
  description: string;
  deckText?: string;
  stakeText?: string;
  targetItems?: string[];
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
  mainActionText = "Select",
  secondaryActionText = "Edit",
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
    <div className="j-filter-browser">
      <JimboPanel className="j-filter-browser__list-panel">
        <div
          className="j-filter-browser__list hide-scrollbar"
        >
          {pageFilters.map((filter) => {
            const isSelected = selectedId === filter.id;
            const deckStake = [filter.deckText, filter.stakeText].filter(Boolean).join(" / ");
            return (
              <button
                key={filter.id}
                type="button"
                className="j-filter-browser__item"
                data-active={isSelected}
                onClick={() => {
                  setSelectedId(filter.id);
                  onSelectFilter?.(filter);
                }}
              >
                <div className="j-filter-browser__item-main">
                  <JimboText size="xs" tone="grey" className="j-filter-browser__deck-stake">
                    {deckStake || "Any deck / Any stake"}
                  </JimboText>
                  <JimboText size="sm" tone={isSelected ? "gold" : "white"} className="j-filter-browser__name">
                    {filter.name}
                  </JimboText>
                </div>
                {filter.targetItems?.length ? (
                  <div className="j-filter-browser__targets" aria-label="Targets">
                    {filter.targetItems.slice(0, 4).map((target) => (
                      <span key={target} className="j-filter-browser__target">{target}</span>
                    ))}
                  </div>
                ) : null}
              </button>
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

        <div className="j-filter-browser__pager">
          <JimboFlankNav
            canPrev={safePage > 0}
            canNext={safePage < totalPages - 1}
            onPrev={handlePrevPage}
            onNext={handleNextPage}
          >
            <div className="j-filter-browser__page-chip">
              <JimboText size="sm" tone="white" className="j-text-center">
                {safePage + 1} / {totalPages}
              </JimboText>
            </div>
          </JimboFlankNav>
        </div>
      </JimboPanel>

      <JimboPanel className="j-filter-browser__details">
        {!selectedFilter ? (
          <JimboText size="md" tone="grey" className="j-text-center">
            Select an item to view details
          </JimboText>
        ) : (
          <div className="j-filter-browser__details-body">
            <JimboText size="xs" tone="grey" className="j-filter-browser__deck-stake">
              {[selectedFilter.deckText, selectedFilter.stakeText].filter(Boolean).join(" / ") || "Any deck / Any stake"}
            </JimboText>
            <JimboText size="lg" tone="gold" className="j-text-center">
              {selectedFilter.name}
            </JimboText>
            
            <JimboText size="sm" tone="white" className="j-text-center">
              {selectedFilter.description}
            </JimboText>

            {selectedFilter.targetItems?.length ? (
              <div className="j-filter-browser__targets j-filter-browser__targets--details">
                {selectedFilter.targetItems.map((target) => (
                  <span key={target} className="j-filter-browser__target">{target}</span>
                ))}
              </div>
            ) : null}

            <div className="j-filter-browser__meta">
              {selectedFilter.authorText && <JimboText size="xs" tone="grey">{selectedFilter.authorText}</JimboText>}
              {selectedFilter.dateText && <JimboText size="xs" tone="grey">{selectedFilter.dateText}</JimboText>}
              {selectedFilter.statsText && <JimboText size="xs" tone="grey">{selectedFilter.statsText}</JimboText>}
            </div>

            <div className="j-filter-browser__actions">
              <JimboButton tone="blue" size="md" onClick={() => onMainAction?.(selectedFilter)}>
                {mainActionText}
              </JimboButton>
              {showSecondary && onSecondaryAction && (
                <JimboButton tone="orange" size="sm" onClick={() => onSecondaryAction(selectedFilter)}>
                  {secondaryActionText}
                </JimboButton>
              )}
              {showDelete && onDeleteFilter && (
                <JimboButton tone="red" size="sm" onClick={() => onDeleteFilter(selectedFilter)}>
                  Delete
                </JimboButton>
              )}
            </div>
          </div>
        )}
      </JimboPanel>
    </div>
  );
}
