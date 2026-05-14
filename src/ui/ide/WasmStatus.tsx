"use client";

import React, { useMemo } from 'react';
import { Motely, getMotelyRuntimeSnapshot } from '../../motelyBoot.js';
import { Cpu, Loader2, CheckCircle2, XCircle } from 'lucide-react';
import { cn } from '../../lib/utils';

export function WasmStatus() {
    const snapshot = getMotelyRuntimeSnapshot();
    const status = snapshot.status;

    const displayVersion = useMemo(
        () => (status === 'ready' ? Motely.version() : null),
        [status],
    );

    const error = snapshot.error ? String(snapshot.error) : null;

    return (
        <div className={cn(
            "fixed bottom-10 right-10 z-[100] flex items-center gap-3 px-4 py-2 rounded-full border shadow-2xl backdrop-blur-md transition-all",
            status === 'ready' ? "bg-green-500/10 border-green-500/50 text-green-400" :
                status === 'error' ? "bg-red-500/10 border-red-500/50 text-red-400" :
                    "bg-blue-500/10 border-blue-500/50 text-blue-400"
        )}>
            {status === 'loading' ? <Loader2 size={16} className="animate-spin" /> :
                status === 'ready' ? <CheckCircle2 size={16} /> :
                    status === 'error' ? <XCircle size={16} /> :
                        <Cpu size={16} />}

            <div className="flex flex-col">
                <span className="text-[12px] tracking-widest leading-tight">
                    Wasm: {status}
                </span>
                {displayVersion && (
                    <span className="text-[11px] opacity-70 leading-tight">
                        v{displayVersion}
                    </span>
                )}
                {error && <span className="text-[11px] opacity-70 truncate max-w-[200px] leading-tight">{error}</span>}
            </div>
        </div>
    );
}
