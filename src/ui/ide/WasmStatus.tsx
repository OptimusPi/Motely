"use client";

import React, { useEffect, useState } from 'react';
import motely, { Motely } from 'motely-wasm';
import { Cpu, Loader2, CheckCircle2, XCircle } from 'lucide-react';
import { cn } from '../../lib/utils';

export function WasmStatus() {
    const [status, setStatus] = useState<'idle' | 'loading' | 'ready' | 'error'>('idle');
    const [version, setVersion] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        let cancelled = false;
        const load = async () => {
            setStatus('loading');
            try {
                await motely.boot();
                if (cancelled) return;
                setVersion(Motely.MotelyWasm.getVersion());
                setStatus('ready');
            } catch (err: any) {
                console.error("WASM Status Error:", err);
                if (cancelled) return;
                setStatus('error');
                setError(err.message || String(err));
            }
        };
        load();

        return () => {
            cancelled = true;
        };
    }, []);

    const displayVersion = version;

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
                <span className="text-[12px] uppercase tracking-widest leading-tight">
                    WASM: {status.toUpperCase()}
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
