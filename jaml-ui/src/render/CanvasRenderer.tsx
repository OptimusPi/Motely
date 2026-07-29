"use client";

import React from "react";
import { Layer } from "./Layer.js";
import { useJamlCardRenderer } from "./useJamlCardRenderer.js";

export interface JamlCardRendererProps {
    layers: Layer[];
    invert?: boolean;
    className?: string;
    hoverTilt?: boolean;
}

export function JamlCardRenderer({ layers, invert = false, className = "", hoverTilt = false }: JamlCardRendererProps) {
    const { canvasRef, containerRef, ratio, handlers } = useJamlCardRenderer({
        layers,
        invert,
        hoverTilt
    });

    return (
        <div
            ref={containerRef}
            className={`j-card-renderer ${className}`.trim()}
            data-hover-tilt={hoverTilt}
            data-hovered="false"
            style={{ "--j-card-aspect": String(ratio) } as React.CSSProperties}
        >
            <canvas ref={canvasRef} className="j-card-renderer__canvas" />
            <div className="j-card-renderer__hit" {...handlers} />
        </div>
    );
}
