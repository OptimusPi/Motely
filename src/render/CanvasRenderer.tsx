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
        <div className={className} style={{ ...containerStyle, position: "relative" }}>
            <canvas ref={canvasRef} style={canvasStyle} />
            {hoverTilt && (
                <div 
                    style={{ position: "absolute", inset: "-15px", zIndex: 10, cursor: "pointer" }} 
                    {...handlers} 
                />
            )}
            {!hoverTilt && (
                <div 
                    style={{ position: "absolute", inset: "0", zIndex: 10 }} 
                    {...handlers} 
                />
            )}
        </div>
    );
}
