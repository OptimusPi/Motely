"use client";

import React from "react";
import { Layer } from "./Layer.js";
import { useJamlCardRenderer } from "../ui/hooks.js";

export interface JamlCardRendererProps {
    layers: Layer[];
    invert?: boolean;
    className?: string;
    hoverTilt?: boolean;
}

export function JamlCardRenderer({ layers, invert = false, className = "", hoverTilt = false }: JamlCardRendererProps) {
    const { canvasRef, containerStyle, canvasStyle, handlers } = useJamlCardRenderer({
        layers,
        invert,
        hoverTilt
    });

    return (
        <div
            className={className}
            style={containerStyle}
            {...handlers}
        >
            <canvas ref={canvasRef} style={canvasStyle} />
        </div>
    );
}
