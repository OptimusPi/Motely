"use client";

import React, { useState, useEffect } from 'react';
import motely, { Motely } from 'motely-wasm';
import { cn } from '../../lib/utils';
import { DeckSprite } from './DeckSprite';

import { Loader2, Sparkles, ChevronRight } from 'lucide-react';

interface AgnosticSeedCardProps {
    seed: string;
    deckSlug?: string;
    stakeSlug?: string;
    className?: string;
    onClick?: () => void;
    analysis?: any;
    result?: any;
    dayNumber?: number;
    ritualId?: string;
    jamlConfig?: string | null;
    isLocked?: boolean;
    onShowHowTo?: () => void;
    onOpenSubmit?: () => void;
    canSubmit?: boolean;
    filter?: any;
}

export function AgnosticSeedCard({
    seed,
    deckSlug = 'Erratic',
    stakeSlug = 'White',
    isLocked,
    dayNumber,
    className,
    onClick,
    analysis: propAnalysis,
    result: propResult,
    filter
}: AgnosticSeedCardProps) {
    const [loading, setLoading] = useState(false);
    const [fetchedAnalysis, setFetchedAnalysis] = useState<any>(null);

    const result = propAnalysis || propResult || fetchedAnalysis;

    useEffect(() => {
        if (propAnalysis || propResult) return;

        const analyze = async () => {
            setLoading(true);
            try {
                await motely.boot();
                const jaml = `version: 1\nconfig:\n  deck: ${deckSlug}\n  stake: ${stakeSlug}\n`;
                const rawData = Motely.MotelyWasm.analyzeJamlSeeds(jaml, [seed]);
                
                if (rawData && rawData.seeds.length > 0) {
                    const seedData = rawData.seeds[0];
                    setFetchedAnalysis({
                        score: seedData.score,
                        matches: [],
                        analysis: seedData.analysis
                    });
                }
            } catch (err) {
                console.error(err);
            } finally {
                setLoading(false);
            }
        };

        analyze();
    }, [seed, deckSlug, stakeSlug, propAnalysis, propResult]);

    if (isLocked) {
        return (
            <div
                className={cn(
                    "balatro-panel flex flex-col items-center justify-center text-center",
                    "w-[315px] h-[340px] shrink-0",
                    "border-dashed border-white/10 opacity-60 grayscale",
                    "animate-sway", // Move sway to container
                    className
                )}
            >
                <div className="mb-4">
                    <DeckSprite deck={deckSlug} stake={stakeSlug} size={84} />
                </div>
                <h3 className="font-header text-2xl text-[var(--balatro-grey)] tracking-widest uppercase mb-2">PREVIEW ONLY</h3>
                <p className="font-pixel text-[10px] text-white/30 max-w-[200px]">
                    This seed unlocks tomorrow. You can view the deck, but the strategy is hidden!
                </p>
                <div className="mt-6 px-4 py-2 bg-black/60 rounded-xl border-2 border-white/10 shadow-[0_4px_0_rgba(0,0,0,0.5)]">
                    <span className="font-header text-xl text-[var(--balatro-gold)] text-shadow-balatro">UNLOCKS DAY {dayNumber}</span>
                </div>
            </div>
        );
    }

    return (
        <div
            className={cn(
                "balatro-panel flex flex-col cursor-pointer",
                "w-[315px] h-[340px] shrink-0", // ONE SIZE RULE
                "animate-sway", // Preserve the cute breathing/sway
                className
            )}
            onClick={onClick}
        >
            {/* Header - Mobile Optimized */}
            <div className="flex items-start gap-3 pb-4 border-b border-white/10">
                <div className="animate-juice-pop">
                    <DeckSprite deck={deckSlug} stake={stakeSlug} size={64} />
                </div>
                <div className="flex-1 min-w-0">
                    <h3 className="font-header text-3xl text-white leading-tight mb-1 truncate">{seed}</h3>
                    <p className="font-pixel text-xs text-white/40 tracking-wider">
                        {deckSlug} • {stakeSlug}
                    </p>
                </div>
                {loading ? (
                    <Loader2 className="animate-spin text-[var(--balatro-gold)] shrink-0" size={28} />
                ) : (
                    <Sparkles className="text-[var(--balatro-gold)] shrink-0" size={28} />
                )}
            </div>

            {/* Stats Grid - Larger Touch Targets */}
            <div className="grid grid-cols-2 gap-3 py-6">
                <div className="bg-black/40 rounded-xl p-5 border border-white/5 flex flex-col items-center justify-center">
                    <span className="block text-[11px] font-pixel text-white/30 mb-2">PRIMARY MATCH</span>
                    <div className="font-header text-xl text-[var(--balatro-blue)] leading-tight text-center">
                        {result?.matches?.[0]?.item?.name || result?.matches?.[0]?.name || filter?.should?.[0]?.value || filter?.must?.[0]?.value || "N/A"}
                    </div>
                </div>
                <div className="bg-black/40 rounded-xl p-5 border border-white/5 flex flex-col items-center justify-center">
                    <span className="block text-[11px] font-pixel text-white/30 mb-2">SIM SCORE</span>
                    <div className="font-header text-xl text-[var(--balatro-green)] leading-tight text-center">
                        {result?.score?.toLocaleString() || "0"}
                    </div>
                </div>
            </div>

            {/* Footer - Mobile Optimized */}
            <div className="mt-auto flex items-center justify-center pt-3 border-t border-white/5">
                <span className="font-header text-lg text-[var(--balatro-gold)] group-hover:text-white transition-colors text-shadow-balatro">
                    CLICK TO VIEW STRATEGY
                </span>
            </div>
        </div>
    );
}
