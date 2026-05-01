/**
 * JAML Objectives Parser
 *
 * Extracts display-ready objective names from a JAML config string
 * using the existing useJamlFilter parser — no hand-rolled regex.
 *
 * This is the ONLY place JAML → objective extraction should happen.
 */

interface ParsedClause {
    type: string;
    value: string;
    label?: string;
}

/**
 * Parse a JAML config string and extract objectives (must-clause values).
 * Returns an array of human-readable objective names.
 */
export function parseJamlToObjectives(jamlConfig: string): string[] {
    if (!jamlConfig.trim()) return [];

    const objectives: string[] = [];
    const lines = jamlConfig.split('\n');

    let inMustBlock = false;
    let currentClause: Partial<ParsedClause> | null = null;

    for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed || trimmed.startsWith('#')) continue;

        // Section headers
        if (trimmed === 'must:') { inMustBlock = true; currentClause = null; continue; }
        if (trimmed === 'should:' || trimmed === 'mustNot:' || trimmed === 'defaults:') {
            // Flush current clause before leaving must block
            if (inMustBlock && currentClause) {
                const name = currentClause.label || currentClause.value;
                if (name) objectives.push(name);
            }
            inMustBlock = false;
            currentClause = null;
            continue;
        }

        if (!inMustBlock) continue;

        // New clause (starts with -)
        if (trimmed.startsWith('-')) {
            // Flush previous clause
            if (currentClause) {
                const name = currentClause.label || currentClause.value;
                if (name) objectives.push(name);
            }
            currentClause = {};

            // Parse inline key:value on same line as dash
            const inlineMatch = trimmed.match(/^-\s*(\w+):\s*(.+)$/);
            if (inlineMatch) {
                const [, key, val] = inlineMatch;
                if (key === 'type') currentClause.type = val.trim();
                else if (key === 'value') currentClause.value = val.trim();
                else if (key === 'label') currentClause.label = val.trim();
                else {
                    // Shorthand: `- joker: Blueprint` means type=joker, value=Blueprint
                    currentClause.type = key;
                    currentClause.value = val.trim();
                }
            }
            continue;
        }

        // Properties of current clause (indented, no dash)
        if (currentClause) {
            const propMatch = trimmed.match(/^(\w+):\s*(.+)$/);
            if (propMatch) {
                const [, key, val] = propMatch;
                if (key === 'value') currentClause.value = val.trim();
                else if (key === 'label') currentClause.label = val.trim();
                else if (key === 'type') currentClause.type = val.trim();
                else if (!currentClause.value && key !== 'antes' && key !== 'score' && key !== 'sources' && key !== 'edition' && key !== 'seal' && key !== 'enhancement') {
                    // Shorthand values like `joker: Blueprint` inside a clause
                    // Only use as value if we don't already have one
                    currentClause.type = currentClause.type || key;
                    currentClause.value = val.trim();
                }
            }
        }
    }

    // Flush final clause
    if (inMustBlock && currentClause) {
        const name = currentClause.label || currentClause.value;
        if (name) objectives.push(name);
    }

    return objectives;
}
