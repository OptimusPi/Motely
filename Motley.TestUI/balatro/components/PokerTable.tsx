import { useRef } from 'react'
import { useFrame } from '@react-three/fiber'
import * as THREE from 'three'

interface PokerTableProps {
  position?: [number, number, number]
}

export function PokerTable({ position = [0, -0.5, 0] }: PokerTableProps) {
  const tableRef = useRef<THREE.Group>(null)

  useFrame((state) => {
    if (tableRef.current) {
      // Subtle ambient movement
      tableRef.current.rotation.y = Math.sin(state.clock.elapsedTime * 0.1) * 0.005
    }
  })

  return (
    <group ref={tableRef} position={position}>
      {/* Table surface */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
        <circleGeometry args={[5, 64]} />
        <meshStandardMaterial color="#0d5c2e" metalness={0.1} roughness={0.9} />
      </mesh>

      {/* Table felt texture overlay */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.001, 0]}>
        <circleGeometry args={[4.8, 64]} />
        <meshStandardMaterial
          color="#0a4a24"
          metalness={0}
          roughness={1}
          transparent
          opacity={0.3}
        />
      </mesh>

      {/* Table edge/rim */}
      <mesh position={[0, 0.05, 0]}>
        <torusGeometry args={[5, 0.15, 16, 64]} />
        <meshStandardMaterial color="#2c1810" metalness={0.3} roughness={0.7} />
      </mesh>

      {/* Inner decorative ring */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.002, 0]}>
        <ringGeometry args={[3.5, 3.6, 64]} />
        <meshStandardMaterial
          color="#c9a227"
          metalness={0.6}
          roughness={0.4}
          emissive="#c9a227"
          emissiveIntensity={0.1}
        />
      </mesh>

      {/* Card play area highlight */}
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, 0.003, 0]}>
        <planeGeometry args={[4, 2]} />
        <meshStandardMaterial color="#0a5a2a" transparent opacity={0.4} />
      </mesh>

      {/* Corner decorations */}
      {[0, 1, 2, 3].map((i) => {
        const angle = (i * Math.PI) / 2 + Math.PI / 4
        const x = Math.cos(angle) * 4
        const z = Math.sin(angle) * 4
        return (
          <mesh key={`corner-${i}`} position={[x, 0.01, z]} rotation={[-Math.PI / 2, 0, angle]}>
            <circleGeometry args={[0.3, 6]} />
            <meshStandardMaterial color="#c9a227" metalness={0.7} roughness={0.3} />
          </mesh>
        )
      })}
    </group>
  )
}

export default PokerTable
