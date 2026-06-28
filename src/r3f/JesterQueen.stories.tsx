import type { Meta, StoryObj } from "@storybook/react";
import { Canvas } from "@react-three/fiber";
import {
  Float,
  Environment,
  ContactShadows,
  RoundedBox,
  MeshTransmissionMaterial,
  Text,
  OrbitControls,
  Sparkles,
  Lightformer,
} from "@react-three/drei";
import { useRef } from "react";
import { useFrame } from "@react-three/fiber";
import type { Mesh, Group } from "three";

const PLUM = "#5b2a86";
const MAGENTA = "#ff3fa4";
const GOLD = "#ffd166";

/** A single emissive suit pip floating just above the card face. */
function SuitGem({ glyph, position, color }: { glyph: string; position: [number, number, number]; color: string }) {
  return (
    <group position={position}>
      <Text fontSize={0.42} anchorX="center" anchorY="middle" outlineWidth={0.012} outlineColor="#1a0a2e">
        {glyph}
        <meshStandardMaterial color={color} emissive={color} emissiveIntensity={1.6} toneMapped={false} />
      </Text>
    </group>
  );
}

/** The card itself: a glass-velvet collectible that drifts and catches the rim light. */
function JesterCard() {
  const grin = useRef<Mesh>(null!);
  useFrame((state) => {
    // a sly, slow knowing tilt — not a kindergarten spin
    if (grin.current) grin.current.rotation.z = Math.sin(state.clock.elapsedTime * 0.6) * 0.04;
  });

  return (
    <Float speed={1.4} rotationIntensity={0.5} floatIntensity={0.9} floatingRange={[-0.08, 0.12]}>
      {/* Card body — beveled, slightly translucent plum velvet glass */}
      <RoundedBox args={[2.5, 3.5, 0.16]} radius={0.18} smoothness={8} castShadow receiveShadow>
        <MeshTransmissionMaterial
          thickness={0.6}
          roughness={0.32}
          transmission={0.55}
          ior={1.4}
          chromaticAberration={0.12}
          color={PLUM}
          background={undefined}
          attenuationColor={MAGENTA}
          attenuationDistance={1.2}
        />
      </RoundedBox>

      {/* Ornate gold inner border */}
      <RoundedBox args={[2.18, 3.18, 0.02]} radius={0.13} smoothness={6} position={[0, 0, 0.085]}>
        <meshStandardMaterial color={GOLD} emissive={GOLD} emissiveIntensity={0.25} metalness={0.9} roughness={0.25} />
      </RoundedBox>

      {/* Title */}
      <group ref={grin} position={[0, 0.05, 0.1]}>
        <Text position={[0, 1.18, 0]} fontSize={0.26} letterSpacing={0.04} anchorX="center" outlineWidth={0.01} outlineColor="#1a0a2e">
          THE JESTER QUEEN
          <meshStandardMaterial color={GOLD} emissive={GOLD} emissiveIntensity={0.8} toneMapped={false} />
        </Text>

        {/* The grin */}
        <Text position={[0, 0.05, 0]} fontSize={1.5} anchorX="center" anchorY="middle">
          😏
        </Text>

        {/* Crown / jester hat hint */}
        <Text position={[0, 0.95, 0]} fontSize={0.5} anchorX="center" anchorY="middle">
          👑
        </Text>

        {/* Four suits across the bottom, glowing */}
        <SuitGem glyph="♦" position={[-0.75, -1.15, 0]} color={MAGENTA} />
        <SuitGem glyph="♣" position={[-0.25, -1.15, 0]} color={GOLD} />
        <SuitGem glyph="♥" position={[0.25, -1.15, 0]} color={MAGENTA} />
        <SuitGem glyph="♠" position={[0.75, -1.15, 0]} color={GOLD} />
      </group>

      {/* glitter around her, because of course */}
      <Sparkles count={40} scale={[3.5, 4.5, 1.5]} size={3} speed={0.4} color={GOLD} />
    </Float>
  );
}

function Scene() {
  const rig = useRef<Group>(null!);
  return (
    <>
      <color attach="background" args={["#140a24"]} />
      <fog attach="fog" args={["#140a24", 6, 14]} />

      <group ref={rig}>
        <JesterCard />
      </group>

      {/* three-point stage light: warm key, magenta rim, cool fill */}
      <spotLight position={[4, 6, 5]} angle={0.5} penumbra={0.8} intensity={120} color={GOLD} castShadow />
      <spotLight position={[-5, 2, -3]} angle={0.7} penumbra={1} intensity={90} color={MAGENTA} />
      <ambientLight intensity={0.15} />

      <ContactShadows position={[0, -2.4, 0]} opacity={0.55} scale={10} blur={2.6} far={4} color="#2a0a3e" />

      {/* drei studio env with custom lightformers for the velvet sheen */}
      <Environment resolution={256}>
        <Lightformer form="rect" intensity={2} position={[0, 3, 4]} scale={[6, 2, 1]} color={MAGENTA} />
        <Lightformer form="rect" intensity={1.2} position={[-4, 1, 2]} scale={[3, 4, 1]} color={PLUM} />
        <Lightformer form="ring" intensity={1.5} position={[3, 2, -2]} scale={2} color={GOLD} />
      </Environment>

      <OrbitControls enablePan={false} minPolarAngle={Math.PI / 3} maxPolarAngle={Math.PI / 1.8} minDistance={4} maxDistance={9} />
    </>
  );
}

const meta: Meta = {
  title: "r3f/JesterQueen",
  parameters: { layout: "fullscreen" },
};
export default meta;

export const FemmeJimbo: StoryObj = {
  render: () => (
    <div style={{ width: "100vw", height: "100vh", background: "#140a24" }}>
      <Canvas shadows camera={{ position: [0, 0.3, 6], fov: 38 }} dpr={[1, 2]}>
        <Scene />
      </Canvas>
    </div>
  ),
};
