/**
 * Advance past trailing punctuation and whitespace so reveal snaps to word boundaries.
 * Used by both JammySpeechBox (home announcement) and ChatInterface (streaming chat).
 */
export function getRevealPos(text: string, highlightPos: number): number {
  if (highlightPos <= 0) return 0
  if (highlightPos >= text.length) return text.length

  let i = highlightPos
  while (i < text.length && /[)\]}'"'\u2018\u2019.,!?;:]/.test(text[i]!)) i++
  while (i < text.length && /\s/.test(text[i]!)) i++

  return i
}
