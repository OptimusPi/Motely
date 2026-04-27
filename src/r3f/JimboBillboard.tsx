import React, { useMemo } from 'react'
import { Billboard } from '@react-three/drei'
import { useLoader } from '@react-three/fiber'
import * as THREE from 'three'
import type { MotelySpriteData } from '../decode/motelySprite.js'

export interface JimboBillboardProps {
  sprite: MotelySpriteData | null
  label?: string
  width?: number
  height?: number
  yLockOnly?: boolean
  position?: [number, number, number]
}

export function JimboBillboard({
  sprite,
  label,
  width = 3.4,
  height = 4.5,
  yLockOnly = false,
  position = [0, 0, 0]
}: JimboBillboardProps) {
  if (!sprite) return null;

  // Memoize texture to avoid per-render allocation
  const texture = useLoader(THREE.TextureLoader, sprite.atlasPath);

  const clonedTexture = useMemo(() => {
    const tex = texture.clone();
    tex.magFilter = THREE.NearestFilter;
    tex.minFilter = THREE.NearestFilter;
    // Set up sprite cropping
    tex.repeat.set(1 / sprite.gridCols, 1 / sprite.gridRows);
    tex.offset.set(
      sprite.gridCol / sprite.gridCols,
      1 - ((sprite.gridRow + 1) / sprite.gridRows)
    );
    tex.needsUpdate = true;
    return tex;
  }, [texture, sprite.gridCol, sprite.gridRow, sprite.gridCols, sprite.gridRows]);

  const material = useMemo(() => {
    return new THREE.MeshBasicMaterial({
      map: clonedTexture,
      transparent: true,
      alphaTest: 0.5
    });
  }, [clonedTexture]);

  return (
    <Billboard
      lockY={yLockOnly}
      lockX={false}
      lockZ={false}
      position={position}
    >
      <mesh material={material}>
        <planeGeometry args={[width, height]} />
      </mesh>
    </Billboard>
  )
}
