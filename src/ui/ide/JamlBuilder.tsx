"use client";

import React, { useState, useRef, useEffect } from 'react';

import { useJamlFilter } from '../../lib/hooks/useJamlFilter';
import { DECK_OPTIONS, STAKE_OPTIONS } from '../../lib/data/constants';
import JamlEditor from './JamlEditor';

import { cn } from '../../lib/utils';
import { X, Edit2, Loader2, Search, Square, Copy, RotateCcw, Flame, Sparkles } from 'lucide-react';
import { Motely } from 'motely-wasm';
import { AgnosticSeedCard } from './AgnosticSeedCard';
import { WasmStatus } from './WasmStatus';
import { JAML_PRESETS } from '../../lib/jaml/jamlPresets.js';

export default function JamlBuilder() {
    const {
        filter,
        jamlText,
        setFromJaml,
        updateFilter,
        addClause,
        editClause,
        deleteClause,
        editingClause,
        setEditingClause
    } = useJamlFilter();

    const [isSearching, setIsSearching] = useState(false);
    const [searchResults, setSearchResults] = useState<Motely.MotelyWasmSearchResult[]>([]);
    const [searchError, setSearchError] = useState<string | null>(null);
    const [seedsProcessed, setSeedsProcessed] = useState(0);
    const stopRef = useRef(false);
    const searchCleanupRef = useRef<(() => void) | null>(null);

    useEffect(() => {
        return () => {
            if (searchCleanupRef.current) searchCleanupRef.current();
            handleStop();
        };
    }, []);
    const pollRef = useRef<NodeJS.Timeout | null>(null);
    const workerRef = useRef<Worker | null>(null);

    const handleSearch = async () => {
        // Stop any existing search
        if (isSearching) {
            handleStop();
        }

        setIsSearching(true);
        setSearchError(null);
        setSearchResults([]);
        setSeedsProcessed(0);
        stopRef.current = false;

        try {
            // Import worker using Vite's ?worker syntax
            const Worker = (await import('../../hooks/searchWorker?worker')).default;
            const worker = new Worker();
            workerRef.current = worker;

            worker.onmessage = (e) => {
                const event = e.data;
                if (event.type === 'result') {
                    setSearchResults(prev => {
                        // Prevent duplicates
                        if (prev.some(r => r.seed === event.seed)) return prev;
                        return [...prev, {
                            seed: event.seed,
                            score: event.score,
                            tallies: event.tallyColumns || []
                        } as any];
                    });
                } else if (event.type === 'progress') {
                    setSeedsProcessed(Number(event.searched) || 0);
                } else if (event.type === 'complete') {
                    setIsSearching(false);
                    worker.terminate();
                    workerRef.current = null;
                } else if (event.type === 'error') {
                    setSearchError(event.message || 'Unknown error');
                    setIsSearching(false);
                    worker.terminate();
                    workerRef.current = null;
                } else if (event.type === 'cancelled') {
                    worker.terminate();
                    workerRef.current = null;
                }
            };

            worker.postMessage({
                type: 'start',
                mode: 'random',
                jaml: jamlText,
                count: 1000 // A default random count, adjust as needed
            });
        } catch (e: any) {
            console.error("Local search error:", e);
            setSearchError(e.message || 'Local search failed');
            setIsSearching(false);
        }
    };

    const handleStop = async () => {
        stopRef.current = true;
        setIsSearching(false);

        if (workerRef.current) {
            workerRef.current.postMessage({ type: 'stop' });
            // The worker will reply with 'cancelled' and terminate itself
        }

        if (searchCleanupRef.current) {
            searchCleanupRef.current();
            searchCleanupRef.current = null;
        }
    };


    const handleCopyJaml = () => {
        if (typeof navigator !== 'undefined' && navigator.clipboard) {
            navigator.clipboard.writeText(jamlText).catch(err => {
                console.error('Failed to copy text: ', err);
            });
        }
    };

    const handleResetJaml = () => {
        if (confirm("Reset editor to default JAML?")) {
            setFromJaml(""); // useJamlFilter handles the default
        }
    };

    return (
        <div className="w-full max-w-[1400px] mx-auto p-4 flex-1 overflow-hidden flex flex-col gap-4 bg-[var(--balatro-black)]/40">



            <div className="flex flex-col lg:grid lg:grid-cols-[1fr_400px] gap-6 h-full overflow-hidden">

                {/* LEFT COLUMN: Interactive God-Like Editor (Main Workspace) */}
                <div className="flex flex-col gap-6 overflow-hidden min-h-0">

                    {/* CONTROL BAR */}
                    <div className="balatro-panel border-[var(--balatro-grey)] bg-black/40 py-4">
                        <div className="flex justify-between items-center">
                            <div className="flex gap-4 items-center">
                                <div>
                                    <h2 className="text-white text-xl font-header mb-1 uppercase tracking-widest leading-none">JAML IDE</h2>
                                    <p className="text-white/60 font-pixel text-[10px] uppercase tracking-wide">Ritual Factory v2.0</p>
                                </div>
                                <div className="h-8 w-px bg-white/10 mx-2" />
                                <div className="flex gap-1">
                                    <button
                                        onClick={handleCopyJaml}
                                        className="p-2 hover:bg-white/5 rounded text-white/30 hover:text-white/80 transition-colors"
                                        title="Copy JAML"
                                    >
                                        <Copy size={16} />
                                    </button>
                                    <button
                                        onClick={handleResetJaml}
                                        className="p-2 hover:bg-white/5 rounded text-white/30 hover:text-white/80 transition-colors"
                                        title="Reset Editor"
                                    >
                                        <RotateCcw size={16} />
                                    </button>
                                </div>
                            </div>
                            <div className="flex gap-2 items-center">
                                {isSearching && (
                                    <div className="flex items-center gap-2 mr-2 px-3 py-1 bg-black/40 rounded border border-white/5 font-pixel text-[10px] text-white/50">
                                        <Loader2 size={12} className="animate-spin text-[var(--balatro-gold)]" />
                                        <span>SEARCHING...</span>
                                    </div>
                                )}
                                <button
                                    onClick={handleSearch}
                                    disabled={isSearching}
                                    className="balatro-button balatro-button-gold !py-2 !px-4 !text-sm flex items-center gap-2"
                                >
                                    {isSearching ? <Loader2 size={14} className="animate-spin" /> : <Search size={14} />}
                                    RUN SEARCH
                                </button>
                                {isSearching && (
                                    <button
                                        onClick={handleStop}
                                        className="balatro-button balatro-button-red !py-2 !px-4 !text-sm"
                                    >
                                        STOP
                                    </button>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* THE GOD-LIKE EDITOR */}
                    <div className="flex-1 min-h-0 bg-black/20 rounded-2xl border border-white/5 overflow-hidden shadow-inner flex flex-col">
                        <JamlEditor
                            initialJaml={jamlText}
                            onJamlChange={(val: string) => setFromJaml(val)}
                            className="flex-1"
                        />
                    </div>
                </div>

                {/* RIGHT COLUMN: Search Results (Sidebar) */}
                <div className="flex flex-col gap-6 overflow-hidden pb-10">
                    {/* SEARCH RESULTS */}
                    <div className="h-full balatro-panel border-[var(--balatro-gold)] overflow-hidden flex flex-col">
                        <div className="flex justify-between items-center mb-4">
                            <div className="flex items-center gap-3">
                                <h3 className="text-[var(--balatro-gold)] text-xl font-header drop-shadow-md uppercase">
                                    Results {searchResults.length > 0 && `(${searchResults.length})`}
                                </h3>
                                {isSearching && (
                                    <div className="flex items-center gap-2 font-pixel text-[10px] text-[var(--balatro-blue)] animate-pulse">
                                        <Sparkles size={12} className="animate-spin" />
                                        <span>{seedsProcessed}</span>
                                    </div>
                                )}
                            </div>
                            {isSearching && <Loader2 size={16} className="animate-spin text-[var(--balatro-blue)]" />}
                        </div>

                        {/* Spectral Progress Bar */}
                        {isSearching && (
                            <div className="w-full h-1 bg-black/40 rounded-full mb-4 overflow-hidden border border-white/5">
                                <div
                                    className="h-full bg-gradient-to-r from-blue-500 via-purple-500 to-pink-500 animate-gradient-x transition-all duration-300"
                                    style={{ width: `${Math.min((searchResults.length / 50) * 100, 100)}%` }}
                                />
                            </div>
                        )}

                        <div className="flex-1 overflow-y-auto custom-scrollbar p-1">
                            {searchResults.length > 0 ? (
                                <div className="grid grid-cols-1 gap-4">
                                    {searchResults.map((result: Motely.MotelyWasmSearchResult) => (
                                        <AgnosticSeedCard
                                            key={result.seed}
                                            seed={result.seed}
                                            result={result}
                                            filter={filter}
                                            className="!scale-95 !origin-top"
                                        />
                                    ))}
                                </div>
                            ) : (
                                <div className="h-full flex flex-col items-center justify-center italic font-pixel text-sm border-2 border-dashed border-white/5 rounded-2xl p-4 text-center">
                                    {searchError ? (
                                        <div className="text-[var(--balatro-red)] bg-red-950/20 p-4 rounded text-center">
                                            <div className="font-header text-lg mb-1">SEARCH FAILED</div>
                                            <div className="opacity-60">{searchError}</div>
                                        </div>
                                    ) : (
                                        <div className="opacity-30 text-white/50">
                                            {isSearching ? 'SEEKING...' : 'RESULTS WILL APPEAR HERE'}
                                        </div>
                                    )}
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>

            <WasmStatus />
        </div>
    );
}
