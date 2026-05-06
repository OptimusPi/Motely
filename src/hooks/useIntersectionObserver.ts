import { useEffect, useLayoutEffect, useRef, useState, useCallback } from "react";

export interface UseIntersectionObserverOptions extends IntersectionObserverInit {
  freezeOnceVisible?: boolean;
}

/**
 * Encapsulates IntersectionObserver logic.
 */
export function useIntersectionObserver(
  options: UseIntersectionObserverOptions = {},
) {
  const [entry, setEntry] = useState<IntersectionObserverEntry | null>(null);
  const [node, setNode] = useState<Element | null>(null);
  const observer = useRef<IntersectionObserver | null>(null);

  const ref = useCallback((node: Element | null) => {
    setNode(node);
  }, []);

  useEffect(() => {
    if (!node) return;

    if (observer.current) observer.current.disconnect();

    const { freezeOnceVisible, ...init } = options;

    observer.current = new IntersectionObserver(([entry]) => {
      setEntry(entry);
      if (freezeOnceVisible && entry.isIntersecting && observer.current) {
        observer.current.disconnect();
      }
    }, init);

    observer.current.observe(node);

    return () => {
      if (observer.current) observer.current.disconnect();
    };
  }, [node, options, options.root, options.rootMargin, options.threshold, options.freezeOnceVisible]);

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
