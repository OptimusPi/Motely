'use client'

type Props = Readonly<{
  value: string
  onChange: (room: string) => void
  title?: string
}>

export function MultiplayerRoomBar({ value, onChange, title }: Props) {
  return (
    <label style={{ display: 'flex', gap: 6, alignItems: 'center', marginTop: 6 }}>
      <span style={{ color: '#888', fontSize: '0.7rem' }}>Sync room</span>
      <input
        value={value}
        onChange={(e) => {
          const v = e.target.value
          onChange(v)
          const u = new URL(window.location.href)
          const t = v.trim()
          if (t) u.searchParams.set('room', t)
          else u.searchParams.delete('room')
          window.history.replaceState({}, '', `${u.pathname}${u.search}${u.hash}`)
        }}
        placeholder="e.g. lobby-1"
        spellCheck={false}
        title={
          title ??
          'Shared state via Upstash (KV_REST_*). Same room id = same table / highway drive snap.'
        }
        style={{
          minWidth: 120,
          maxWidth: 200,
          background: '#16161c',
          border: '1px solid #2a2a32',
          color: '#ddd',
          padding: '4px 8px',
          borderRadius: 4,
          fontFamily: 'ui-monospace, monospace',
          fontSize: 11,
        }}
      />
    </label>
  )
}

export function readMultiplayerRoomFromUrl(): string {
  if (typeof window === 'undefined') return ''
  return new URLSearchParams(window.location.search).get('room')?.trim() ?? ''
}

export function readDriveSeatFromUrl(): 'driver' | 'passenger' {
  if (typeof window === 'undefined') return 'driver'
  const s = new URLSearchParams(window.location.search).get('seat')?.toLowerCase()
  return s === 'passenger' ? 'passenger' : 'driver'
}
