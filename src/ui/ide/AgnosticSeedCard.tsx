"use client";

import React, { useState, useEffect } from 'react';
import motely, { Motely } from 'motely-wasm';
import { cn } from '../../lib/utils';
import { DeckSprite } from './DeckSprite';
import { JamlGameCard, JamlVoucher, resolveAnalyzerShopItem } from '../../components/GameCard.js';

import { Loader2, Sparkles } from 'lucide-react';

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

function allAnalyzedItems(analysis: Motely.Analysis.MotelyLegacyTextAnalyzer | undefined): (Motely.Analysis.MotelyAnalyzedItem & { anteIndex: number; source: string })[] {
    if (!analysis) return [];
    return analysis.antes.flatMap((ante, anteIndex) => [
        ...ante.shopQueue.map(item => ({ ...item, anteIndex, source: 'shop' })),
        ...ante.packs.flatMap(pack =>
            pack.items.map(item => ({ ...item, anteIndex, source: 'pack' }))
        ),
    ]);
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
    jamlConfig,
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
                const jaml = jamlConfig ?? `version: 1\nconfig:\n  deck: ${deckSlug}\n  stake: ${stakeSlug}\n`;
                const rawData = Motely.MotelyWasm.analyzeJamlSeeds(jaml, [seed]);

                if (rawData && rawData.seeds.length > 0) {
                    const seedData = rawData.seeds[0];
                    setFetchedAnalysis({
                        score: seedData.score,
                        analysis: seedData.analysis,
                    });
                }
            } catch (err) {
                console.error(err);
            } finally {
                setLoading(false);
            }
        };

        analyze();
    }, [seed, deckSlug, stakeSlug, jamlConfig, propAnalysis, propResult]);

    const items = allAnalyzedItems(result?.analysis);

    if (isLocked) {
        return (
            <div
                className={cn(
                    "balatro-panel flex flex-col items-center justify-center text-center",
                    "w-[315px] h-[340px] shrink-0",
                    "border-dashed border-white/10 opacity-60 grayscale",
                    "animate-sway",
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
                "animate-sway",
                className
            )}
            onClick={onClick}
        >
            {/* Header */}
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

            {/* All analyzed items — matched ones highlighted */}
            <div className="flex-1 overflow-y-auto py-3 min-h-0">
                {items.length > 0 ? (
                    <div className="flex flex-wrap gap-1.5">
                        {items.map((item, i) => {
                            const resolved = resolveAnalyzerShopItem(
                                { id: `item-${i}`, name: item.name, value: item.value },
                                0.42
                            );
                            return (
                                <div
                                    key={i}
                                    className={cn(
                                        "flex-shrink-0",
                                        item.matched && "drop-shadow-[0_0_5px_var(--balatro-gold)]"
                                    )}
                                >
                                    {resolved.kind === 'voucher' ? (
                                        <JamlVoucher voucherName={resolved.voucherName} scale={0.42} />
                                    ) : resolved.kind === 'joker' || resolved.kind === 'consumable' || resolved.kind === 'playing' ? (
                                        <JamlGameCard card={resolved.card} type={resolved.type} />
                                    ) : null}
                                </div>
                            );
                        })}
                    </div>
                ) : (
                    <div className="h-full flex items-center justify-center font-pixel text-xs text-white/20">
                        {loading ? 'Analyzing...' : 'Pass jamlConfig to analyze'}
                    </div>
                )}
            </div>

            {/* Footer */}
            <div className="flex items-center justify-between pt-3 border-t border-white/5">
                <span className="font-header text-sm text-[var(--balatro-green)]">
                    {result?.score?.toLocaleString() ?? '—'}
                </span>
                <span className="font-header text-lg text-[var(--balatro-gold)] group-hover:text-white transition-colors text-shadow-balatro">
                    CLICK TO VIEW STRATEGY
                </span>
            </div>
        </div>
    );
}
