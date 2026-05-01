import { useRef, useState, useEffect, MouseEvent } from 'react';

export function useDragScroll() {
    const ref = useRef<HTMLDivElement>(null);
    const [isDragging, setIsDragging] = useState(false);
    const [startX, setStartX] = useState(0);
    const [scrollLeft, setScrollLeft] = useState(0);

    useEffect(() => {
        const element = ref.current;
        if (!element) return;

        const handleMouseDown = (e: globalThis.MouseEvent) => {
            setIsDragging(true);
            setStartX(e.pageX - element.offsetLeft);
            setScrollLeft(element.scrollLeft);
            element.style.cursor = 'grabbing';
            element.style.userSelect = 'none';
        };

        const handleMouseLeave = () => {
            setIsDragging(false);
            if (element) element.style.cursor = 'grab';
        };

        const handleMouseUp = () => {
            setIsDragging(false);
            if (element) element.style.cursor = 'grab';
        };

        const handleMouseMove = (e: globalThis.MouseEvent) => {
            if (!isDragging) return;
            e.preventDefault();
            const x = e.pageX - element.offsetLeft;
            const walk = (x - startX) * 2;
            element.scrollLeft = scrollLeft - walk;
        };

        element.addEventListener('mousedown', handleMouseDown);
        element.addEventListener('mouseleave', handleMouseLeave);
        element.addEventListener('mouseup', handleMouseUp);
        element.addEventListener('mousemove', handleMouseMove);

        return () => {
            element.removeEventListener('mousedown', handleMouseDown);
            element.removeEventListener('mouseleave', handleMouseLeave);
            element.removeEventListener('mouseup', handleMouseUp);
            element.removeEventListener('mousemove', handleMouseMove);
        };
    }, [isDragging, startX, scrollLeft]);

    return { ref, isDragging };
}
