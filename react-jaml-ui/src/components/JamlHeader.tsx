import React from "react"
import { Grid, Box, ActionIcon, Badge, Tooltip } from "@mantine/core"
import { IconHome, IconSettings, IconRefresh } from "@tabler/icons-react"
import { useJamlStore } from "../store/JamlStore"

export function JamlHeader() {
  const { setSettingsOpen, setSplitWidth, panelState } = useJamlStore()
  
  const handleHome = () => {
    window.location.href = "/"
  }
  
  const handleReset = () => {
    setSplitWidth(50)
  }
  
  return (
    <Box
      style={{
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
      }}
    >
      <ActionIcon size="sm" variant="transparent" onClick={handleHome}>
        <IconHome size={16} />
      </ActionIcon>
      
      <Badge 
        size="sm" 
        variant="filled"
        style={{ 
          background: "#eaba44", 
          color: "#000",
          fontWeight: "bold",
          fontSize: "12px"
        }}
      >
        JAML
      </Badge>
      
      <ActionIcon size="sm" variant="transparent" onClick={handleReset}>
        <IconRefresh size={16} />
      </ActionIcon>
      
      <ActionIcon size="sm" variant="transparent" onClick={() => setSettingsOpen(true)}>
        <IconSettings size={16} />
      </ActionIcon>
    </Box>
  )
}
