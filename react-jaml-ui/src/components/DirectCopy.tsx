import React, { useState, useRef, useEffect } from "react"

export function DirectCopy() {
  const [jamlContent, setJamlContent] = useState("")
  const [splitWidth, setSplitWidth] = useState(50)
  const [isDragging, setIsDragging] = useState(false)
  const dividerRef = useRef<HTMLDivElement>(null)
  
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
  
  const handleHome = () => {
    window.location.href = "/"
  }
  
  const handleReset = () => {
    setSplitWidth(50)
  }
  
  const toggleSettings = () => {
    console.log("Settings toggle")
  }
  
  const handleSave = () => {
    console.log("Save filter:", jamlContent)
  }
  
  const handleSearch = () => {
    console.log("Start search:", jamlContent)
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

        .results-empty {
          color: #888;
          textAlign: center;
          padding: 20px;
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
                <button className="btn btn-search" onClick={handleSearch}>Search</button>
                <button className="btn btn-save" onClick={handleSave}>Save Filter</button>
              </div>
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
          <button onClick={toggleSettings}>⚙️</button>
        </div>
        
        <div className="split-column split-right" style={{ width: `${100 - splitWidth}%` }}>
          <div className="panel-section panel-section-purple">
            <div className="panel-content">
              <div className="results-empty">No results yet</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
