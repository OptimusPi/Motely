'use client'

import { splitTtsDisplay } from '../../lib/tts/splitTtsDisplay'

/** Fixed chrome so the home announcement always occupies one predictable size (ai-elements, same family as chat). */
const speechBoxFrame =
  'w-[20.5rem] max-w-[calc(100vw-2.25rem)] min-h-[7.25rem] h-[7.25rem] rounded-xl border border-white/15 bg-black/72 px-4 py-3 text-center shadow-lg backdrop-blur-md'

const speechText =
  'whitespace-pre-wrap break-words text-sm font-normal leading-relaxed tracking-normal text-[#f6f0d5] antialiased'

// Simple utility to concatenate classes instead of importing tailwind-merge (for agnostic component)
function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(' ')
}

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
      className={cn(
        // Anchor to the scene center so Jammy stays centered inside the orbital menu,
        // with the announcement floating just above the mascot's head.
        'pointer-events-none absolute top-1/2 left-1/2 z-20 flex w-full max-w-[calc(100vw-2.25rem)] -translate-x-1/2 -translate-y-[calc(100%+5.75rem)] justify-center sm:-translate-y-[calc(100%+6.25rem)]',
        className,
      )}
    >
      <div className="max-w-none">
        <div
          className={cn(
            speechBoxFrame,
            'flex flex-col items-center justify-center overflow-hidden text-[#f6f0d5]!',
          )}
        >
          <p className={cn(speechText, 'm-0 w-full max-w-full')}>
            <span className="text-[#f6f0d5]">{prefix}</span>
            <span className="text-[#f6f0d5]">{spoken}</span>
            <span className="text-[#f6f0d5]/35">{pending}</span>
            <span className="text-[#f6f0d5]/35">{suffix}</span>
          </p>
        </div>
      </div>
    </div>
  )
}
