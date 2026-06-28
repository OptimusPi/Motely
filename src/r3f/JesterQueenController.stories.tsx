import type { Meta, StoryObj } from "@storybook/react";
import { Canvas } from "@react-three/fiber";
import { KeyboardControls, Environment, ContactShadows, RoundedBox, Text, Sparkles } from "@react-three/drei";
import { Physics, RigidBody } from "@react-three/rapier";
import Ecctrl from "ecctrl";

const PLUM = "#5b2a86";
const MAGENTA = "#ff3fa4";
const GOLD = "#ffd166";

// Ecctrl keyboard preset (pmndrs spec).
const keyboardMap = [
  { name: "forward", keys: ["ArrowUp", "KeyW"] },
  { name: "backward", keys: ["ArrowDown", "KeyS"] },
  { name: "leftward", keys: ["ArrowLeft", "KeyA"] },
  { name: "rightward", keys: ["ArrowRight", "KeyD"] },
  { name: "jump", keys: ["Space"] },
  { name: "run", keys: ["Shift"] },
];

/** The walkable Jester Queen — a capsule body crowned with her grin. Sits inside <Ecctrl>. */
function JesterModel() {
  return (
    <group>
      {/* body */}
      <mesh castShadow position={[0, 0, 0]}>
        <capsuleGeometry args={[0.3, 0.5, 8, 16]} />
        <meshStandardMaterial color={PLUM} roughness={0.4} metalness={0.1} />
      </mesh>
      {/* corset shimmer */}
      <mesh position={[0, -0.1, 0]}>
        <capsuleGeometry args={[0.31, 0.2, 8, 16]} />
        <meshStandardMaterial color={MAGENTA} emissive={MAGENTA} emissiveIntensity={0.4} roughness={0.3} />
      </mesh>
      {/* face */}
      <Text position={[0, 0.18, 0.31]} fontSize={0.28} anchorX="center" anchorY="middle">😏</Text>
      {/* jester crown */}
      <Text position={[0, 0.6, 0]} fontSize={0.4} anchorX="center" anchorY="middle">👑</Text>
      <Sparkles count={18} scale={[1.2, 1.6, 1.2]} size={2.5} speed={0.4} color={GOLD} />
    </group>
  );
}

/** A glossy stage tile to walk on / jump between. */
function Platform({ position, size = [4, 0.5, 4] as [number, number, number], color = "#2a1840" }) {
  return (
    <RigidBody type="fixed" colliders="cuboid" position={position}>
      <mesh receiveShadow castShadow>
        <boxGeometry args={size} />
        <meshStandardMaterial color={color} roughness={0.6} metalness={0.2} />
      </mesh>
    </RigidBody>
  );
}

function Scene() {
  return (
    <>
      <color attach="background" args={["#140a24"]} />
      <fog attach="fog" args={["#140a24", 12, 40]} />
      <ambientLight intensity={0.2} />
      <spotLight position={[8, 14, 8]} angle={0.5} penumbra={0.8} intensity={250} color={GOLD} castShadow shadow-mapSize={[2048, 2048]} />
      <spotLight position={[-10, 6, -6]} angle={0.7} penumbra={1} intensity={140} color={MAGENTA} />

      <Physics timeStep="vary">
        {/* big floor */}
        <RigidBody type="fixed" colliders="cuboid" position={[0, -1, 0]}>
          <mesh receiveShadow>
            <boxGeometry args={[60, 1, 60]} />
            <meshStandardMaterial color="#1c0f30" roughness={0.8} />
          </mesh>
        </RigidBody>

        {/* jumpable platforms */}
        <Platform position={[6, 0.5, 0]} />
        <Platform position={[11, 2, -3]} size={[3, 0.5, 3]} color="#3a1a5e" />
        <Platform position={[-7, 1.2, 4]} size={[3.5, 0.5, 3.5]} color="#3a1a5e" />

        {/* THE CONTROLLER — gravity, follow-camera, WASD + Space all built in */}
        <KeyboardControls map={keyboardMap}>
          <Ecctrl
            position={[0, 3, 0]}
            maxVelLimit={4}
            jumpVel={5}
            camInitDis={-6}
            camMaxDis={-10}
            camMinDis={-2}
          >
            <JesterModel />
          </Ecctrl>
        </KeyboardControls>
      </Physics>

      <ContactShadows position={[0, -0.49, 0]} opacity={0.4} scale={40} blur={2.5} far={6} color="#2a0a3e" />
      <Environment preset="night" />
    </>
  );
}

const meta: Meta = {
  title: "r3f/JesterQueenController",
  parameters: {
    layout: "fullscreen",
    docs: { description: { component: "Ecctrl playground: WASD/arrows to move, Shift to run, Space to jump. Camera follows. Walk her between the platforms." } },
  },
};
export default meta;

export const WalkHer: StoryObj = {
  render: () => (
    <div style={{ width: "100vw", height: "100vh", background: "#140a24" }}>
      <Canvas shadows camera={{ position: [0, 4, 8], fov: 42 }} dpr={[1, 2]}>
        <Scene />
      </Canvas>
      <div style={{ position: "fixed", left: 16, bottom: 16, color: "#ffd166", font: "13px ui-monospace,monospace", textShadow: "0 1px 3px #000" }}>
        WASD / arrows · Shift = run · Space = jump
      </div>
    </div>
  ),
};
