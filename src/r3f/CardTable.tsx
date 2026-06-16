"use client";

import * as React from "react";
import { Canvas, useFrame, useThree, type ThreeEvent } from "@react-three/fiber";
import * as THREE from "three";

import {
  useSpriteTexture,
  editionMaterial,
  updateEditionEmissive,
  CardLighting,
  CARD_W,
  CARD_H,
  MAX_TILT,
  type CardEdition,
} from "./Card3D.js";
import { type SpriteSheetType } from "../sprites/spriteMapper.js";

// Frame-rate independent ease: how fast a value chases its target each frame.
const ease = (delta: number, rate = 10) => 1 - Math.exp(-rate * delta);
// How far the card lifts toward the camera while held (world units).
const LIFT_Z = 0.6;

interface DraggableCardProps {
  itemName: string;
  fallbackSheet: SpriteSheetType;
  edition: CardEdition;
  homeX: number;
  dragging: boolean;
  onPick: () => void;
  onRelease: () => void;
}

/**
 * One Balatro card on the felt. At rest it sits in its slot and leans toward the
 * pointer on hover; picked up, it lifts toward the camera and chases the pointer
 * in world space, dropping back to its slot on release. Catches the light per
 * edition. All the motion is on the GPU per-frame — the reason for r3f.
 */
function DraggableCard({
  itemName,
  fallbackSheet,
  edition,
  homeX,
  dragging,
  onPick,
  onRelease,
}: DraggableCardProps) {
  const texture = useSpriteTexture(itemName, fallbackSheet);
  const meshRef = React.useRef<THREE.Mesh>(null);
  const matRef = React.useRef<THREE.MeshStandardMaterial>(null);
  const [hovered, setHovered] = React.useState(false);
  const { camera } = useThree();
  const em = React.useMemo(() => editionMaterial(edition), [edition]);

  // Scratch vector reused every frame so the drag loop allocates nothing.
  const scratch = React.useMemo(() => new THREE.Vector3(), []);

  useFrame((state, delta) => {
    const mesh = meshRef.current;
    if (!mesh) return;
    const k = ease(delta);

    if (dragging) {
      // Unproject the pointer (NDC) onto the lifted z-plane and chase it.
      scratch.set(state.pointer.x, state.pointer.y, 0.5).unproject(camera);
      scratch.sub(camera.position).normalize();
      const dist = (LIFT_Z - camera.position.z) / scratch.z;
      const px = camera.position.x + scratch.x * dist;
      const py = camera.position.y + scratch.y * dist;
      mesh.position.x += (px - mesh.position.x) * k;
      mesh.position.y += (py - mesh.position.y) * k;
      mesh.position.z += (LIFT_Z - mesh.position.z) * k;
      tiltTo(mesh, state.pointer.x * MAX_TILT, -state.pointer.y * MAX_TILT, k);
      scaleTo(mesh, 1.18, k);
    } else {
      // Settle back into the slot; lean toward the pointer only while hovered.
      mesh.position.x += (homeX - mesh.position.x) * k;
      mesh.position.y += (0 - mesh.position.y) * k;
      mesh.position.z += (0 - mesh.position.z) * k;
      const ty = hovered ? state.pointer.x * MAX_TILT : 0;
      const tx = hovered ? -state.pointer.y * MAX_TILT : 0;
      tiltTo(mesh, ty, tx, k);
      scaleTo(mesh, hovered ? 1.12 : 1, k);
    }
    updateEditionEmissive(matRef.current, edition, state.clock.elapsedTime, mesh.rotation.y);
  });

  return (
    <mesh
      ref={meshRef}
      position={[homeX, 0, 0]}
      onPointerOver={(e: ThreeEvent<PointerEvent>) => {
        e.stopPropagation();
        setHovered(true);
      }}
      onPointerOut={() => setHovered(false)}
      onPointerDown={(e: ThreeEvent<PointerEvent>) => {
        e.stopPropagation();
        onPick();
      }}
      onPointerUp={() => onRelease()}
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
    </mesh>
  );
}

function tiltTo(mesh: THREE.Mesh, targetY: number, targetX: number, k: number) {
  mesh.rotation.y += (targetY - mesh.rotation.y) * k;
  mesh.rotation.x += (targetX - mesh.rotation.x) * k;
}

function scaleTo(mesh: THREE.Mesh, target: number, k: number) {
  mesh.scale.x += (target - mesh.scale.x) * k;
  mesh.scale.y += (target - mesh.scale.y) * k;
  mesh.scale.z += (target - mesh.scale.z) * k;
}

export interface CardTableItem {
  /** Item name — e.g. "Blueprint". Resolved against jaml-ui sprite metadata. */
  itemName: string;
  /** Sheet to fall back to when the name doesn't resolve. Default "Jokers". */
  fallbackSheet?: SpriteSheetType;
  /** Finish — "base" | "foil" | "holo" | "polychrome". Default "base". */
  edition?: CardEdition;
}

export interface CardTableProps {
  /** The cards to lay out in a row, left to right. */
  items: CardTableItem[];
  /** Pixel height of the canvas. Default 320. */
  height?: number | string;
  /** World-space distance between card centers. Default 1.25. */
  gap?: number;
  className?: string;
  style?: React.CSSProperties;
}

/**
 * A row of floating, grabbable 3D Balatro cards in a single Canvas — the shop,
 * not a swatch. Hover to lean a card toward the pointer; press to lift it off
 * the felt and drag it; release to drop it back into its slot. Foil/holo cards
 * catch and throw the light as they move.
 *
 * ```tsx
 * import { CardTable } from "jaml-ui/r3f";
 * <CardTable items={[{ itemName: "Blueprint", edition: "holo" }]} />
 * ```
 *
 * Peer deps: `three`, `@react-three/fiber`, `@react-three/drei`.
 */
export function CardTable({
  items,
  height = 320,
  gap = 1.25,
  className,
  style,
}: CardTableProps) {
  const [dragging, setDragging] = React.useState<number | null>(null);
  const release = React.useCallback(() => setDragging(null), []);
  const n = items.length;

  return (
    <div className={className} style={{ width: "100%", height, ...style }}>
      <Canvas
        camera={{ position: [0, 0, 3], fov: 40 }}
        gl={{ alpha: true }}
        dpr={[1, 2]}
        onPointerMissed={release}
      >
        <CardLighting />
        <React.Suspense fallback={null}>
          {items.map((item, i) => (
            <DraggableCard
              key={`${item.itemName}-${i}`}
              itemName={item.itemName}
              fallbackSheet={item.fallbackSheet ?? "Jokers"}
              edition={item.edition ?? "base"}
              homeX={(i - (n - 1) / 2) * gap}
              dragging={dragging === i}
              onPick={() => setDragging(i)}
              onRelease={release}
            />
          ))}
        </React.Suspense>
      </Canvas>
    </div>
  );
}
