import { useCallback, useEffect, useRef, useState } from 'react';

export function useDragScroll() {
    const [element, setElement] = useState<HTMLDivElement | null>(null);
    const [isDragging, setIsDragging] = useState(false);

    const ref = useCallback((node: HTMLDivElement | null) => {
        setElement(node);
    }, []);

    const isDraggingRef = useRef(false);
    const startXRef = useRef(0);
    const scrollLeftRef = useRef(0);

    useEffect(() => {
        if (!element) return;

        const handleMouseDown = (e: globalThis.MouseEvent) => {
            isDraggingRef.current = true;
            startXRef.current = e.pageX - element.offsetLeft;
            scrollLeftRef.current = element.scrollLeft;
            setIsDragging(true);
            element.style.cursor = 'grabbing';
            element.style.userSelect = 'none';
        };

        const endDrag = () => {
            if (!isDraggingRef.current) return;
            isDraggingRef.current = false;
            setIsDragging(false);
            element.style.cursor = 'grab';
            element.style.userSelect = '';
        };

        const handleMouseMove = (e: globalThis.MouseEvent) => {
            if (!isDraggingRef.current) return;
            e.preventDefault();
            const x = e.pageX - element.offsetLeft;
            const walk = (x - startXRef.current) * 2;
            element.scrollLeft = scrollLeftRef.current - walk;
        };

        element.addEventListener('mousedown', handleMouseDown);
        element.addEventListener('mouseleave', endDrag);
        element.addEventListener('mouseup', endDrag);
        element.addEventListener('mousemove', handleMouseMove);

        return () => {
            element.removeEventListener('mousedown', handleMouseDown);
            element.removeEventListener('mouseleave', endDrag);
            element.removeEventListener('mouseup', endDrag);
            element.removeEventListener('mousemove', handleMouseMove);
        };
    }, [element]);

    return { ref, isDragging };
}
