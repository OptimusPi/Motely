import React, { useRef, useEffect } from "react"
import { Box, Card, Text } from "@mantine/core"
import { JamlHeader } from "./JamlHeader"
import { JamlEditor } from "./JamlEditor"
import { ResultsPanel } from "./ResultsPanel"
import { SettingsModal } from "./SettingsModal"
import { useJamlStore } from "../store/JamlStore"

export function JamlLayout() {
  const { panelState, uiState, setSplitWidth } = useJamlStore()
  const dividerRef = useRef<HTMLDivElement>(null)
  const isDragging = useRef(false)
  
  const handleMouseDown = (e: React.MouseEvent) => {
    isDragging.current = true
    e.preventDefault()
  }
  
  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDragging.current || !dividerRef.current) return
      
      const container = dividerRef.current.parentElement
      if (!container) return
      
      const rect = container.getBoundingClientRect()
      const percent = ((e.clientX - rect.left) / rect.width) * 100
      const clampedPercent = Math.max(10, Math.min(90, percent))
      
      setSplitWidth(clampedPercent)
    }
    
    const handleMouseUp = () => {
      isDragging.current = false
    }
    
    if (isDragging.current) {
      document.addEventListener("mousemove", handleMouseMove)
      document.addEventListener("mouseup", handleMouseUp)
      
      return () => {
        document.removeEventListener("mousemove", handleMouseMove)
        document.removeEventListener("mouseup", handleMouseUp)
      }
    }
  }, [setSplitWidth])
  
  const leftPanels = panelState.panels.filter(p => p.side === "left")
  const rightPanels = panelState.panels.filter(p => p.side === "right")
  
  return (
    <Box style={{ height: "100vh", position: "relative", background: "#1a1a1a" }}>
      <JamlHeader />
      
      <Box
        style={{
          height: "100%",
          paddingTop: "40px",
          display: "flex",
          position: "relative"
        }}
      >
        {/* Left Column */}
        <Box
          style={{
            width: `${panelState.splitWidth}%`,
            display: "flex",
            flexDirection: "column",
            gap: "8px",
            padding: "8px"
          }}
        >
          {leftPanels.map(panel => (
            <Card
              key={panel.id}
              style={{
                flex: panel.collapsed ? "0 0 auto" : "1 1 auto",
                minHeight: panel.collapsed ? "auto" : panel.defaultHeight,
                background: panel.color === "red" ? "#ff4c40" : "#429f79",
                border: `2px solid ${panel.color === "red" ? "#a02721" : "#215f46"}`,
                borderRadius: "0 0 4px 4px",
                overflow: "hidden",
                position: "relative"
              }}
            >
              <Box
                style={{
                  height: "28px",
                  background: panel.color === "red" ? "#a02721" : "#215f46",
                  display: "flex",
                  alignItems: "center",
                  padding: "0 12px",
                  color: "white",
                  fontWeight: "bold",
                  fontSize: "14px",
                  fontFamily: "monospace"
                }}
              >
                {panel.label}
              </Box>
              
              {!panel.collapsed && (
                <Box style={{ padding: "12px", background: "#2a2a2a", height: "calc(100% - 28px)" }}>
                  {panel.id === "jaml-editor" && <JamlEditor />}
                  {panel.id === "results" && <ResultsPanel />}
                </Box>
              )}
            </Card>
          ))}
        </Box>
        
        {/* Divider */}
        <Box
          ref={dividerRef}
          style={{
            position: "absolute",
            left: `${panelState.splitWidth}%`,
            top: "40px",
            bottom: 0,
            width: "4px",
            background: "#eaba44",
            cursor: "col-resize",
            zIndex: 100
          }}
          onMouseDown={handleMouseDown}
        />
        
        {/* Right Column */}
        <Box
          style={{
            width: `${100 - panelState.splitWidth}%`,
            display: "flex",
            flexDirection: "column",
            gap: "8px",
            padding: "8px"
          }}
        >
          {rightPanels.map(panel => (
            <Card
              key={panel.id}
              style={{
                flex: panel.collapsed ? "0 0 auto" : "1 1 auto",
                minHeight: panel.collapsed ? "auto" : panel.defaultHeight,
                background: panel.color === "purple" ? "#9b59b6" : "#0093ff",
                border: `2px solid ${panel.color === "purple" ? "#5D3570" : "#0057a1"}`,
                borderRadius: "0 0 4px 4px",
                overflow: "hidden",
                position: "relative"
              }}
            >
              <Box
                style={{
                  height: "28px",
                  background: panel.color === "purple" ? "#5D3570" : "#0057a1",
                  display: "flex",
                  alignItems: "center",
                  padding: "0 12px",
                  color: "white",
                  fontWeight: "bold",
                  fontSize: "14px",
                  fontFamily: "monospace"
                }}
              >
                {panel.label}
              </Box>
              
              {!panel.collapsed && (
                <Box style={{ padding: "12px", background: "#2a2a2a", height: "calc(100% - 28px)" }}>
                  {panel.id === "jaml-editor" && <JamlEditor />}
                  {panel.id === "results" && <ResultsPanel />}
                </Box>
              )}
            </Card>
          ))}
        </Box>
      </Box>
      
      <SettingsModal opened={uiState.settingsOpen} />
    </Box>
  )
}
