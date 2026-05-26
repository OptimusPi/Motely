import React from 'react'
import { Text } from '@react-three/drei'
import { resolveJamlAssetUrl } from '../assets.js'
import { JimboColorOption } from '../ui/tokens.js'

export interface JimboText3DProps {
  children: string
  color?: string
  outlineColor?: string
  outlineWidth?: number
  position?: [number, number, number]
  fontSize?: number
}

export function JimboText3D({
  children,
  color = JimboColorOption.WHITE,
  outlineColor = JimboColorOption.BLACK,
  outlineWidth = 0.05,
  position = [0, 0, 0],
  fontSize = 1
}: JimboText3DProps) {
  // We use the m6x11plus font from assets if possible, or fallback
  return (
    <Text
      position={position}
      fontSize={fontSize}
      color={color}
      outlineColor={outlineColor}
      outlineWidth={outlineWidth}
      font={resolveJamlAssetUrl('font')}
      anchorX="center"
      anchorY="middle"
    >
      {children}
    </Text>
  )
}
