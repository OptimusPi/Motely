export interface HighlightingRules {
    highPriority: Set<string>; // 'Must' items
    mediumPriority: Set<string>; // 'Should' items
}

/**
 * Simple regex-based JAML parser for extracting highlighting rules.
 * Handles basic YAML structure:
 * Must:
 *   - joker: WeeJoker
 * Should:
 *   - joker: HangingChad
 */
export function parseJamlHighlights(jamlContent: string | null): HighlightingRules {
    const rules: HighlightingRules = {
        highPriority: new Set(),
        mediumPriority: new Set()
    };

    if (!jamlContent) return rules;

    const lines = jamlContent.split('\n');
    let currentSection: 'Must' | 'Should' | null = null;

    for (const line of lines) {
        const trimmed = line.trim();

        if (trimmed.startsWith('Must:')) {
            currentSection = 'Must';
            continue;
        }
        if (trimmed.startsWith('Should:')) {
            currentSection = 'Should';
            continue;
        }

        // Look for "- joker: Name" or "- souljoker: Name"
        const jokerMatch = trimmed.match(/- (?:joker|souljoker): (.+)/);
        if (jokerMatch) {
            const name = jokerMatch[1].trim();
            // Normalize: "WeeJoker" -> "Wee Joker" if needed? 
            // Blueprint output usually has spaces "Wee Joker". 
            // JAML uses "WeeJoker".
            // We'll normalize by removing spaces for comparison.
            const normalized = name.replace(/\s+/g, '').toLowerCase();

            if (currentSection === 'Must') rules.highPriority.add(normalized);
            if (currentSection === 'Should') rules.mediumPriority.add(normalized);
        }
    }

    return rules;
}

export function normalizeName(name: string): string {
    return name.replace(/\s+/g, '').toLowerCase();
}
