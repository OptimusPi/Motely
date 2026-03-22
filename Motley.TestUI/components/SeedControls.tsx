'use client'

import { useEffect, useState } from 'react'
import { useBalatroStore } from '../balatro'

export function SeedControls() {
  const gameSeed = useBalatroStore((s) => s.gameSeed)
  const initGame = useBalatroStore((s) => s.initGame)
  const [seedInput, setSeedInput] = useState(gameSeed)

  useEffect(() => {
    setSeedInput(gameSeed)
  }, [gameSeed])

  return (
    <div
      className="seed-controls"
      style={{ display: 'flex', gap: 8, flexWrap: 'wrap', alignItems: 'center', marginTop: 8 }}
    >
      <label style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
        <span style={{ color: '#aaa', fontSize: '0.75rem' }}>Seed</span>
        <input
          value={seedInput}
          onChange={(e) => setSeedInput(e.target.value)}
          spellCheck={false}
          style={{
            minWidth: 140,
            background: '#16161c',
            border: '1px solid #2a2a32',
            color: '#ddd',
            padding: '4px 8px',
            borderRadius: 4,
            fontFamily: 'ui-monospace, monospace',
            fontSize: 12,
          }}
        />
      </label>
      <button
        type="button"
        className="tool-switch"
        onClick={() => initGame({ seed: seedInput })}
        style={{ fontSize: '0.7rem' }}
      >
        Deal / apply seed
      </button>
      <button
        type="button"
        className="tool-switch"
        onClick={() => initGame({ newRun: true })}
        style={{ fontSize: '0.7rem' }}
      >
        New run
      </button>
    </div>
  )
}
