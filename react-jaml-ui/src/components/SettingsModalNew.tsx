import React from "react"

interface Filter {
  id: string
  name: string
  content: string
  author?: string
  created: string
}

interface SettingsModalProps {
  filters: Filter[]
  onClose: () => void
  onSelectFilter: (filter: Filter) => void
  onDeleteFilter: (filter: Filter) => void
}

export function SettingsModalNew({ filters, onClose, onSelectFilter, onDeleteFilter }: SettingsModalProps) {
  return (
    <div className="modal" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3 className="modal-title">Settings</h3>
          <button onClick={onClose} className="modal-close">×</button>
        </div>
        <div className="modal-body">
          <div className="settings-section">
            <h4>Filters</h4>
            <div className="filter-list">
              {filters.map((filter) => (
                <div key={filter.id} className="filter-item">
                  <button
                    className="filter-select"
                    onClick={() => onSelectFilter(filter)}
                  >
                    <span className="filter-name">{filter.name}</span>
                    <small className="filter-meta">{filter.author || 'Unknown'}</small>
                  </button>
                  <button
                    onClick={() => onDeleteFilter(filter)}
                    className="btn btn-danger btn-sm"
                  >
                    Delete
                  </button>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
