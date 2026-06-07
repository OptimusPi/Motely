"use client";

import * as React from "react";
import { Canvas, useLoader, useFrame } from "@react-three/fiber";
import { Float } from "@react-three/drei";
import { useSpring, animated } from "@react-spring/three";
import * as THREE from "three";

import { resolveJamlAssetUrl } from "../assets.js";
import { getSpriteDataOrMystery, SHEET_META, type SpriteSheetType } from "../sprites/spriteMapper.js";

// Balatro cards are 71x95px cells on every sheet — keep the plane at that ratio.
export const CARD_W = 1;
export const CARD_H = CARD_W * (95 / 71);

/**
 * Load a spritesheet PNG and crop it to a single item's cell via UV repeat/offset.
 * Reuses jaml-ui/core's sprite metadata so the 3D card shows the *real* art,
 * pixel-perfect (NearestFilter), not a placeholder.
 */
export function useSpriteTexture(itemName: string, fallbackSheet: SpriteSheetType): THREE.Texture {
  const { pos, type } = getSpriteDataOrMystery(itemName, fallbackSheet);
  const meta = SHEET_META[type];
  const url = resolveJamlAssetUrl(meta.assetKey);
  const base = useLoader(THREE.TextureLoader, url);

  // Clone per instance: useLoader caches by URL, so cards sharing a sheet must
  // not mutate a shared texture's offset. Configure the clone, not the cached one.
  return React.useMemo(() => {
    const texture = base.clone();
    texture.magFilter = THREE.NearestFilter;
    texture.minFilter = THREE.NearestFilter;
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.repeat.set(1 / meta.cols, 1 / meta.rows);
    // Three's UV origin is bottom-left; sprite rows are indexed top-down.
    texture.offset.set(pos.x / meta.cols, 1 - (pos.y + 1) / meta.rows);
    texture.needsUpdate = true;
    return texture;
  }, [base, meta, pos.x, pos.y]);
}

interface CardMeshProps {
  itemName: string;
  fallbackSheet: SpriteSheetType;
}

// Max tilt away from facing the camera, in radians (~17°).
export const MAX_TILT = 0.3;

function CardMesh({ itemName, fallbackSheet }: CardMeshProps) {
  const texture = useSpriteTexture(itemName, fallbackSheet);
  const meshRef = React.useRef<THREE.Mesh>(null);
  const [hovered, setHovered] = React.useState(false);
  const spring = useSpring({
    scale: hovered ? 1.12 : 1,
    config: { tension: 260, friction: 18 },
  });

  // Magnetic tilt: ease the card's rotation toward the pointer every frame.
  // This is the reason for r3f — a GPU transform that DOM can't do smoothly.
  useFrame((state, delta) => {
    const mesh = meshRef.current;
    if (!mesh) return;
    const targetY = state.pointer.x * MAX_TILT;
    const targetX = -state.pointer.y * MAX_TILT;
    const lerp = 1 - Math.exp(-8 * delta); // frame-rate independent easing
    mesh.rotation.y += (targetY - mesh.rotation.y) * lerp;
    mesh.rotation.x += (targetX - mesh.rotation.x) * lerp;
  });

  return (
    <animated.mesh
      ref={meshRef}
      scale={spring.scale.to((s) => [s, s, s])}
      onPointerOver={() => setHovered(true)}
      onPointerOut={() => setHovered(false)}
    >
      <planeGeometry args={[CARD_W, CARD_H]} />
      <meshBasicMaterial
        map={texture}
        transparent
        alphaTest={0.5}
        side={THREE.DoubleSide}
        toneMapped={false}
      />
    </animated.mesh>
  );
}

export interface Card3DProps {
  /** Item name to render — e.g. "Blueprint". Resolved against jaml-ui sprite metadata. */
  itemName: string;
  /** Which sheet to fall back to when the name doesn't resolve. Default "Jokers". */
  fallbackSheet?: SpriteSheetType;
  /** Pixel height of the canvas. Default 320. */
  height?: number | string;
  className?: string;
  style?: React.CSSProperties;
}

/**
 * A floating, hover-reactive 3D Balatro card.
 *
 * ```tsx
 * import { Card3D } from "jaml-ui/r3f";
 * <Card3D itemName="Blueprint" />
 * ```
 *
 * Peer deps: `three`, `@react-three/fiber`, `@react-three/drei`, `@react-spring/three`.
 */
export function Card3D({
  itemName,
  fallbackSheet = "Jokers",
  height = 320,
  className,
  style,
}: Card3DProps) {
  return (
    <div className={className} style={{ width: "100%", height, ...style }}>
      <Canvas camera={{ position: [0, 0, 3], fov: 40 }} gl={{ alpha: true }} dpr={[1, 2]}>
        <ambientLight intensity={1} />
        <React.Suspense fallback={null}>
          <Float speed={2} rotationIntensity={0.6} floatIntensity={0.8}>
            <CardMesh itemName={itemName} fallbackSheet={fallbackSheet} />
          </Float>
        </React.Suspense>
      </Canvas>
    </div>
  );
}
