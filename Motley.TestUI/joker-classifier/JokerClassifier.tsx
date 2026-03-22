import { useEffect, useState } from 'react'
import { BalatroJokerThreePreview } from './BalatroJokerThreePreview'
import { BASE_JOKERS, loadJokersFromStorage, saveJokersToStorage } from './defaultJokers'
import type { JokerRow } from './types'

const s = {
  root: {
    background: '#111',
    color: '#ccc',
    minHeight: '100vh',
    padding: 16,
    paddingBottom: 48,
    fontFamily: 'ui-monospace, monospace',
    fontSize: 13,
  } as const,
  h1: { color: '#eee', fontSize: 16, fontWeight: 'bold' as const, margin: '0 0 2px 0' },
  sub: { color: '#666', fontSize: 12, marginBottom: 12 },
  row: {
    display: 'flex',
    gap: 8,
    marginBottom: 10,
    flexWrap: 'wrap' as const,
    alignItems: 'center' as const,
  },
  input: {
    background: '#1a1a1a',
    border: '1px solid #333',
    color: '#ccc',
    padding: '4px 8px',
    fontSize: 12,
    borderRadius: 2,
    outline: 'none',
  },
  btn: (active: boolean) =>
    ({
      background: active ? '#222' : '#161616',
      border: '1px solid #333',
      color: active ? '#eee' : '#666',
      padding: '4px 10px',
      fontSize: 12,
      borderRadius: 2,
      cursor: 'pointer',
    }) as const,
  saveBtn: {
    background: '#161616',
    border: '1px solid #2a3a2a',
    color: '#6a9a6a',
    padding: '4px 10px',
    fontSize: 12,
    borderRadius: 2,
    cursor: 'pointer',
  },
  dlBtn: {
    background: '#161616',
    border: '1px solid #2a2a3a',
    color: '#6a6a9a',
    padding: '4px 10px',
    fontSize: 12,
    borderRadius: 2,
    cursor: 'pointer',
  },
  th: {
    padding: '5px 8px',
    color: '#555',
    fontWeight: 'normal' as const,
    borderBottom: '1px solid #222',
    textAlign: 'left' as const,
  },
  td: { padding: '4px 8px', verticalAlign: 'middle' as const },
}

type Props = {
  onBack?: () => void
  onOpenAdventure?: () => void
}

export function JokerClassifier({ onBack, onOpenAdventure }: Props) {
  const [jokers, setJokers] = useState<JokerRow[]>(BASE_JOKERS)
  const [previewName, setPreviewName] = useState<string>(BASE_JOKERS[0]?.name ?? 'Joker')
  const [search, setSearch] = useState('')
  const [filter, setFilter] = useState<'all' | 'humanoid' | 'object'>('all')
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const stored = loadJokersFromStorage()
    if (stored && stored.length > 0) setJokers(stored)
    setLoading(false)
  }, [])

  useEffect(() => {
    if (!jokers.some((j) => j.name === previewName) && jokers[0]) {
      setPreviewName(jokers[0].name)
    }
  }, [jokers, previewName])

  const toggle = (name: string) =>
    setJokers((prev) => prev.map((j) => (j.name === name ? { ...j, humanoid: !j.humanoid } : j)))

  const updateNotes = (name: string, val: string) =>
    setJokers((prev) => prev.map((j) => (j.name === name ? { ...j, notes: val } : j)))

  const msg = (text: string) => {
    setStatus(text)
    window.setTimeout(() => setStatus(''), 2500)
  }

  const save = () => {
    try {
      saveJokersToStorage(jokers)
      msg('saved ✓')
    } catch (e) {
      msg(`error: ${e instanceof Error ? e.message : String(e)}`)
    }
  }

  const exportJSON = () => {
    const blob = new Blob([JSON.stringify(jokers, null, 2)], { type: 'application/json' })
    const a = Object.assign(document.createElement('a'), {
      href: URL.createObjectURL(blob),
      download: 'balatro_jokers.json',
    })
    a.click()
    URL.revokeObjectURL(a.href)
    msg('downloaded ✓')
  }

  const humanoidCount = jokers.filter((j) => j.humanoid).length
  const objectCount = jokers.filter((j) => !j.humanoid).length

  const visible = jokers.filter((j) => {
    const q = search.toLowerCase()
    const matchSearch = !q || j.name.toLowerCase().includes(q) || j.notes.toLowerCase().includes(q)
    const matchFilter =
      filter === 'all' ||
      (filter === 'humanoid' && j.humanoid) ||
      (filter === 'object' && !j.humanoid)
    return matchSearch && matchFilter
  })

  if (loading) return <div style={s.root}>loading...</div>

  return (
    <div style={s.root}>
      <div style={{ maxWidth: 1240, margin: '0 auto' }}>
        {(onBack || onOpenAdventure) && (
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: 12 }}>
            {onBack && (
              <button type="button" onClick={onBack} style={s.btn(false)}>
                ← Balatro 3D
              </button>
            )}
            {onOpenAdventure && (
              <button type="button" onClick={onOpenAdventure} style={s.btn(false)}>
                Highway drive →
              </button>
            )}
          </div>
        )}
        <h1 style={s.h1}>Balatro Joker Classifier</h1>
        <div style={s.sub}>
          {humanoidCount} humanoid · {objectCount} object/prop · {jokers.length} total (dupes
          stripped from seed list)
        </div>
        <div
          style={{
            marginBottom: 14,
            padding: '10px 12px',
            background: '#161616',
            border: '1px solid #2a2a2a',
            borderRadius: 4,
            color: '#777',
            fontSize: 11,
            lineHeight: 1.45,
          }}
        >
          <strong style={{ color: '#9a9a9a' }}>3D studio sheet — </strong>
          shared spec for you + the modeler: <strong style={{ color: '#aaa' }}>humanoid</strong> →
          rigged character mesh / walk cycle lane; <strong style={{ color: '#aaa' }}>object</strong>{' '}
          → prop, sign, or building block. <strong style={{ color: '#aaa' }}>bg</strong> is the card
          base tint for materials;
          <strong style={{ color: '#aaa' }}> notes</strong> are the art brief (export JSON when you
          want a frozen handoff). <strong style={{ color: '#aaa' }}>Click a row</strong> for the
          official spritesheet slice (<code style={{ color: '#888' }}>Jokers.png</code> +{' '}
          <code style={{ color: '#888' }}>balatro-jokers.json</code>
          ).
        </div>

        <div style={s.row}>
          <input
            style={{ ...s.input, width: 200 }}
            placeholder="search name or notes..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          {(
            [
              ['all', `all (${jokers.length})`],
              ['humanoid', `humanoid (${humanoidCount})`],
              ['object', `object (${objectCount})`],
            ] as const
          ).map(([f, label]) => (
            <button key={f} type="button" onClick={() => setFilter(f)} style={s.btn(filter === f)}>
              {label}
            </button>
          ))}
          <div style={{ flex: 1 }} />
          <button type="button" onClick={save} style={s.saveBtn}>
            save to localStorage
          </button>
          <button type="button" onClick={exportJSON} style={s.dlBtn}>
            export JSON
          </button>
          {status && <span style={{ color: '#6a9a6a', fontSize: 12 }}>{status}</span>}
        </div>

        <div
          style={{
            display: 'flex',
            gap: 20,
            alignItems: 'flex-start',
            flexWrap: 'wrap' as const,
          }}
        >
          <div style={{ flex: '1 1 480px', minWidth: 0 }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr>
                  <th style={{ ...s.th, width: 30 }}>#</th>
                  <th style={{ ...s.th, width: 28 }}>bg</th>
                  <th style={s.th}>name</th>
                  <th style={{ ...s.th, width: 72, textAlign: 'center' }}>humanoid</th>
                  <th style={s.th}>notes (editable)</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((j, i) => (
                  <tr
                    key={j.name}
                    role="button"
                    tabIndex={0}
                    onClick={() => setPreviewName(j.name)}
                    onKeyDown={(e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault()
                        setPreviewName(j.name)
                      }
                    }}
                    style={{
                      cursor: 'pointer',
                      background: i % 2 === 0 ? '#141414' : '#111',
                      borderBottom: '1px solid #1a1a1a',
                      outline:
                        j.name === previewName ? '1px solid rgba(124, 58, 237, 0.5)' : 'none',
                      outlineOffset: -1,
                    }}
                  >
                    <td style={{ ...s.td, color: '#444' }}>{j.id}</td>
                    <td style={s.td}>
                      <div
                        style={{
                          width: 20,
                          height: 20,
                          background: j.bg,
                          border: '1px solid #2a2a2a',
                          borderRadius: 2,
                        }}
                      />
                    </td>
                    <td style={{ ...s.td, color: j.humanoid ? '#ddd' : '#777' }}>{j.name}</td>
                    <td style={{ ...s.td, textAlign: 'center' }}>
                      <input
                        type="checkbox"
                        checked={j.humanoid}
                        onClick={(e) => e.stopPropagation()}
                        onChange={() => toggle(j.name)}
                        style={{ width: 14, height: 14, cursor: 'pointer', accentColor: '#7c3aed' }}
                      />
                    </td>
                    <td style={s.td}>
                      <input
                        value={j.notes}
                        onClick={(e) => e.stopPropagation()}
                        onChange={(e) => updateNotes(j.name, e.target.value)}
                        style={{
                          background: 'transparent',
                          border: 'none',
                          color: '#555',
                          fontSize: 12,
                          width: '100%',
                          outline: 'none',
                          fontFamily: 'ui-monospace, monospace',
                        }}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            {visible.length === 0 && (
              <div style={{ color: '#444', textAlign: 'center', padding: 24 }}>no results</div>
            )}
          </div>

          <aside
            style={{
              flex: '0 0 300px',
              position: 'sticky' as const,
              top: 16,
              alignSelf: 'flex-start' as const,
            }}
          >
            <div
              style={{
                color: '#888',
                fontSize: 11,
                marginBottom: 8,
                textTransform: 'uppercase' as const,
                letterSpacing: '0.08em',
              }}
            >
              In-game sprite (P_CENTERS)
            </div>
            <BalatroJokerThreePreview displayName={previewName} />
          </aside>
        </div>
      </div>
    </div>
  )
}
