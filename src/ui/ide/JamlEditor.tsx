
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import yaml from 'js-yaml';
import { Plus, Minus, Check } from 'lucide-react';
import { JamlCompletionService, CompletionData } from '@/lib/jaml/jamlCompletion';



// Balatro Colors - High Contrast Theme
const COLORS = {
    white: '#FFFFFF',
    black: '#000000',
    red: '#ff5e5e',      // Balatro Red
    blue: '#4bb1ff',     // Balatro Blue
    green: '#46bc77',    // Balatro Green
    purple: '#9074ff',   // Balatro Purple
    orange: '#ff9d4d',   // Balatro Orange
    yellow: '#ffcf4d',   // Balatro Yellow
    gold: '#efb82d',
    // Editor background
    editorBg: '#0f1416',
    editorBgAlt: '#1c2629',
};

interface InteractiveJamlEditorProps {
    initialJaml?: string;
    onJamlChange?: (jamlYaml: string, parsed: unknown, isValid: boolean) => void;
    className?: string; // Add className prop for flexibility
}

interface ParsedLine {
    id: string;
    raw: string;
    indent: number;
    key?: string;
    value?: string;
    isComment: boolean;
    isArrayItem: boolean;
    lineNumber: number;
    clauseType?: string;
    validationState: 'required-incomplete' | 'optional-incomplete' | 'complete' | 'invalid' | 'metadata';
    isInvalidValue?: boolean;
    isArrayValue?: boolean;
    arrayValues?: string[];
}

const DEFAULT_JAML = `# My JAML Filter
name: My Filter
deck: Red
stake: White

must:
  - joker: Blueprint
    edition: Negative
    antes: [1, 2, 3]

should:
  - soulJoker: Any
    score: 5
`;

const METADATA_KEYS = ['name', 'author', 'description', 'deck', 'stake', 'label'];
const REQUIRED_KEYS = ['joker', 'soulJoker', 'voucher', 'tarotCard', 'planetCard', 'spectralCard', 'standardCard', 'tag', 'boss', 'event'];

// --- Sub-components ---

function SuggestionList({ suggestions, selectedIndex, onSelect, onHover }: {
    suggestions: CompletionData[],
    selectedIndex: number,
    onSelect: (val: string) => void,
    onHover: (idx: number) => void
}) {
    if (suggestions.length === 0) {
        return (
            <div className="p-2 text-xs text-white/40 italic">No suggestions...</div>
        );
    }

    return (
        <div className="flex flex-col max-h-[200px] overflow-y-auto" onMouseDown={e => e.preventDefault()}>
            {suggestions.map((s, idx) => {
                const isSelected = idx === selectedIndex;
                return (
                    <div
                        key={`${s.text}-${idx}`}
                        onClick={() => onSelect(s.text)}
                        onMouseEnter={() => onHover(idx)}
                        className={`
              flex items-center justify-between px-3 py-1.5 cursor-pointer font-mono text-[13px] transition-colors
              ${isSelected ? 'bg-[var(--balatro-gold)] text-black' : 'text-white hover:bg-white/10'}
            `}
                    >
                        <span>{s.displayText}</span>
                        {isSelected && <span className="opacity-50 text-[11px] ml-2">↵</span>}
                    </div>
                );
            })}
        </div>
    );
}

function AntesToggle({ values, onToggle, onStartEdit, color, darkColor }: {
    values: string[], onToggle: (val: string) => void, onStartEdit: () => void, color: string, darkColor: string
}) {
    const [expanded, setExpanded] = useState(false);
    const maxAnte = 8;
    const selectedAntes = new Set(values.map(v => parseInt(v, 10)).filter(n => !isNaN(n)));

    const getDisplayText = () => {
        if (selectedAntes.size === 0) return 'tap to select';
        const sorted = Array.from(selectedAntes).sort((a, b) => a - b);
        if (sorted.length === 1) return `Ante ${sorted[0]}`;
        const min = sorted[0];
        const max = sorted[sorted.length - 1];
        const isConsecutive = sorted.every((v, i) => i === 0 || v === sorted[i - 1] + 1);
        if (isConsecutive) return `Antes ${min}-${max}`;
        return `Antes ${sorted.join(', ')}`;
    };


    if (!expanded) {
        return (
            <div
                onClick={() => setExpanded(true)}
                className="jaml-block"
                style={{
                    '--jaml-bg': selectedAntes.size > 0 ? `${color}20` : `${COLORS.red}15`,
                    '--jaml-border': `1px solid ${selectedAntes.size > 0 ? color : COLORS.red}`,
                    '--jaml-color': selectedAntes.size > 0 ? color : COLORS.red,
                    '--jaml-min-w': '100px',
                    '--jaml-shadow': selectedAntes.size > 0 ? `0 0 10px ${color}20` : 'none',
                } as React.CSSProperties}
            >
                {getDisplayText()}
            </div>
        );
    }


    return (
        <div className="flex flex-row items-center">
            {Array.from({ length: maxAnte + 1 }, (_, i) => i).map((ante) => {
                const isSelected = selectedAntes.has(ante);
                const borderRadiusClass = ante === 0 ? 'jaml-ante-btn--first' : ante === maxAnte ? 'jaml-ante-btn--last' : 'jaml-ante-btn';
                return (
                    <div
                        key={ante}
                        onClick={(e) => {
                            e.stopPropagation();
                            onToggle(ante.toString());
                        }}
                        className={`jaml-block ${borderRadiusClass}`}
                        {...{
                            style: {
                                '--jaml-min-w': '28px',
                                '--jaml-bg': isSelected ? `${color}40` : 'transparent',
                                '--jaml-border': `1px solid ${isSelected ? color : 'rgba(255,255,255,0.2)'}`,
                                '--jaml-color': isSelected ? color : 'rgba(255,255,255,0.4)',
                                borderRight: ante < maxAnte ? 'none' : undefined,
                            } as React.CSSProperties
                        }}
                    >
                        {ante}
                    </div>
                );
            })}
            <div
                onClick={() => setExpanded(false)}
                className="jaml-block jaml-antes-confirm"
            >
                <Check size={16} strokeWidth={3} />
            </div>
        </div>
    );
}

// Custom Hook for Floating Position (Viewport-aware)
function useFloatingPosition(targetRef: React.RefObject<HTMLElement | null>, isOpen: boolean) {
    const [coords, setCoords] = useState<{ top: number; left: number; position: 'top' | 'bottom' } | null>(null);

    useEffect(() => {
        if (!isOpen || !targetRef.current) {
            setCoords(null);
            return;
        }

        const updatePosition = () => {
            if (!targetRef.current) return;
            const rect = targetRef.current.getBoundingClientRect();
            const spaceBelow = window.innerHeight - rect.bottom;
            const spaceAbove = rect.top;

            // Prefer bottom, unless too close to edge
            const showBelow = spaceBelow > 220 || spaceBelow > spaceAbove;

            setCoords({
                left: rect.left,
                top: showBelow ? rect.bottom + 4 : rect.top - 4,
                position: showBelow ? 'bottom' : 'top'
            });
        };

        updatePosition();
        // Update on scroll/resize to keep pinned
        window.addEventListener('scroll', updatePosition, true);
        window.addEventListener('resize', updatePosition);

        return () => {
            window.removeEventListener('scroll', updatePosition, true);
            window.removeEventListener('resize', updatePosition);
        };
    }, [isOpen, targetRef]);

    return coords;
}


// --- Main Editor Component ---

export default function JamlEditor({ initialJaml, onJamlChange, className }: InteractiveJamlEditorProps) {
    const [lines, setLines] = useState<ParsedLine[]>([]);
    const [editingLineId, setEditingLineId] = useState<string | null>(null);
    const [editingPart, setEditingPart] = useState<'key' | 'value' | 'arrayItem' | null>(null);
    const [editingArrayIndex, setEditingArrayIndex] = useState<number | null>(null);
    const [focusedLineIndex, setFocusedLineIndex] = useState<number>(0);

    const editorRef = useRef<HTMLDivElement>(null);

    // -- Parsing Logic --
    const parseJamlToLines = useCallback((text: string): ParsedLine[] => {
        const rawLines = text.split('\n');
        let currentClauseType: string | undefined;

        return rawLines.map((raw, index) => {
            const indent = raw.search(/\S|$/);
            const trimmed = raw.trim();
            const isComment = trimmed.startsWith('#');
            const isArrayItem = trimmed.startsWith('- ');

            let key: string | undefined;
            let value: string | undefined;
            let isArrayValue = false;
            let arrayValues: string[] | undefined;

            if (!isComment && trimmed.includes(':')) {
                const colonIndex = trimmed.indexOf(':');
                const rawKey = trimmed.slice(isArrayItem ? 2 : 0, colonIndex).trim();
                const rawValue = trimmed.slice(colonIndex + 1).trim();
                key = rawKey;
                value = rawValue || undefined;

                if (value && value.startsWith('[') && value.endsWith(']')) {
                    isArrayValue = true;
                    const inner = value.slice(1, -1);
                    arrayValues = inner.split(',').map(v => v.trim()).filter(v => v);
                }
            }

            if (indent === 0 && !isArrayItem) currentClauseType = undefined;
            // Basic clause type tracking (heuristic)
            if (isArrayItem && key && REQUIRED_KEYS.includes(key)) {
                currentClauseType = key;
            }

            let validationState: ParsedLine['validationState'] = 'complete';
            let isInvalidValue = false;

            if (key) {
                if (METADATA_KEYS.includes(key)) validationState = 'metadata';
                else if (REQUIRED_KEYS.includes(key)) validationState = value ? 'complete' : 'required-incomplete';
                else if (!value && key !== 'must' && key !== 'should' && key !== 'mustNot') validationState = 'optional-incomplete';

                if (value && value.startsWith('~') && value.endsWith('~')) {
                    isInvalidValue = true;
                    validationState = 'invalid';
                }
            }

            return {
                id: `line-${index}`,
                raw, indent, key, value, isComment, isArrayItem, lineNumber: index,
                clauseType: currentClauseType, validationState, isInvalidValue, isArrayValue, arrayValues
            };
        });
    }, []);

    const linesToJaml = useCallback((linesList: ParsedLine[]): string => linesList.map(l => l.raw).join('\n'), []);

    useEffect(() => {
        const text = initialJaml || DEFAULT_JAML;
        // Prevent feedback loop: If the incoming text matches what we already have, don't re-parse/reset.
        // This is crucial when onJamlChange -> parent -> initialJaml cycle exists.
        if (lines.length > 0 && linesToJaml(lines) === text) return;

        setLines(parseJamlToLines(text));
    }, [initialJaml, parseJamlToLines, lines, linesToJaml]);

    const updateLineValue = useCallback((lineId: string, part: 'key' | 'value', newValue: string) => {
        const newLines = lines.map(line => {
            if (line.id !== lineId) return line;

            const indent = ' '.repeat(line.indent);
            const prefix = line.isArrayItem ? '- ' : '';
            let newRaw = line.raw;

            if (part === 'value' && line.key) {
                newRaw = `${indent}${prefix}${line.key}: ${newValue}`;
            } else if (part === 'key') {
                const valuePart = line.value ? `: ${line.value}` : ':';
                newRaw = `${indent}${prefix}${newValue}${valuePart}`;
            }

            let isArrayValue = false;
            let arrayValues: string[] | undefined;
            if (newValue && newValue.startsWith('[') && newValue.endsWith(']')) {
                isArrayValue = true;
                arrayValues = newValue.slice(1, -1).split(',').map(v => v.trim()).filter(v => v);
            }

            return { ...line, raw: newRaw, [part]: newValue, isArrayValue, arrayValues };
        });

        setLines(newLines);
        const txt = linesToJaml(newLines);
        if (onJamlChange) {
            try { onJamlChange(txt, yaml.load(txt), true); } catch { onJamlChange(txt, null, false); }
        }
    }, [lines, linesToJaml, onJamlChange]);

    const updateArrayItem = useCallback((lineId: string, arrayIndex: number, newValue: string) => {
        const newLines = lines.map(line => {
            if (line.id !== lineId || !line.arrayValues) return line;
            const newArrayValues = [...line.arrayValues];
            newArrayValues[arrayIndex] = newValue;
            const newArrayStr = `[${newArrayValues.join(', ')}]`;
            const indent = ' '.repeat(line.indent);
            const prefix = line.isArrayItem ? '- ' : '';
            const newRaw = `${indent}${prefix}${line.key}: ${newArrayStr}`;
            return { ...line, raw: newRaw, value: newArrayStr, arrayValues: newArrayValues };
        });

        setLines(newLines);
        const txt = linesToJaml(newLines);
        if (onJamlChange) { try { onJamlChange(txt, yaml.load(txt), true); } catch { onJamlChange(txt, null, false); } }
    }, [lines, linesToJaml, onJamlChange]);

    const addArrayItem = useCallback((lineId: string, newValue: string) => {
        const newLines = lines.map(line => {
            if (line.id !== lineId) return line;
            const newArrayValues = [...(line.arrayValues || []), newValue];
            const newArrayStr = `[${newArrayValues.join(', ')}]`;
            const indent = ' '.repeat(line.indent);
            const prefix = line.isArrayItem ? '- ' : '';
            const newRaw = `${indent}${prefix}${line.key}: ${newArrayStr}`;
            return { ...line, raw: newRaw, value: newArrayStr, arrayValues: newArrayValues, isArrayValue: true };
        });

        setLines(newLines);
        const txt = linesToJaml(newLines);
        if (onJamlChange) { try { onJamlChange(txt, yaml.load(txt), true); } catch { onJamlChange(txt, null, false); } }
    }, [lines, linesToJaml, onJamlChange]);

    const removeArrayItem = useCallback((lineId: string, arrayIndex: number) => {
        const newLines = lines.map(line => {
            if (line.id !== lineId || !line.arrayValues) return line;
            const newArrayValues = line.arrayValues.filter((_, i) => i !== arrayIndex);
            const newArrayStr = newArrayValues.length > 0 ? `[${newArrayValues.join(', ')}]` : '';
            const indent = ' '.repeat(line.indent);
            const prefix = line.isArrayItem ? '- ' : '';
            const newRaw = `${indent}${prefix}${line.key}: ${newArrayStr}`;
            return { ...line, raw: newRaw, value: newArrayStr || undefined, arrayValues: newArrayValues.length > 0 ? newArrayValues : undefined, isArrayValue: newArrayValues.length > 0 };
        });

        setLines(newLines);
        const txt = linesToJaml(newLines);
        if (onJamlChange) { try { onJamlChange(txt, yaml.load(txt), true); } catch { onJamlChange(txt, null, false); } }
    }, [lines, linesToJaml, onJamlChange]);

    const addLineAfter = useCallback((afterLineId: string, content: string) => {
        const index = lines.findIndex(l => l.id === afterLineId);
        if (index === -1) return;
        const currentLine = lines[index];
        const newLineRaw = ' '.repeat(currentLine.indent) + content;
        const newParsed = parseJamlToLines(newLineRaw)[0];
        const newLine: ParsedLine = { ...newParsed, id: `line-new-${Date.now()}`, clauseType: currentLine.clauseType };

        const newLines = [...lines];
        newLines.splice(index + 1, 0, newLine);
        const renumbered = newLines.map((l, i) => ({ ...l, lineNumber: i, id: `line-${i}` })); // simplified ID update

        setLines(renumbered);
        const txt = linesToJaml(renumbered);
        if (onJamlChange) { try { onJamlChange(txt, yaml.load(txt), true); } catch { onJamlChange(txt, null, false); } }
    }, [lines, linesToJaml, onJamlChange, parseJamlToLines]);

    const deleteLine = useCallback((lineId: string) => {
        const filtered = lines.filter(l => l.id !== lineId);
        const renumbered = filtered.map((l, i) => ({ ...l, lineNumber: i, id: `line-${i}` }));

        setLines(renumbered);
        const txt = linesToJaml(renumbered);
        if (onJamlChange) { try { onJamlChange(txt, yaml.load(txt), true); } catch { onJamlChange(txt, null, false); } }
    }, [lines, linesToJaml, onJamlChange]);

    // -- Render --

    const maxKeyLengthByIndent = useMemo(() => {
        const byIndent: Record<number, number> = {};
        for (const line of lines) {
            if (line.key) {
                // Cap the alignment width at 12 characters to prevent huge gaps
                byIndent[line.indent] = Math.min(Math.max(byIndent[line.indent] || 0, line.key.length), 12);
            }
        }
        return byIndent;
    }, [lines]);

    return (
        <div
            ref={editorRef}
            className={`flex flex-col bg-[#0f1416] text-white font-mono text-[13px] leading-[1.8] outline-none rounded-md p-4 overflow-auto min-h-[500px] border border-white/5 ${className}`}
            tabIndex={0}
        >
            <div className="flex flex-col gap-0.5">
                {lines.map((line, index) => (
                    <JamlLine
                        key={line.id}
                        line={line}
                        keyWidth={maxKeyLengthByIndent[line.indent] || 8}
                        isEditing={editingLineId === line.id}
                        editingPart={editingLineId === line.id ? editingPart : null}
                        editingArrayIndex={editingLineId === line.id ? editingArrayIndex : null}
                        onStartEdit={(part, idx) => {
                            setEditingLineId(line.id);
                            setEditingPart(part);
                            setEditingArrayIndex(idx ?? null);
                            setFocusedLineIndex(index);
                        }}
                        onEndEdit={() => {
                            setEditingLineId(null);
                            setEditingPart(null);
                            setEditingArrayIndex(null);
                        }}
                        onChange={(part, val) => updateLineValue(line.id, part, val)}
                        onArrayItemChange={(idx, val) => updateArrayItem(line.id, idx, val)}
                        onArrayItemAdd={(val) => addArrayItem(line.id, val)}
                        onArrayItemRemove={(idx) => removeArrayItem(line.id, idx)}
                        onDelete={() => deleteLine(line.id)}
                        onAddLine={(content) => addLineAfter(line.id, content)}
                    />
                ))}
            </div>
            <div className="mt-4 p-2 bg-black/40 rounded border border-white/5 flex flex-wrap gap-4 text-[12px] text-white/50 font-mono">
                <span className="flex items-center gap-1.5"><span className="jaml-legend-dot jaml-legend-dot--red"></span> required</span>
                <span className="flex items-center gap-1.5"><span className="jaml-legend-dot jaml-legend-dot--blue"></span> optional</span>
                <span className="flex items-center gap-1.5"><span className="jaml-legend-dot jaml-legend-dot--green"></span> complete</span>
                <span className="flex items-center gap-1.5"><span className="jaml-legend-dot jaml-legend-dot--purple"></span> metadata</span>
                <span className="ml-auto opacity-40">Click to edit • Tab to navigate</span>
            </div>
        </div>
    );
}

// --- Single Line Component ---

interface JamlLineProps {
    line: ParsedLine;
    keyWidth: number;
    isEditing: boolean;
    editingPart: 'key' | 'value' | 'arrayItem' | null;
    editingArrayIndex: number | null;
    onStartEdit: (part: 'key' | 'value' | 'arrayItem', idx?: number) => void;
    onEndEdit: () => void;
    onChange: (part: 'key' | 'value', val: string) => void;
    onArrayItemChange: (idx: number, val: string) => void;
    onArrayItemAdd: (val: string) => void;
    onArrayItemRemove: (idx: number) => void;
    onDelete: () => void;
    onAddLine: (content: string) => void;
}

function JamlLine({
    line,
    keyWidth,
    isEditing,
    editingPart,
    editingArrayIndex,
    onStartEdit,
    onEndEdit,
    onChange,
    onArrayItemChange,
    onArrayItemAdd,
    onArrayItemRemove,
    onDelete,
    onAddLine
}: JamlLineProps) {
    const [, setHovered] = useState(false);
    const [suggestions, setSuggestions] = useState<CompletionData[]>([]);
    const [selectedIndex, setSelectedIndex] = useState(0);
    const [localValue, setLocalValue] = useState('');
    const inputRef = useRef<HTMLInputElement>(null);
    const targetRef = useRef<HTMLDivElement>(null);

    const floatingCoords = useFloatingPosition(targetRef, isEditing && suggestions.length > 0);

    // Colors
    const getBaseColor = () => {
        switch (line.validationState) {
            case 'required-incomplete': return COLORS.red;
            case 'optional-incomplete': return COLORS.blue;
            case 'complete': return COLORS.green;
            case 'invalid': return COLORS.red;
            case 'metadata': return COLORS.purple;
            default: return '#888';
        }
    };

    const getBrightColor = () => {
        switch (line.validationState) {
            case 'required-incomplete': return COLORS.red;
            case 'optional-incomplete': return COLORS.blue;
            case 'complete': return COLORS.green;
            case 'invalid': return COLORS.red;
            case 'metadata': return COLORS.purple;
            default: return '#FFF';
        }
    };

    // Suggestions Logic
    useEffect(() => {
        if (isEditing && editingPart) {
            let textContext = line.raw;
            if (editingPart === 'value' && line.key) {
                textContext = `${line.key}: ${localValue}`;
            } else if (editingPart === 'key') {
                textContext = localValue;
            }

            const sugs = JamlCompletionService.getCompletions(textContext);
            setSuggestions(sugs.slice(0, 10));
            setSelectedIndex(0);
        }
    }, [isEditing, editingPart, localValue, line.raw, line.key]);

    // Input Focus
    useEffect(() => {
        if (isEditing && inputRef.current) {
            inputRef.current.focus();
            if (editingPart === 'key') setLocalValue(line.key || '');
            else if (editingPart === 'value') setLocalValue((line.value || '').replace(/^~|~$/g, ''));
            else if (editingPart === 'arrayItem' && editingArrayIndex !== null) setLocalValue(line.arrayValues?.[editingArrayIndex] || '');
            else setLocalValue('');
        }
    }, [isEditing, editingPart, editingArrayIndex, line]);


    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            const finalVal = (suggestions.length > 0 && selectedIndex >= 0) ? suggestions[selectedIndex].text : localValue;

            if (editingPart === 'key') onChange('key', finalVal);
            else if (editingPart === 'value') onChange('value', finalVal);
            else if (editingPart === 'arrayItem') {
                if (editingArrayIndex! >= (line.arrayValues?.length || 0)) onArrayItemAdd(finalVal);
                else onArrayItemChange(editingArrayIndex!, finalVal);
            }
            onEndEdit();
        } else if (e.key === 'ArrowDown') {
            e.preventDefault();
            setSelectedIndex(Math.min(selectedIndex + 1, suggestions.length - 1));
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            setSelectedIndex(Math.max(selectedIndex - 1, 0));
        } else if (e.key === 'Escape') {
            onEndEdit();
        } else if (e.key === 'Backspace' && localValue === '' && editingPart === 'arrayItem') {
            onArrayItemRemove(editingArrayIndex!);
            onEndEdit();
        }
    };

    if (line.isComment) {
        return <div className="pl-8 text-stone-400 italic text-xs py-0.5">{line.raw}</div>;
    }
    if (!line.key && !line.raw.trim()) {
        return <div className="h-5" />;
    }
    if ((line.key === 'must' || line.key === 'should' || line.key === 'mustNot') && !line.value) {
        const color = line.key === 'must' ? COLORS.red : COLORS.blue;
        return <div className="jaml-clause-header pl-8 py-1 text-[16px] border-b border-white/10 mt-4 mb-2 uppercase tracking-widest" {...{ style: { '--jaml-color': color } as React.CSSProperties }}>{line.raw}</div>
    }

    const indentSpaces = ' '.repeat(line.indent);
    const prefix = line.isArrayItem ? '- ' : '';

    // Helper: popover CSS vars from floating coords
    const popoverVars = floatingCoords ? {
        '--popover-top': `${floatingCoords.top}px`,
        '--popover-left': `${floatingCoords.left}px`,
        '--popover-transform': floatingCoords.position === 'top' ? 'translateY(-100%)' : 'none',
    } as React.CSSProperties : {};

    return (
        <div className="relative flex items-center py-0.5 group" onMouseEnter={() => setHovered(true)} onMouseLeave={() => setHovered(false)}>
            {/* Delete Zone */}
            <div
                className="w-6 h-full flex items-center justify-center cursor-pointer opacity-0 group-hover:opacity-100 transition-opacity"
                onClick={(e) => { e.stopPropagation(); onDelete(); }}
            >
                <Minus size={12} className="text-orange-500 hover:bg-orange-100 rounded" />
            </div>

            {/* Indent & Prefix */}
            <span className="whitespace-pre text-stone-400">{indentSpaces}{prefix}</span>

            {/* Key */}
            {line.key && (
                <div className="relative">
                    <div
                        ref={editingPart === 'key' ? targetRef : null}
                        onClick={(e) => { e.stopPropagation(); onStartEdit('key'); }}
                        className="jaml-block jaml-block--start"
                        {...{
                            style: {
                                '--jaml-min-w': `${keyWidth}ch`,
                                '--jaml-color': (isEditing && editingPart === 'key') ? getBrightColor() : getBaseColor(),
                                '--jaml-bg': (isEditing && editingPart === 'key') ? `${getBrightColor()}15` : 'transparent',
                            } as React.CSSProperties
                        }}
                    >
                        {isEditing && editingPart === 'key' ? (
                            <input
                                ref={inputRef}
                                title="Edit key"
                                value={localValue}
                                onChange={e => setLocalValue(e.target.value)}
                                onKeyDown={handleKeyDown}
                                onBlur={() => setTimeout(onEndEdit, 200)}
                                className="jaml-inline-input"
                                {...{ style: { '--jaml-color': getBrightColor() } as React.CSSProperties }}
                            />
                        ) : line.key}
                    </div>
                    {/* Popover */}
                    {isEditing && editingPart === 'key' && suggestions.length > 0 && floatingCoords && createPortal(
                        <div className="jaml-suggestion-popover" {...{ style: popoverVars }}>
                            <SuggestionList suggestions={suggestions} selectedIndex={selectedIndex} onHover={setSelectedIndex} onSelect={(val) => {
                                onChange('key', val);
                                onEndEdit();
                            }} />
                        </div>,
                        document.body
                    )}
                </div>
            )}

            {line.key && <span className="text-stone-400 mr-1">:</span>}

            {/* Value */}
            {line.key && (
                line.isArrayValue && line.arrayValues ? (
                    line.key === 'antes' ? (
                        <AntesToggle
                            values={line.arrayValues}
                            onToggle={(val) => {
                                const idx = line.arrayValues!.indexOf(val);
                                if (idx >= 0) onArrayItemRemove(idx);
                                else onArrayItemAdd(val);
                            }}
                            onStartEdit={() => onStartEdit('arrayItem', 0)}
                            color={getBrightColor()}
                            darkColor={getBaseColor()}
                        />
                    ) : (
                        <div className="flex gap-0.5 items-center">
                            {line.arrayValues.map((val: string, idx: number) => (
                                <div
                                    key={idx}
                                    onClick={(e) => { e.stopPropagation(); onStartEdit('arrayItem', idx); }}
                                    className="jaml-block"
                                    {...{
                                        style: {
                                            '--jaml-min-w': '24px',
                                            '--jaml-bg': `${getBaseColor()}15`,
                                            '--jaml-border': `1px solid ${getBaseColor()}40`,
                                            '--jaml-color': getBaseColor(),
                                        } as React.CSSProperties
                                    }}
                                >
                                    {isEditing && editingPart === 'arrayItem' && editingArrayIndex === idx ? (
                                        <input
                                            ref={inputRef}
                                            title="Edit array item"
                                            value={localValue}
                                            onChange={e => setLocalValue(e.target.value)}
                                            onKeyDown={handleKeyDown}
                                            onBlur={() => setTimeout(onEndEdit, 200)}
                                            className="bg-transparent w-[3ch] text-center outline-none"
                                        />
                                    ) : val}
                                </div>
                            ))}
                            <div
                                onClick={(e) => { e.stopPropagation(); onStartEdit('arrayItem', line.arrayValues!.length); }}
                                className="jaml-block"
                                {...{
                                    style: {
                                        '--jaml-min-w': '24px',
                                        '--jaml-bg': `${COLORS.green}15`,
                                        '--jaml-border': `1px solid ${COLORS.green}40`,
                                        '--jaml-color': COLORS.green,
                                    } as React.CSSProperties
                                }}
                            >
                                <Plus size={12} />
                            </div>
                        </div>
                    )
                ) : (
                    <div className="relative">
                        <div
                            ref={editingPart === 'value' ? targetRef : null}
                            onClick={(e) => { e.stopPropagation(); onStartEdit('value'); }}
                            className="jaml-block"
                            {...{
                                style: {
                                    '--jaml-min-w': line.value ? undefined : '60px',
                                    '--jaml-color': line.isInvalidValue ? COLORS.red : (line.value ? getBaseColor() : COLORS.red),
                                    '--jaml-bg': line.isInvalidValue ? `${COLORS.red}15` : (line.value ? `${getBaseColor()}10` : `${COLORS.red}08`),
                                    '--jaml-border': `1px solid ${line.isInvalidValue ? `${COLORS.red}60` : (line.value ? `${getBaseColor()}40` : `${COLORS.red}40`)}`,
                                    '--jaml-text-decoration': line.isInvalidValue ? 'line-through' : 'none',
                                } as React.CSSProperties
                            }}
                        >
                            {isEditing && editingPart === 'value' ? (
                                <input
                                    ref={inputRef}
                                    title="Edit value"
                                    value={localValue}
                                    onChange={e => setLocalValue(e.target.value)}
                                    onKeyDown={handleKeyDown}
                                    onBlur={() => setTimeout(onEndEdit, 200)}
                                    className="jaml-inline-input jaml-inline-input--sized"
                                    {...{
                                        style: {
                                            '--jaml-color': getBaseColor(),
                                            '--jaml-input-w': `${Math.max(localValue.length, 4)}ch`,
                                        } as React.CSSProperties
                                    }}
                                />
                            ) : (line.isInvalidValue ? line.value?.replace(/~/g, '') : (line.value || '???'))}
                        </div>
                        {isEditing && editingPart === 'value' && suggestions.length > 0 && floatingCoords && createPortal(
                            <div className="jaml-suggestion-popover" {...{ style: popoverVars }}>
                                <SuggestionList suggestions={suggestions} selectedIndex={selectedIndex} onHover={setSelectedIndex} onSelect={(val) => {
                                    onChange('value', val);
                                    onEndEdit();
                                }} />
                            </div>,
                            document.body
                        )}
                    </div>
                )
            )}
        </div>
    );
}
