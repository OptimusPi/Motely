import { useEffect, useLayoutEffect, useRef, useState, useCallback } from "react";

export interface UseIntersectionObserverOptions extends IntersectionObserverInit {
  freezeOnceVisible?: boolean;
}

/**
 * Encapsulates IntersectionObserver logic.
 */
export function useIntersectionObserver({
  root,
  rootMargin,
  threshold,
  freezeOnceVisible,
}: UseIntersectionObserverOptions = {}) {
  const [entry, setEntry] = useState<IntersectionObserverEntry | null>(null);
  const [node, setNode] = useState<Element | null>(null);

  const ref = useCallback((node: Element | null) => {
    setNode(node);
  }, []);

  // Stable key so callers passing inline arrays don't re-create the observer each render.
  const thresholdKey = Array.isArray(threshold) ? threshold.join(",") : String(threshold);

  useEffect(() => {
    if (!node) return;

    const observer = new IntersectionObserver(([entry]) => {
      setEntry(entry);
      if (freezeOnceVisible && entry.isIntersecting) {
        observer.disconnect();
      }
    }, { root, rootMargin, threshold });

    observer.observe(node);

    return () => observer.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [node, root, rootMargin, thresholdKey, freezeOnceVisible]);

  return { ref, entry };
}

/**
 * Specialization for infinite scroll / sentinel patterns.
 */
export function useInfiniteScroll(
  onVisible: () => void,
  options: IntersectionObserverInit = {},
  active = true,
) {
  const onVisibleRef = useRef(onVisible);
  useLayoutEffect(() => {
    onVisibleRef.current = onVisible;
  });

  const { ref, entry } = useIntersectionObserver({
    ...options,
    threshold: options.threshold ?? 0.1,
  });

  useEffect(() => {
    if (!active || !entry) return;
    if (entry.isIntersecting) {
      onVisibleRef.current();
    }
  }, [entry, active]);

  return ref;
}
