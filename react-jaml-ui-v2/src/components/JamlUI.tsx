import React, { useState, useRef, useEffect } from 'react'

export function JamlUI() {
  const [jamlContent, setJamlContent] = useState('')
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
      document.addEventListener('mousemove', handleMouseMove)
      document.addEventListener('mouseup', handleMouseUp)
      
      return () => {
        document.removeEventListener('mousemove', handleMouseMove)
        document.removeEventListener('mouseup', handleMouseUp)
      }
    }
  }, [isDragging])
  
  return (
    <div className="jaml-ui">
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

        .jaml-ui {
          height: 100vh;
          min-height: 100vh;
          overflow: visible;
          background: rgba(0, 0, 0, 0.4);
          position: relative;
          border-left: 1px solid rgba(255, 255, 255, 0.08);
          border-right: 1px solid rgba(255, 255, 255, 0.08);
          box-shadow: inset 0 0 40px rgba(0, 0, 0, 0.6), 0 30px 60px rgba(0, 0, 0, 0.45);
          padding-bottom: 24px;
          padding-top: 28px;
          box-sizing: border-box;
        }

        .main-layout {
          display: flex;
          position: relative;
          padding: 0;
          box-sizing: border-box;
          height: calc(100vh - 24px - 28px);
          max-height: calc(100vh - 24px - 28px);
          overflow: visible;
          margin: 0;
          margin-top: 28px;
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
          border-radius: 4px;
          box-shadow: 0 2px 8px rgba(0,0,0,0.3);
          z-index: 1000;
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

        .panel-section {
          flex: 1;
          position: relative;
          width: 100%;
          box-sizing: border-box;
          background: #2a2a2a;
          border-top: 10px solid var(--panel-color);
          border-left: 4px solid var(--panel-color);
          border-right: 4px solid var(--panel-color);
          border-bottom: 4px solid var(--panel-color);
          border-radius: 0;
          overflow: visible;
          display: flex;
          flex-direction: column;
          min-height: 0;
          max-height: 100vh;
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
          min-height: 0;
          padding: 12px;
        }

        .jaml-editor {
          width: 100%;
          height: 100%;
          background: #1e1e1e;
          color: #d4d4d4;
          border: 1px solid #333;
          font-family: monospace;
          font-size: 14px;
          resize: none;
        }

        .button-group {
          display: flex;
          gap: 8px;
          margin-top: 8px;
        }

        .btn {
          padding: 4px 8px;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-size: 12px;
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
          text-align: center;
          padding: 20px;
        }
      `}</style>
      
      <div className="main-layout">
        <div className="split-column" style={{ width: `${splitWidth}%` }}>
          <div className="panel-section panel-section-red">
            <div className="panel-content">
              <textarea
                className="jaml-editor"
                value={jamlContent}
                onChange={(e) => setJamlContent(e.target.value)}
                placeholder="Enter JAML filter content..."
              />
              <div className="button-group">
                <button className="btn btn-search">Search</button>
                <button className="btn btn-save">Save Filter</button>
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
          <button>🏠</button>
          <span className="logo">JAML</span>
          <button>↻</button>
          <button>⚙️</button>
        </div>
        
        <div className="split-column" style={{ width: `${100 - splitWidth}%` }}>
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
