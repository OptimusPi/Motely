import type { Meta, StoryObj } from '@storybook/react'
import { Canvas } from '@react-three/fiber'
import { useState } from 'react'
import { Card3D } from './Card3D'
import { JimboAppScroll } from '../ui/jimboApp'
import { getSpriteData, SHEET_META } from '../sprites/spriteMapper'
import { resolveJamlAssetUrl } from '../assets'
import type { MotelySpriteData } from '../decode/motelySprite'

function makeSprite(name: string, sheet: keyof typeof SHEET_META): MotelySpriteData | null {
  const sprite = getSpriteData(name)
  const meta = SHEET_META[sheet]
  if (!sprite || !meta) return null
  return {
    atlasPath: resolveJamlAssetUrl(meta.assetKey),
    gridCol: sprite.pos.x,
    gridRow: sprite.pos.y,
    gridCols: meta.cols,
    gridRows: meta.rows,
    displayName: name,
    category: 'joker',
  }
}

const SHOWCASE_CARDS = [
  makeSprite('Joker', 'Jokers'),
  makeSprite('Blueprint', 'Jokers'),
  makeSprite('Brainstorm', 'Jokers'),
].filter(Boolean) as MotelySpriteData[]

const meta = {
  title: 'R3F / Card3D',
  parameters: { jimboHarness: true, layout: 'fullscreen' },
} satisfies Meta

export default meta
type Story = StoryObj<typeof meta>

export const Single: Story = {
  render: () => {
    const sprite = makeSprite('Joker', 'Jokers')
    if (!sprite) return <div>Sprite not found</div>
    return (
      <JimboAppScroll>
        <div style={{ width: '100%', aspectRatio: '1 / 1' }}>
          <Canvas camera={{ position: [0, 0, 2.2], fov: 40 }}>
            <ambientLight intensity={0.8} />
            <pointLight position={[2, 2, 2]} intensity={1.2} />
            <Card3D sprite={sprite} />
          </Canvas>
        </div>
      </JimboAppScroll>
    )
  },
}

export const SelectableFan: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [selected, setSelected] = useState<number | null>(null)
    const spacing = 0.85
    const offset = ((SHOWCASE_CARDS.length - 1) / 2) * spacing
    return (
      <JimboAppScroll>
        <div style={{ width: '100%', aspectRatio: '4 / 3' }}>
          <Canvas camera={{ position: [0, 0, 3.5], fov: 40 }}>
            <ambientLight intensity={0.8} />
            <pointLight position={[0, 2, 3]} intensity={1.5} />
            {SHOWCASE_CARDS.map((sprite, i) => (
              <Card3D
                key={sprite.displayName}
                sprite={sprite}
                position={[i * spacing - offset, 0, 0]}
                selected={selected === i}
                highlighted={selected === i}
                onClick={() => setSelected(selected === i ? null : i)}
              />
            ))}
          </Canvas>
        </div>
      </JimboAppScroll>
    )
  },
}
