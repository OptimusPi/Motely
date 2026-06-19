"use client";

import { useState, useEffect, useRef, useCallback, useLayoutEffect } from "react";

const DEFAULT_PULL = 12;

export interface StreamItem {
  id: string;
  name: string;
  value?: number;
}

export interface StreamState {
  items: StreamItem[];
  ready: boolean;
  loading: boolean;
  loadingMore: boolean;
  error: string | null;
  pullMore: (count?: number) => void;
}

/**
 * Generic stream hook for iterating any motely-wasm analyzer stream.
 *
 * @param initStream - called once to initialize the stream (e.g. analyzer.initShop(ante))
 * @param nextItem - called to get the next item from the stream (e.g. analyzer.nextShopItem())
 * @param deps - dependency array that triggers stream re-initialization
 * @param initialItems - items already visible (cursor advances past them)
 */
export function useMotelyStream(
  initStream: (() => void) | null,
  nextItem: (() => StreamItem) | null,
  deps: unknown[],
  initialItems: StreamItem[] = [],
): StreamState {
  const [items, setItems] = useState<StreamItem[]>(() => initialItems.map(i => ({ ...i })));
  const [error, setError] = useState<string | null>(null);
  const [ready, setReady] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);

  const nextRef = useRef(nextItem);
  useLayoutEffect(() => {
    nextRef.current = nextItem;
  });
  const busyRef = useRef(false);
  const genRef = useRef(0);

  useEffect(() => {
    const gen = ++genRef.current;
    const base = initialItems.map(i => ({ ...i }));

    // eslint-disable-next-line react-hooks/set-state-in-effect
    setItems(base);
    setError(null);
    setReady(false);
    setLoading(true);
    setLoadingMore(false);

    if (!initStream || !nextItem) {
      setLoading(false);
      return;
    }

    try {
      initStream();

      // Advance past already-visible items
      for (let i = 0; i < base.length; i++) nextItem();

      if (base.length === 0) {
        const prefetch: StreamItem[] = [];
        for (let i = 0; i < DEFAULT_PULL; i++) prefetch.push(nextItem());
        if (gen !== genRef.current) return;
        setItems(prefetch);
      }

      setReady(true);
    } catch (err) {
      if (gen !== genRef.current) return;
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      if (gen === genRef.current) setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  const pullMore = useCallback((count = DEFAULT_PULL) => {
    const next = nextRef.current;
    if (!next || count <= 0 || busyRef.current) return;
    busyRef.current = true;
    setLoadingMore(true);
    setError(null);
    try {
      const batch: StreamItem[] = [];
      for (let i = 0; i < count; i++) batch.push(next());
      setItems(prev => [...prev, ...batch]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      busyRef.current = false;
      setLoadingMore(false);
    }
  }, []);

  return { items, ready, loading, loadingMore, error, pullMore };
}
