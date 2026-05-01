import { getRevealPos } from "./getRevealPos";

/** Strip markdown markers only — do not collapse whitespace (TTS highlight indices are raw offsets). */
export function stripMarkdownForTtsDisplay(text: string): string {
    return text
        .replace(/```[\s\S]*?```/g, "")
        .replace(/\*\*/g, "")
        .trim();
}

export type TtsDisplaySplit = {
    prefix: string;
    spoken: string;
    pending: string;
    suffix: string;
    useWindowedView: boolean;
};

/**
 * Split plain text into spoken vs pending regions for TTS word sync.
 * When `stripMarkdown` is false, indices must refer to `text` as-is (matches streaming TTS offsets).
 */
export function splitTtsDisplay(
    text: string,
    highlightPos: number | null | undefined,
    activeSentenceRange: { start: number; end: number } | null | undefined,
    options?: { stripMarkdown?: boolean },
): TtsDisplaySplit | null {
    const body = options?.stripMarkdown ? stripMarkdownForTtsDisplay(text) : text;
    if (!body) return null;

    const ttsDisabled = highlightPos === null || highlightPos === undefined;
    if (ttsDisabled) return null;

    const hasTtsHighlight = typeof highlightPos === "number" && highlightPos >= 0;
    const hasActiveSentence = Boolean(
        activeSentenceRange && activeSentenceRange.start >= 0 && activeSentenceRange.end > activeSentenceRange.start,
    );

    const sentenceStart = hasActiveSentence ? Math.min(activeSentenceRange!.start, body.length) : 0;
    const sentenceEnd = hasActiveSentence ? Math.min(activeSentenceRange!.end, body.length) : body.length;

    const revealPos = hasTtsHighlight
        ? Math.min(Math.max(getRevealPos(body, highlightPos!), sentenceStart), sentenceEnd)
        : 0;

    const useWindowedView = hasTtsHighlight && hasActiveSentence;
    const displayStart = useWindowedView ? sentenceStart : 0;
    const displayEnd = useWindowedView ? sentenceEnd : body.length;

    const prefix = useWindowedView ? "" : body.slice(0, sentenceStart);
    const spoken = body.slice(displayStart, Math.min(revealPos, displayEnd));
    const pending = body.slice(Math.min(revealPos, displayEnd), displayEnd);
    const suffix = useWindowedView ? "" : body.slice(sentenceEnd);

    return { prefix, spoken, pending, suffix, useWindowedView };
}
