"use client";

import React, { useState, useRef, useEffect } from 'react';

import { useJamlFilter } from '../../lib/hooks/useJamlFilter';

import JamlEditor from './JamlEditor';


import { Loader2, Search, Copy, RotateCcw, Sparkles, Palette, List } from 'lucide-react';
import type { MotelyScoredSeedResult } from 'motely-wasm/motely';
import { AgnosticSeedCard } from './AgnosticSeedCard';
import { WasmStatus } from './WasmStatus';

// Aesthetic options matching Motely.Filters.JamlAesthetic
type AestheticOption = { value: number; label: string; icon: React.ReactNode };
const AESTHETIC_OPTIONS: AestheticOption[] = [
  { value: 0, label: 'Palindrome', icon: <span className="text-xs">↔</span> },
  { value: 1, label: 'Psychosis', icon: <span className="text-xs">◎</span> },
  { value: 2, label: 'Gross', icon: <span className="text-xs">🤢</span> },
  { value: 3, label: 'NSFW', icon: <span className="text-xs">🔞</span> },
  { value: 4, label: 'Funny', icon: <span className="text-xs">😂</span> },
  { value: 5, label: 'Balatro', icon: <span className="text-xs">🃏</span> },
];

// Parse seeds array from raw JAML text
function parseSeedsFromJaml(jamlText: string): string[] {
  const seeds: string[] = [];
  const lines = jamlText.split('\n');
  let inSeedsSection = false;

  for (const line of lines) {
    const trimmed = line.trim();

    // Check if we're entering seeds section
    if (trimmed === 'seeds:') {
      inSeedsSection = true;
      continue;
    }

    // Exit seeds section when we hit another top-level key or blank line after seeds
    if (inSeedsSection) {
      if (trimmed === '' || /^[a-zA-Z]+:/.test(trimmed) && !line.startsWith(' ') && !line.startsWith('-')) {
        inSeedsSection = false;
        continue;
      }

      // Parse seed entry
      const seedMatch = trimmed.match(/^-\s*([A-Z0-9]+)$/);
      if (seedMatch) {
        seeds.push(seedMatch[1]);
      }
    }
  }

  return seeds;
}


export default function JamlBuilder() {

    const {
        jamlText,
        setFromJaml
    } = useJamlFilter();

    const [isSearching, setIsSearching] = useState(false);
    const [searchResults, setSearchResults] = useState<MotelyScoredSeedResult[]>([]);
    const [searchError, setSearchError] = useState<string | null>(null);
    const [seedsProcessed, setSeedsProcessed] = useState(0);
    const [searchMode, setSearchMode] = useState<'aesthetic' | 'seedlist'>('aesthetic');
    const [selectedAesthetic, setSelectedAesthetic] = useState<number>(0);
    const stopRef = useRef(false);
    const searchCleanupRef = useRef<(() => void) | null>(null);

    const workerRef = useRef<Worker | null>(null);

    // Parse seeds from JAML for seedlist mode
    const parsedSeeds = parseSeedsFromJaml(jamlText);
    const hasSeedsInJaml = parsedSeeds.length > 0;

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

    useEffect(() => {
        return () => {
            if (searchCleanupRef.current) searchCleanupRef.current();
            handleStop();
        };
    }, []);

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
                        if (prev.some(r => r.seed === event.seed)) return prev;
                        const tallies = new Int32Array(event.tallyColumns || []);
                        const next: MotelyScoredSeedResult = {
                            seed: event.seed,
                            score: event.score,
                            tallies,
                        };
                        return [...prev, next];
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

            const seeds = parseSeedsFromJaml(jamlText);

            worker.postMessage({
                type: 'start',
                mode: searchMode,
                jaml: jamlText,
                aesthetic: selectedAesthetic,
                seeds: seeds.length > 0 ? seeds : undefined,
                count: 10000 // Default aesthetic budget
            });
        } catch (e) {
            const error = e as Error;
            console.error("Local search error:", error);
            setSearchError(error.message || 'Local search failed');
            setIsSearching(false);
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
                                    <h2 className="text-white text-xl font-header mb-1 tracking-widest leading-none">Jaml ide</h2>
                                    <p className="text-white/60 font-pixel text-[10px] tracking-wide">Ritual factory v2.0</p>
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
                                {/* Search Mode Selector */}
                                <div className="flex items-center gap-1 mr-2 bg-black/40 rounded border border-white/5 px-2 py-1">
                                    <button
                                        onClick={() => setSearchMode('aesthetic')}
                                        disabled={isSearching}
                                        className={`p-1.5 rounded transition-colors ${
                                            searchMode === 'aesthetic'
                                                ? 'bg-[var(--balatro-gold)]/20 text-[var(--balatro-gold)]'
                                                : 'text-white/40 hover:text-white/70'
                                        }`}
                                        title="Aesthetic Search"
                                    >
                                        <Palette size={14} />
                                    </button>
                                    <button
                                        onClick={() => hasSeedsInJaml && setSearchMode('seedlist')}
                                        disabled={isSearching || !hasSeedsInJaml}
                                        className={`p-1.5 rounded transition-colors ${
                                            searchMode === 'seedlist'
                                                ? 'bg-[var(--balatro-blue)]/20 text-[var(--balatro-blue)]'
                                                : hasSeedsInJaml
                                                    ? 'text-white/40 hover:text-white/70'
                                                    : 'text-white/20 cursor-not-allowed'
                                        }`}
                                        title={hasSeedsInJaml ? `Seed List (${parsedSeeds.length})` : 'No seeds in JAML'}
                                    >
                                        <List size={14} />
                                    </button>
                                </div>

                                {/* Aesthetic Selector (only in aesthetic mode) */}
                                {searchMode === 'aesthetic' && (
                                    <select
                                        value={selectedAesthetic}
                                        onChange={(e) => setSelectedAesthetic(parseInt(e.target.value))}
                                        disabled={isSearching}
                                        className="bg-black/40 border border-white/10 rounded px-2 py-1 text-xs font-pixel text-white/70 focus:outline-none focus:border-[var(--balatro-gold)]/50 mr-2"
                                    >
                                        {AESTHETIC_OPTIONS.map((opt) => (
                                            <option key={opt.value} value={opt.value}>
                                                {opt.label}
                                            </option>
                                        ))}
                                    </select>
                                )}

                                {/* Seed List Indicator (only in seedlist mode) */}
                                {searchMode === 'seedlist' && hasSeedsInJaml && (
                                    <span className="text-xs font-pixel text-[var(--balatro-blue)] mr-2">
                                        {parsedSeeds.length} seeds
                                    </span>
                                )}

                                {isSearching && (
                                    <div className="flex items-center gap-2 mr-2 px-3 py-1 bg-black/40 rounded border border-white/5 font-pixel text-[10px] text-white/50">
                                        <Loader2 size={12} className="animate-spin text-[var(--balatro-gold)]" />
                                        <span>Searching…</span>
                                    </div>
                                )}
                                <button
                                    onClick={handleSearch}
                                    disabled={isSearching || (searchMode === 'seedlist' && !hasSeedsInJaml)}
                                    className="balatro-button balatro-button-gold !py-2 !px-4 !text-sm flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                                >
                                    {isSearching ? <Loader2 size={14} className="animate-spin" /> : <Search size={14} />}
                                    Run search
                                </button>
                                {isSearching && (
                                    <button
                                        onClick={handleStop}
                                        className="balatro-button balatro-button-red !py-2 !px-4 !text-sm"
                                    >
                                        Stop
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
                                <h3 className="text-[var(--balatro-gold)] text-xl font-header drop-shadow-md">
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
                                    {searchResults.map((result) => (
                                        <AgnosticSeedCard
                                            key={result.seed}
                                            seed={result.seed}
                                            result={result}
                                            className="!scale-95 !origin-top"
                                        />
                                    ))}
                                </div>
                            ) : (
                                <div className="h-full flex flex-col items-center justify-center italic font-pixel text-sm border-2 border-dashed border-white/5 rounded-2xl p-4 text-center">
                                    {searchError ? (
                                        <div className="text-[var(--balatro-red)] bg-red-950/20 p-4 rounded text-center">
                                            <div className="font-header text-lg mb-1">Search failed</div>
                                            <div className="opacity-60">{searchError}</div>
                                        </div>
                                    ) : (
                                        <div className="opacity-30 text-white/50">
                                            {isSearching ? 'Seeking…' : 'Results will appear here'}
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
