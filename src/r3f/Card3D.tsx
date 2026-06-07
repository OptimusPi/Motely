"use client";

import * as React from "react";
import { Canvas, useLoader, useFrame } from "@react-three/fiber";
import { Float } from "@react-three/drei";
import { useSpring, animated } from "@react-spring/three";
import * as THREE from "three";

import { resolveJamlAssetUrl } from "../assets.js";
import { getSpriteDataOrMystery, SHEET_META, type SpriteSheetType } from "../sprites/spriteMapper.js";
import { SPRITE_SHEETS, JOKER_FACES, type SpritePos } from "../sprites/spriteData.js";

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

/** Balatro-style finishes. The card catches light differently per edition. */
export type CardEdition = "base" | "foil" | "holo" | "polychrome";

export interface EditionMaterial {
  roughness: number;
  metalness: number;
  emissiveIntensity: number;
  /** Whether the emissive hue cycles per-frame (the holographic shimmer). */
  animated: boolean;
}

/**
 * Material tuning per edition. Metalness stays 0 — without an env map, metal goes
 * black — so "shine" comes from a low-roughness specular highlight that rakes
 * across as the card tilts under the light. Holo/polychrome add a cycling
 * emissive hue on top for the rainbow shimmer.
 */
export function editionMaterial(edition: CardEdition): EditionMaterial {
  switch (edition) {
    case "foil":
      return { roughness: 0.18, metalness: 0, emissiveIntensity: 0, animated: false };
    case "holo":
      return { roughness: 0.26, metalness: 0, emissiveIntensity: 0.35, animated: true };
    case "polychrome":
      return { roughness: 0.16, metalness: 0, emissiveIntensity: 0.55, animated: true };
    case "base":
    default:
      return { roughness: 1, metalness: 0, emissiveIntensity: 0, animated: false };
  }
}

/** Drive the holographic emissive hue from time + tilt angle. No-op for base/foil. */
export function updateEditionEmissive(
  material: THREE.MeshStandardMaterial | null,
  edition: CardEdition,
  t: number,
  tiltY: number,
) {
  if (!material || (edition !== "holo" && edition !== "polychrome")) return;
  const speed = edition === "polychrome" ? 0.5 : 0.22;
  const hue = (((t * speed + tiltY * 0.6) % 1) + 1) % 1;
  material.emissive.setHSL(hue, 0.85, 0.5);
}

/** Crop the jokers sheet to an explicit grid cell (used for legendary soul faces). */
function useJokersCellTexture(pos: SpritePos): THREE.Texture {
  const sheet = SPRITE_SHEETS.jokers;
  const base = useLoader(THREE.TextureLoader, sheet.src);
  return React.useMemo(() => {
    const t = base.clone();
    t.magFilter = THREE.NearestFilter;
    t.minFilter = THREE.NearestFilter;
    t.colorSpace = THREE.SRGBColorSpace;
    t.repeat.set(1 / sheet.columns, 1 / sheet.rows);
    t.offset.set(pos.x / sheet.columns, 1 - (pos.y + 1) / sheet.rows);
    t.needsUpdate = true;
    return t;
  }, [base, sheet, pos.x, pos.y]);
}

/**
 * The legendary's soul — its glowing face — hovering on its own depth plane just
 * in front of the card. Bobs independently, so under tilt it parallaxes off the
 * base: the thing DOM compositing can never do, only real depth.
 */
function SoulMesh({ pos }: { pos: SpritePos }) {
  const texture = useJokersCellTexture(pos);
  const ref = React.useRef<THREE.Mesh>(null);
  useFrame((state) => {
    if (ref.current) ref.current.position.y = 0.04 * Math.sin(state.clock.elapsedTime * 2);
  });
  return (
    <mesh ref={ref} position={[0, 0, 0.06]}>
      <planeGeometry args={[CARD_W, CARD_H]} />
      <meshBasicMaterial map={texture} transparent alphaTest={0.1} side={THREE.DoubleSide} toneMapped={false} />
    </mesh>
  );
}

interface CardMeshProps {
  itemName: string;
  fallbackSheet: SpriteSheetType;
  edition: CardEdition;
}

// Max tilt away from facing the camera, in radians (~17°).
export const MAX_TILT = 0.3;

function CardMesh({ itemName, fallbackSheet, edition }: CardMeshProps) {
  const texture = useSpriteTexture(itemName, fallbackSheet);
  const soul = React.useMemo(() => JOKER_FACES.find((j) => j.name === itemName), [itemName]);
  const meshRef = React.useRef<THREE.Mesh>(null);
  const matRef = React.useRef<THREE.MeshStandardMaterial>(null);
  const [hovered, setHovered] = React.useState(false);
  const em = React.useMemo(() => editionMaterial(edition), [edition]);
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
    updateEditionEmissive(matRef.current, edition, state.clock.elapsedTime, mesh.rotation.y);
  });

  return (
    <animated.mesh
      ref={meshRef}
      scale={spring.scale.to((s) => [s, s, s])}
      onPointerOver={() => setHovered(true)}
      onPointerOut={() => setHovered(false)}
    >
      <planeGeometry args={[CARD_W, CARD_H]} />
      <meshStandardMaterial
        ref={matRef}
        map={texture}
        transparent
        alphaTest={0.5}
        side={THREE.DoubleSide}
        toneMapped={false}
        roughness={em.roughness}
        metalness={em.metalness}
        emissive="#000000"
        emissiveIntensity={em.emissiveIntensity}
      />
      {soul && <SoulMesh pos={soul.pos} />}
    </animated.mesh>
  );
}

/** Off-axis key light + soft ambient: the specular that makes foil "catch." */
export function CardLighting() {
  return (
    <>
      <ambientLight intensity={0.85} />
      <pointLight position={[2.5, 3, 4]} intensity={1.6} />
    </>
  );
}

export interface Card3DProps {
  /** Item name to render — e.g. "Blueprint". Resolved against jaml-ui sprite metadata. */
  itemName: string;
  /** Which sheet to fall back to when the name doesn't resolve. Default "Jokers". */
  fallbackSheet?: SpriteSheetType;
  /** Finish — "base" | "foil" | "holo" | "polychrome". Default "base". */
  edition?: CardEdition;
  /** Pixel height of the canvas. Default 320. */
  height?: number | string;
  className?: string;
  style?: React.CSSProperties;
}

/**
 * A floating, hover-reactive 3D Balatro card that catches the light.
 *
 * ```tsx
 * import { Card3D } from "jaml-ui/r3f";
 * <Card3D itemName="Blueprint" edition="holo" />
 * ```
 *
 * Peer deps: `three`, `@react-three/fiber`, `@react-three/drei`, `@react-spring/three`.
 */
export function Card3D({
  itemName,
  fallbackSheet = "Jokers",
  edition = "base",
  height = 320,
  className,
  style,
}: Card3DProps) {
  return (
    <div className={className} style={{ width: "100%", height, ...style }}>
      <Canvas camera={{ position: [0, 0, 3], fov: 40 }} gl={{ alpha: true }} dpr={[1, 2]}>
        <CardLighting />
        <React.Suspense fallback={null}>
          <Float speed={2} rotationIntensity={0.6} floatIntensity={0.8}>
            <CardMesh itemName={itemName} fallbackSheet={fallbackSheet} edition={edition} />
          </Float>
        </React.Suspense>
      </Canvas>
    </div>
  );
}
