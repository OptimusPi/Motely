'use client'

import { splitTtsDisplay } from '../../lib/tts/splitTtsDisplay.js'
import './mascot.css'

export function JammySpeechBox({
  text,
  highlightPos,
  activeSentenceRange,
  className,
}: {
  text: string | null
  highlightPos?: number | null
  activeSentenceRange?: { start: number; end: number } | null
  className?: string
}) {
  const raw = (text ?? '').trim()
  if (!raw) return null

  const split = splitTtsDisplay(raw, highlightPos, activeSentenceRange, { stripMarkdown: true })
  const ttsOff = highlightPos == null
  const displaySource = split
    ? `${split.prefix}${split.spoken}${split.pending}${split.suffix}`
    : raw.replace(/```[\s\S]*?```/g, '').replace(/\*\*/g, '').trim()
  if (!displaySource) return null

  const prefix = ttsOff || !split ? '' : split.prefix
  const spoken = !split || ttsOff ? displaySource : split.spoken
  const pending = !split || ttsOff ? '' : split.pending
  const suffix = !split || ttsOff ? '' : split.suffix

  return (
    <div
      data-testid="jammy-announcement"
      // Anchored to the scene center so Jammy stays centered inside the orbital
      // menu, with the announcement floating just above the mascot's head.
      className={['jimbo-jammy', className].filter(Boolean).join(' ')}
    >
      <div className="jimbo-jammy__inner">
        <div className="jimbo-jammy__box">
          <p className="jimbo-jammy__text">
            <span className="jimbo-jammy__seg">{prefix}</span>
            <span className="jimbo-jammy__seg">{spoken}</span>
            <span className="jimbo-jammy__seg--muted">{pending}</span>
            <span className="jimbo-jammy__seg--muted">{suffix}</span>
          </p>
        </div>
      </div>
    </div>
  )
}
