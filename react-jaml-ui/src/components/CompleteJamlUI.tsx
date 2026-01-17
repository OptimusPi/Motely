import React, { useState, useRef, useEffect } from "react"
import { useFilters } from "../hooks/useFilters"
import { useSearch } from "../hooks/useSearch"
import { useSignalR } from "../hooks/useSignalR"

export function CompleteJamlUI() {
  const [splitWidth, setSplitWidth] = useState(50)
  const [isDragging, setIsDragging] = useState(false)
  const [showSettings, setShowSettings] = useState(false)
  const dividerRef = useRef<HTMLDivElement>(null)
  
  const {
    filters,
    currentFilter,
    jamlContent,
    setJamlContent,
    loadFilters,
    selectFilter,
    saveFilter,
    deleteFilter
  } = useFilters()
  
  const {
    results,
    searchStatus,
    isSearching,
    startSearch,
    stopAll,
    exportResults,
    setResults,
    setSearchStatus,
    setIsSearching
  } = useSearch()
  
  const { connect, disconnect, joinSearchGroup } = useSignalR({
    onResult: (result) => {
      if (result && typeof result === 'object') {
        setResults([result, ...results])
      }
    },
    onProgress: (progress) => {
      if (progress && typeof progress === 'object') {
        const proc = progress.processed || progress.seedsSearched || 0
        setSearchStatus(`Searching... Processed: ${proc}`)
      }
    },
    onSearchUpdate: (update) => {
      if (update && typeof update === 'object' && update.searchId) {
        // Handle search updates
        if (update.status === 'completed') {
          setIsSearching(false)
          setSearchStatus('Search completed')
        } else if (update.status === 'failed') {
          setIsSearching(false)
          setSearchStatus('Search failed')
        }
      }
    }
  })
  
  const handleMouseDown = (e: React.MouseEvent) => {
    setIsDragging(true)
    e.preventDefault()
  }
  
  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDragging || !dividerRef.current) return
      
      const container = dividerRef.current.parentElement
      if (!container) return
      
      const rect = container.getBoundingClientRect()
      const percent = ((e.clientX - rect.left) / rect.width) * 100
      const clampedPercent = Math.max(10, Math.min(90, percent))
      
      setSplitWidth(clampedPercent)
    }
    
    const handleMouseUp = () => {
      setIsDragging(false)
    }
    
    if (isDragging) {
      document.addEventListener("mousemove", handleMouseMove)
      document.addEventListener("mouseup", handleMouseUp)
      
      return () => {
        document.removeEventListener("mousemove", handleMouseMove)
        document.removeEventListener("mouseup", handleMouseUp)
      }
    }
  }, [isDragging])
  
  useEffect(() => {
    loadFilters()
    connect().catch(e => console.warn('SignalR connection failed (non-critical):', e?.message))
    
    return () => {
      disconnect()
    }
  }, [])
  
  const handleHome = () => {
    window.location.href = "/"
  }
  
  const handleReset = () => {
    setSplitWidth(50)
  }
  
  const handleSave = async () => {
    const success = await saveFilter(jamlContent)
    if (success) {
      console.log('Filter saved successfully!')
    }
  }
  
  const handleSearch = async () => {
    const searchId = await startSearch(jamlContent)
    if (searchId) {
      await joinSearchGroup(searchId)
      setSearchStatus('Search started - listening for updates')
    }
  }
  
  const handleSelectFilter = async (filter: any) => {
    await selectFilter(filter)
    setJamlContent(filter.filterJaml || '')
    setShowSettings(false)
  }
  
  const handleDeleteFilter = async (filter: any) => {
    const success = await deleteFilter(filter)
    if (success && currentFilter?.id === filter.id) {
      setJamlContent('')
    }
  }
  
  return (
    <div className="jaml-ui" style={{
      height: "100vh",
      minHeight: "100vh",
      overflow: "visible",
      background: "rgba(0, 0, 0, 0.4)",
      position: "relative",
      borderLeft: "1px solid rgba(255, 255, 255, 0.08)",
      borderRight: "1px solid rgba(255, 255, 255, 0.08)",
      boxShadow: "inset 0 0 40px rgba(0, 0, 0, 0.6), 0 30px 60px rgba(0, 0, 0, 0.45)",
      paddingBottom: "24px",
      paddingTop: "28px",
      boxSizing: "border-box"
    }}>
      <style>{`
        :global(html, body) {
          margin: 0;
          padding: 0;
          height: 100vh;
          overflow: hidden;
        }

        :global(body) {
          position: relative;
          background: #1a1a1a;
          color: #fff;
          font-family: 'm6x11plus', monospace;
        }

        :global(:root) {
          --balatro-red: #ff4c40;
          --balatro-dark-red: #a02721;
          --balatro-blue: #0093ff;
          --balatro-dark-blue: #0057a1;
          --balatro-green: #429f79;
          --balatro-dark-green: #215f46;
          --balatro-purple: #9b59b6;
          --balatro-dark-purple: #5D3570;
          --balatro-gold: #eaba44;
          --balatro-dark-gold: #b89435;
          --balatro-orange: #ff9800;
          --balatro-dark-orange: #cc7700;
        }

        .main-layout {
          display: flex;
          position: relative;
          padding: 0;
          boxSizing: "border-box";
          height: "calc(100vh - 24px - 28px)";
          maxHeight: "calc(100vh - 24px - 28px)";
          overflow: "visible";
          margin: 0;
          marginTop: "28px";
        }

        .jaml-badge {
          position: absolute;
          top: 0px;
          left: 50%;
          transform: translateX(-50%);
          display: flex;
          align-items: center;
          gap: 8px;
          padding: 4px 12px;
          background: var(--balatro-gold);
          borderRadius: 4px;
          boxShadow: 0 2px 8px rgba(0,0,0,0.3);
          zIndex: 1000;
          cursor: pointer;
        }

        .jaml-badge button {
          background: none;
          border: none;
          cursor: pointer;
          font-size: 14px;
          padding: 2px;
          opacity: 0.8;
          color: #000;
        }

        .jaml-badge button:hover {
          opacity: 1;
        }

        .jaml-badge .logo {
          font-weight: bold;
          font-size: 14px;
        }

        .split-divider {
          position: absolute;
          left: 50%;
          top: 28px;
          bottom: 0;
          width: 4px;
          background: var(--balatro-gold);
          cursor: col-resize;
          z-index: 100;
        }

        .split-column {
          height: 100%;
          display: flex;
          flex-direction: column;
          gap: 8px;
          padding: 8px;
        }

        .split-left {
          width: 50%;
        }

        .split-right {
          width: 50%;
        }

        .panel-section {
          flex: 1;
          position: relative;
          width: 100%;
          boxSizing: border-box;
          background: #2a2a2a;
          borderTop: 10px solid var(--panel-color);
          borderLeft: 4px solid var(--panel-color);
          borderRight: 4px solid var(--panel-color);
          borderBottom: 4px solid var(--panel-color);
          borderRadius: 0;
          overflow: visible;
          display: flex;
          flexDirection: column;
          minHeight: 0;
          maxHeight: 100vh;
        }

        .panel-section-red {
          --panel-color: var(--balatro-red);
        }

        .panel-section-purple {
          --panel-color: var(--balatro-purple);
        }

        .panel-content {
          flex: 1;
          overflow: auto;
          minHeight: 0;
          padding: 12px;
        }

        .jaml-editor {
          width: 100%;
          height: 100%;
          background: #1e1e1e;
          color: #d4d4d4;
          border: 1px solid #333;
          fontFamily: "monospace";
          fontSize: 14px;
          resize: none;
        }

        .button-group {
          display: flex;
          gap: 8px;
          marginTop: 8px;
        }

        .btn {
          padding: 4px 8px;
          border: none;
          borderRadius: 4px;
          cursor: pointer;
          fontSize: 12px;
          color: white;
        }

        .btn-search {
          background: var(--balatro-green);
        }

        .btn-save {
          background: var(--balatro-blue);
        }

        .btn-stop {
          background: var(--balatro-red);
        }

        .btn-export {
          background: var(--balatro-purple);
        }

        .results-table {
          width: 100%;
          border-collapse: collapse;
          color: #fff;
        }

        .results-table th,
        .results-table td {
          padding: 8px;
          border: 1px solid #333;
          text-align: left;
        }

        .results-table th {
          background: #444;
          font-weight: bold;
        }

        .results-empty {
          color: #888;
          textAlign: center;
          padding: 20px;
        }

        .modal {
          position: fixed;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: rgba(0, 0, 0, 0.8);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 10000;
        }

        .modal-content {
          background: #2a2a2a;
          border: 2px solid var(--balatro-gold);
          border-radius: 8px;
          max-width: 600px;
          max-height: 80vh;
          overflow: hidden;
        }

        .modal-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 16px;
          background: #1a1a1a;
          border-bottom: 1px solid #333;
        }

        .modal-title {
          margin: 0;
          color: #fff;
        }

        .modal-close {
          background: none;
          border: none;
          color: #fff;
          font-size: 20px;
          cursor: pointer;
          padding: 0;
          width: 24px;
          height: 24px;
        }

        .modal-body {
          padding: 16px;
          max-height: 70vh;
          overflow-y: auto;
        }

        .settings-section {
          margin-bottom: 24px;
        }

        .settings-section h4 {
          margin-bottom: 12px;
          font-size: 16px;
          color: #fff;
        }

        .filter-list {
          display: flex;
          flex-direction: column;
          gap: 8px;
        }

        .filter-item {
          display: flex;
          justify-content: space-between;
          align-items: center;
          padding: 8px 12px;
          background: #1a1a1a;
          border: 1px solid #333;
          border-radius: 4px;
        }

        .filter-select {
          flex: 1;
          text-align: left;
          background: none;
          border: none;
          color: #fff;
          cursor: pointer;
          display: flex;
          flex-direction: column;
          gap: 2px;
        }

        .filter-select:hover .filter-name {
          color: var(--balatro-gold);
        }

        .filter-name {
          font-weight: normal;
        }

        .filter-meta {
          opacity: 0.6;
          font-size: 0.8rem;
        }

        .btn-danger {
          background: var(--balatro-red);
        }

        .btn-sm {
          padding: 2px 6px;
          font-size: 11px;
        }

        .search-status {
          color: #888;
          fontSize: 12px;
          marginTop: 8px;
        }
      `}</style>
      
      <div className="main-layout">
        <div className="split-column split-left" style={{ width: `${splitWidth}%` }}>
          <div className="panel-section panel-section-red">
            <div className="panel-content">
              <textarea
                className="jaml-editor"
                value={jamlContent}
                onChange={(e) => setJamlContent(e.target.value)}
                placeholder="Enter JAML filter content..."
              />
              <div className="button-group">
                <button 
                  className="btn btn-search" 
                  onClick={handleSearch}
                  disabled={isSearching}
                >
                  {isSearching ? 'Searching...' : 'Search'}
                </button>
                <button className="btn btn-save" onClick={handleSave}>Save Filter</button>
                {isSearching && (
                  <button className="btn btn-stop" onClick={stopAll}>Stop</button>
                )}
              </div>
              <div className="search-status">{searchStatus}</div>
            </div>
          </div>
        </div>
        
        <div 
          className="split-divider"
          ref={dividerRef}
          style={{ left: `${splitWidth}%` }}
          onMouseDown={handleMouseDown}
        />
        
        <div className="jaml-badge">
          <button onClick={handleHome}>🏠</button>
          <span className="logo">JAML</span>
          <button onClick={handleReset}>↻</button>
          <button onClick={() => setShowSettings(true)}>⚙️</button>
        </div>
        
        <div className="split-column split-right" style={{ width: `${100 - splitWidth}%` }}>
          <div className="panel-section panel-section-purple">
            <div className="panel-content">
              {results.length > 0 ? (
                <>
                  <table className="results-table">
                    <thead>
                      <tr>
                        <th>Seed</th>
                        <th>Score</th>
                      </tr>
                    </thead>
                    <tbody>
                      {results.map((result, index) => (
                        <tr key={index}>
                          <td>{result.seed}</td>
                          <td>{result.score?.toFixed(2)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div className="button-group">
                    <button className="btn btn-export" onClick={exportResults}>Export CSV</button>
                  </div>
                </>
              ) : (
                <div className="results-empty">
                  {isSearching ? 'Searching for seeds...' : 'No results yet'}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
      
      {showSettings && (
        <div className="modal" onClick={() => setShowSettings(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3 className="modal-title">Settings</h3>
              <button onClick={() => setShowSettings(false)} className="modal-close">×</button>
            </div>
            <div className="modal-body">
              <div className="settings-section">
                <h4>Filters</h4>
                <div className="filter-list">
                  {filters.map((filter) => (
                    <div key={filter.id} className="filter-item">
                      <button
                        className="filter-select"
                        onClick={() => handleSelectFilter(filter)}
                      >
                        <span className="filter-name">{filter.name}</span>
                        <small className="filter-meta">{filter.author || 'Unknown'}</small>
                      </button>
                      <button
                        onClick={() => handleDeleteFilter(filter)}
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
      )}
    </div>
  )
}
