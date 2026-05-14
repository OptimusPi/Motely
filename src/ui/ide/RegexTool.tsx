import React, { useState, useMemo } from 'react';
import { cn } from '../../lib/utils';
import { Search, AlertCircle, CheckCircle2 } from 'lucide-react';

export function RegexTool({ className }: { className?: string }) {
    const [pattern, setPattern] = useState('[a-z]+');
    const [flags, setFlags] = useState('g');
    const [testString, setTestString] = useState('hello world 123');

    const result = useMemo(() => {
        try {
            const regex = new RegExp(pattern, flags);
            const matches = [...testString.matchAll(regex)];
            return {
                valid: true,
                matches: matches.map(m => ({
                    text: m[0],
                    index: m.index,
                    groups: m.groups
                }))
            };
        } catch (e) {
            return {
                valid: false,
                error: (e as Error).message
            };
        }
    }, [pattern, flags, testString]);

    return (
        <div className={cn("flex flex-col gap-4 p-4 rounded-xl border border-white/10 bg-black/40 backdrop-blur-md", className)}>
            <div className="flex items-center gap-2 mb-2 border-b border-white/10 pb-2">
                <Search size={18} className="text-blue-400" />
                <h3 className="text-sm font-semibold tracking-wider text-white/80">REGEX TESTER</h3>
            </div>
            
            <div className="flex flex-col gap-2">
                <label className="text-[10px] uppercase tracking-widest text-white/40">Expression</label>
                <div className="flex items-center gap-2 bg-black/50 border border-white/10 rounded-lg p-2 font-mono text-sm">
                    <span className="text-white/30">/</span>
                    <input 
                        className="flex-1 bg-transparent outline-none text-blue-300 placeholder:text-white/20"
                        value={pattern}
                        onChange={e => setPattern(e.target.value)}
                        placeholder="pattern"
                        spellCheck={false}
                    />
                    <span className="text-white/30">/</span>
                    <input 
                        className="w-12 bg-transparent outline-none text-orange-300 placeholder:text-white/20"
                        value={flags}
                        onChange={e => setFlags(e.target.value)}
                        placeholder="flags"
                        spellCheck={false}
                    />
                </div>
                {!result.valid && (
                    <div className="flex items-center gap-1.5 text-red-400 text-xs mt-1">
                        <AlertCircle size={12} />
                        <span>{result.error}</span>
                    </div>
                )}
            </div>

            <div className="flex flex-col gap-2 mt-2">
                <label className="text-[10px] uppercase tracking-widest text-white/40">Test String</label>
                <textarea 
                    className="w-full h-24 bg-black/50 border border-white/10 rounded-lg p-3 font-mono text-sm text-white/80 outline-none focus:border-blue-500/50 resize-none"
                    value={testString}
                    onChange={e => setTestString(e.target.value)}
                    placeholder="Enter text to test..."
                    spellCheck={false}
                />
            </div>

            <div className="flex flex-col gap-2 mt-2">
                <div className="flex items-center justify-between">
                    <label className="text-[10px] uppercase tracking-widest text-white/40">Matches</label>
                    {result.valid && (
                        <span className="text-[10px] bg-white/5 px-2 py-0.5 rounded-full text-white/60">
                            {result.matches?.length || 0} found
                        </span>
                    )}
                </div>
                
                <div className="flex flex-col gap-1.5 max-h-40 overflow-y-auto pr-2">
                    {result.valid && result.matches && result.matches.length > 0 ? (
                        result.matches.map((m, i) => (
                            <div key={i} className="flex flex-col gap-1 bg-white/5 rounded px-3 py-2 text-xs font-mono">
                                <div className="flex items-center justify-between">
                                    <span className="text-green-300">"{m.text}"</span>
                                    <span className="text-white/30 text-[10px]">Index: {m.index}</span>
                                </div>
                                {m.groups && Object.keys(m.groups).length > 0 && (
                                    <div className="mt-1 pt-1 border-t border-white/5 text-[10px] text-white/50">
                                        Groups: {JSON.stringify(m.groups)}
                                    </div>
                                )}
                            </div>
                        ))
                    ) : result.valid ? (
                        <div className="text-xs text-white/30 italic px-2">No matches found</div>
                    ) : null}
                </div>
            </div>
        </div>
    );
}
