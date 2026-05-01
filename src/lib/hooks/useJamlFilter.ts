import { useState, useCallback, useMemo } from 'react';

export interface JamlClause {
    type: string;
    value: string;
    label?: string;
    antes?: number[];
    score?: number;
    edition?: string;
    seal?: string;
    enhancement?: string;
    rank?: string;
    suit?: string;
    sources?: string[];
}

export interface JamlFilter {
    name: string;
    description: string;
    author: string;
    deck: string;
    stake: string;
    defaults: {
        antes: number[];
        packSlots: number[];
        shopSlots: number[];
        score: number;
    };
    must: JamlClause[];
    should: JamlClause[];
    mustNot: JamlClause[];
}

import {
    DECK_OPTIONS,
    STAKE_OPTIONS,
    ANTE_OPTIONS,
    SLOT_OPTIONS,
    CLAUSE_TYPES,
    SOURCE_OPTIONS,
    EDITION_OPTIONS,
    SEAL_OPTIONS,
    ENHANCEMENT_OPTIONS
} from '@/lib/data/constants';

export {
    DECK_OPTIONS,
    STAKE_OPTIONS,
    ANTE_OPTIONS,
    SLOT_OPTIONS,
    CLAUSE_TYPES,
    SOURCE_OPTIONS,
    EDITION_OPTIONS,
    SEAL_OPTIONS,
    ENHANCEMENT_OPTIONS
};

export function useJamlFilter(initialJaml?: string) {
    const [filter, setFilter] = useState<JamlFilter>(parseJamlToFilter(initialJaml || ''));
    const [editingClause, setEditingClause] = useState<{ bucket: keyof Pick<JamlFilter, 'must' | 'should' | 'mustNot'>, index: number | null } | null>(null);

    // useEffect removed to restore previous UX behavior
    // The JAML from server (Rules format) parses to empty clauses,
    // causing the UI to lose state after load. we want to keep the initial state.

    const jamlText = useMemo(() => filterToJaml(filter), [filter]);

    const updateFilter = useCallback((updates: Partial<JamlFilter>) => {
        setFilter(prev => ({ ...prev, ...updates }));
    }, []);

    const addClause = useCallback((bucket: 'must' | 'should' | 'mustNot', clause: JamlClause) => {
        setFilter(prev => ({
            ...prev,
            [bucket]: [...prev[bucket], clause]
        }));
    }, []);

    const editClause = useCallback((bucket: 'must' | 'should' | 'mustNot', index: number, clause: JamlClause) => {
        setFilter(prev => ({
            ...prev,
            [bucket]: prev[bucket].map((c, i) => i === index ? clause : c)
        }));
    }, []);

    const deleteClause = useCallback((bucket: 'must' | 'should' | 'mustNot', index: number) => {
        setFilter(prev => ({
            ...prev,
            [bucket]: prev[bucket].filter((_, i) => i !== index)
        }));
    }, []);

    const setFromJaml = useCallback((text: string) => {
        const newFilter = parseJamlToFilter(text);
        setFilter(newFilter);
    }, []);

    return {
        filter,
        jamlText,
        setFromJaml,
        updateFilter,
        addClause,
        editClause,
        deleteClause,
        editingClause,
        setEditingClause
    };
}

function parseJamlToFilter(text: string): JamlFilter {
    if (!text.trim()) return createBlankFilter();

    try {
        const filter = createBlankFilter();
        // Reset arrays to empty since we are parsing fresh
        filter.must = [];
        filter.should = [];
        filter.mustNot = [];

        const lines = text.split('\n');
        let currentSection: 'must' | 'should' | 'mustNot' | 'defaults' | null = null;
        let currentClause: JamlClause | null = null;

        for (const line of lines) {
            const trimmed = line.trim();
            if (!trimmed || trimmed.startsWith('#')) continue;

            // 1. Detect Section Headers
            if (trimmed === 'must:') { currentSection = 'must'; currentClause = null; continue; }
            if (trimmed === 'should:') { currentSection = 'should'; currentClause = null; continue; }
            if (trimmed === 'mustNot:') { currentSection = 'mustNot'; currentClause = null; continue; }
            if (trimmed === 'defaults:') { currentSection = 'defaults'; continue; }

            // 2. Detect Root Properties (indent 0)
            const rootMatch = line.match(/^(\w+):\s*(.+)$/);
            if (rootMatch && !line.startsWith(' ') && !line.startsWith('-')) {
                const [_, key, val] = rootMatch;
                if (['name', 'deck', 'stake', 'description', 'author'].includes(key)) {
                    (filter as any)[key] = val.trim();
                }
                continue;
            }

            // 3. Handle Sections
            if (currentSection) {
                if (currentSection === 'defaults') {
                    const propMatch = trimmed.match(/^(\w+):\s*(.+)$/);
                    if (propMatch) {
                        const [_, key, val] = propMatch;
                        if (key === 'antes') filter.defaults.antes = parseNumArray(val);
                        if (key === 'packSlots') filter.defaults.packSlots = parseNumArray(val);
                        if (key === 'shopSlots') filter.defaults.shopSlots = parseNumArray(val);
                        if (key === 'score') filter.defaults.score = parseInt(val) || 0;
                    }
                } else {
                    // Start of new clause
                    const typeMatch = trimmed.match(/^-\s*type:\s*(.+)$/);
                    if (typeMatch) {
                        currentClause = { type: typeMatch[1].trim(), value: '' };
                        filter[currentSection].push(currentClause);
                        continue;
                    }

                    // Properties of current clause
                    if (currentClause) {
                        const propMatch = trimmed.match(/^(\w+):\s*(.+)$/);
                        if (propMatch) {
                            const [_, key, val] = propMatch;
                            if (key === 'antes') currentClause.antes = parseNumArray(val);
                            else if (key === 'sources') currentClause.sources = parseStringArray(val);
                            else if (key === 'score') currentClause.score = parseInt(val) || 0;
                            else (currentClause as any)[key] = val.trim();
                        }
                    }
                }
            }
        }

        return filter;
    } catch (e) {
        console.warn('JAML Parse Error:', e);
        return createBlankFilter();
    }
}

function parseNumArray(str: string): number[] {
    return str.replace(/[\[\]]/g, '').split(',').map(s => parseInt(s.trim())).filter(n => !isNaN(n));
}

function parseStringArray(str: string): string[] {
    return str.replace(/[\[\]]/g, '').split(',').map(s => s.trim()).filter(s => !!s);

}

function filterToJaml(filter: JamlFilter): string {
    const lines: string[] = [];

    lines.push(`name: ${filter.name}`);
    lines.push(`description: ${filter.description || ''}`);
    lines.push(`author: ${filter.author || ''}`);
    lines.push(`deck: ${filter.deck}`);
    lines.push(`stake: ${filter.stake}`);
    lines.push('');
    lines.push('defaults:');
    lines.push(`  antes: [${filter.defaults.antes.join(', ')}]`);
    lines.push(`  packSlots: [${filter.defaults.packSlots.join(', ')}]`);
    lines.push(`  shopSlots: [${filter.defaults.shopSlots.join(', ')}]`);
    lines.push(`  score: ${filter.defaults.score}`);
    lines.push('');

    if (filter.must.length > 0) {
        lines.push('must:');
        filter.must.forEach(clause => lines.push(...clauseToLines(clause, '  ')));
    }

    if (filter.should.length > 0) {
        lines.push('should:');
        filter.should.forEach(clause => lines.push(...clauseToLines(clause, '  ')));
    }

    if (filter.mustNot.length > 0) {
        lines.push('mustNot:');
        filter.mustNot.forEach(clause => lines.push(...clauseToLines(clause, '  ')));
    }

    return lines.join('\n');
}

function clauseToLines(clause: JamlClause, indent: string): string[] {
    const lines: string[] = [];
    lines.push(`${indent}- type: ${clause.type}`);
    lines.push(`${indent}  value: ${clause.value}`);
    if (clause.label) lines.push(`${indent}  label: ${clause.label}`);
    if (clause.antes && clause.antes.length > 0) lines.push(`${indent}  antes: [${clause.antes.join(', ')}]`);
    if (clause.score) lines.push(`${indent}  score: ${clause.score}`);
    if (clause.edition) lines.push(`${indent}  edition: ${clause.edition}`);
    if (clause.seal) lines.push(`${indent}  seal: ${clause.seal}`);
    if (clause.enhancement) lines.push(`${indent}  enhancement: ${clause.enhancement}`);
    if (clause.rank) lines.push(`${indent}  rank: ${clause.rank}`);
    if (clause.suit) lines.push(`${indent}  suit: ${clause.suit}`);
    if (clause.sources && clause.sources.length > 0) lines.push(`${indent}  sources: [${clause.sources.join(', ')}]`);
    return lines;
}

function createBlankFilter(): JamlFilter {
    return {
        name: 'New Filter',
        description: '',
        author: '',
        deck: 'Erratic',
        stake: 'White',
        defaults: {
            antes: [...ANTE_OPTIONS],
            packSlots: [...SLOT_OPTIONS],
            shopSlots: [...SLOT_OPTIONS],
            score: 1
        },
        must: [],
        should: [
            { type: 'joker', value: 'Joker' }
        ],
        mustNot: []
    };
}
