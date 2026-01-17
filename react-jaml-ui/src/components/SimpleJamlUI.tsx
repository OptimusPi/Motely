import React, { useState, useRef, useEffect } from "react"
import { Box, Text } from "@mantine/core"

export function SimpleJamlUI() {
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
  
  return (
    <div style={{ height: "100vh", background: "#1a1a1a", position: "relative", fontFamily: "monospace" }}>
      {/* JAML Header */}
      <div style={{
        position: "absolute",
        top: 0,
        left: "50%",
        transform: "translateX(-50%)",
        zIndex: 1000,
        display: "flex",
        alignItems: "center",
        gap: "8px",
        padding: "4px 12px",
        background: "#eaba44",
        borderRadius: "4px",
        boxShadow: "0 2px 8px rgba(0,0,0,0.3)"
      }}>
        <button style={{ background: "none", border: "none", cursor: "pointer" }}>🏠</button>
        <span style={{ fontWeight: "bold", fontSize: "12px" }}>JAML</span>
        <button style={{ background: "none", border: "none", cursor: "pointer" }}>⚙️</button>
      </div>
      
      {/* Main Layout */}
      <div style={{ height: "100%", paddingTop: "40px", display: "flex" }}>
        {/* Left Panel */}
        <div style={{ width: `${splitWidth}%`, display: "flex", flexDirection: "column", padding: "8px" }}>
          <div style={{
            flex: 1,
            background: "#ff4c40",
            border: "2px solid #a02721",
            borderRadius: "0 0 4px 4px",
            overflow: "hidden",
            position: "relative"
          }}>
            <div style={{
              height: "28px",
              background: "#a02721",
              display: "flex",
              alignItems: "center",
              padding: "0 12px",
              color: "white",
              fontWeight: "bold",
              fontSize: "14px"
            }}>
              JAML Editor
            </div>
            <div style={{ padding: "12px", background: "#2a2a2a", height: "calc(100% - 28px)" }}>
              <textarea
                value={jamlContent}
                onChange={(e) => setJamlContent(e.target.value)}
                placeholder="Enter JAML filter content..."
                style={{
                  width: "100%",
                  height: "100%",
                  background: "#1e1e1e",
                  color: "#d4d4d4",
                  border: "1px solid #333",
                  fontFamily: "monospace",
                  fontSize: "14px",
                  resize: "none"
                }}
              />
              <div style={{ display: "flex", gap: "8px", marginTop: "8px" }}>
                <button style={{
                  background: "#429f79",
                  color: "white",
                  border: "none",
                  padding: "4px 8px",
                  borderRadius: "4px",
                  cursor: "pointer",
                  fontSize: "12px"
                }}>
                  Search
                </button>
                <button style={{
                  background: "#0093ff",
                  color: "white",
                  border: "none",
                  padding: "4px 8px",
                  borderRadius: "4px",
                  cursor: "pointer",
                  fontSize: "12px"
                }}>
                  Save Filter
                </button>
              </div>
            </div>
          </div>
        </div>
        
        {/* Divider */}
        <div
          ref={dividerRef}
          style={{
            position: "absolute",
            left: `${splitWidth}%`,
            top: "40px",
            bottom: 0,
            width: "4px",
            background: "#eaba44",
            cursor: "col-resize",
            zIndex: 100
          }}
          onMouseDown={handleMouseDown}
        />
        
        {/* Right Panel */}
        <div style={{ width: `${100 - splitWidth}%`, display: "flex", flexDirection: "column", padding: "8px" }}>
          <div style={{
            flex: 1,
            background: "#9b59b6",
            border: "2px solid #5D3570",
            borderRadius: "0 0 4px 4px",
            overflow: "hidden",
            position: "relative"
          }}>
            <div style={{
              height: "28px",
              background: "#5D3570",
              display: "flex",
              alignItems: "center",
              padding: "0 12px",
              color: "white",
              fontWeight: "bold",
              fontSize: "14px"
            }}>
              Search Results
            </div>
            <div style={{ padding: "12px", background: "#2a2a2a", height: "calc(100% - 28px)" }}>
              <div style={{ color: "#888", textAlign: "center", padding: "20px" }}>
                No results yet
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  )
}
