"use client";

import React, { useEffect, useRef, useState } from "react";
import { Layer } from "./Layer.js";
import { SPRITE_SHEETS } from "../sprites/spriteData.js";

function loadImage(url: string): Promise<HTMLImageElement | null> {
    return new Promise((resolve) => {
        const image = new window.Image();
        image.addEventListener("load", () => {
            resolve(image);
        });
        image.addEventListener("error", () => {
            resolve(null);
        });
        image.src = url;
    });
}

function renderImage(
    canvas: HTMLCanvasElement,
    context: CanvasRenderingContext2D,
    image: HTMLImageElement,
    layer: Layer,
    timestamp?: number,
) {
    if (!image || !layer || !layer?.pos) return 0;
    const cardWidth = image.width / layer.columns;
    const cardHeight = image.height / layer.rows;
    const canvasStyle = canvas.style;

    if (layer.order === 0) {
        canvas.width = cardWidth;
        canvas.height = cardHeight;
        canvasStyle.width = `${cardWidth}px`;
        canvasStyle.height = `${cardHeight}px`;
    }

    canvasStyle.imageRendering = "pixelated";
    context.imageSmoothingEnabled = true;

    context.save();

    if (layer.animated && timestamp) {
        const elapsed = timestamp;
        const yOffset = Math.sin(elapsed / 1000) * 3;
        const xOffset = Math.sin(elapsed / 1500) * 1.5;
        context.globalAlpha = 0.65 + (Math.sin(elapsed / 2000) + 1) * 0.075;
        context.translate(xOffset, yOffset);
    }

    context.drawImage(image, layer.pos.x * cardWidth, layer.pos.y * cardHeight, cardWidth, cardHeight, 0, 0, canvas.width, canvas.height);

    context.restore();

    return cardWidth / cardHeight;
}

export interface JamlCardRendererProps {
    layers: Layer[];
    invert?: boolean;
    className?: string;
    hoverTilt?: boolean;
}

export function JamlCardRenderer({ layers, invert = false, className = "", hoverTilt = false }: JamlCardRendererProps) {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());
    const [ratio, setRatio] = useState(3 / 4);
    const [, forceUpdate] = useState(0);
    const animationFrameRef = useRef<number | null>(null);
    const [elapsed, setElapsed] = useState(0);
    const [isHovered, setIsHovered] = useState(false);
    const [transform, setTransform] = useState("none");

    const hasAnimatedLayer = layers?.some((layer) => layer.animated);

    useEffect(() => {
        let cancelled = false;
        const imageCache = imageCacheRef.current;
        const preload = async () => {
            const urls = Array.from(new Set(Object.values(SPRITE_SHEETS).map((sheet) => sheet.src)));
            const images = await Promise.all(urls.map((url) => loadImage(url)));
            if (cancelled) return;
            images.forEach((image, index) => {
                if (image) {
                    imageCache.set(urls[index], image);
                }
            });
            forceUpdate((prev) => prev + 1);
        };

        preload().catch((err) => {
            console.error("[JamlCardRenderer]", err);
        });

        return () => {
            cancelled = true;
            imageCache.clear();
        };
    }, []);

    useEffect(() => {
        if (!hasAnimatedLayer) return;

        let startTime: number;

        const animate = (timestamp: number) => {
            if (!startTime) startTime = timestamp;
            const now = timestamp - startTime;
            if (!animationFrameRef.current || timestamp - 100 > animationFrameRef.current) {
                animationFrameRef.current = timestamp;
                setElapsed(now);
            }
            animationFrameRef.current = requestAnimationFrame(animate);
        };

        animationFrameRef.current = requestAnimationFrame(animate);

        return () => {
            if (animationFrameRef.current) {
                cancelAnimationFrame(animationFrameRef.current);
            }
        };
    }, [hasAnimatedLayer]);

    useEffect(() => {
        if (!canvasRef.current || !layers || layers.length === 0) return;
        const canvas = canvasRef.current;
        const context = canvas.getContext("2d");
        if (!context) return;
        let cancelled = false;

        context.clearRect(0, 0, canvas.width, canvas.height);
        [...layers]
            .sort((a, b) => a.order - b.order)
            .forEach((layer) => {
                if (imageCacheRef.current.has(layer.source)) {
                    const image = imageCacheRef.current.get(layer.source);
                    if (!image) return;
                    const imageRatio = renderImage(canvas, context, image, layer, hasAnimatedLayer ? elapsed : undefined);
                    if (layer.order === 0) {
                        setRatio(imageRatio);
                    }
                    return;
                }
                loadImage(layer.source).then((img) => {
                    if (cancelled || !img) return;
                    const imageRatio = renderImage(canvas, context, img, layer, hasAnimatedLayer ? elapsed : undefined);
                    imageCacheRef.current.set(layer.source, img);
                    if (layer.order === 0) {
                        setRatio(imageRatio);
                    }
                    forceUpdate((prev) => prev + 1);
                });
            });

        if (invert) {
            canvas.style.filter = "invert(0.94)";
        } else {
            canvas.style.filter = "none";
        }
        return () => { cancelled = true; };
    }, [layers, elapsed, invert, hasAnimatedLayer]);

    const handlePointerEnter = (event: React.PointerEvent<HTMLDivElement>) => {
        if (!hoverTilt || event.pointerType === "touch") return;
        setIsHovered(true);
    };

    const handlePointerLeave = () => {
        if (!hoverTilt) return;
        setIsHovered(false);
        setTransform("none");
    };

    const handlePointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
        if (!hoverTilt || event.pointerType === "touch") return;
        const rect = event.currentTarget.getBoundingClientRect();
        const x = event.clientX - rect.left;
        const y = event.clientY - rect.top;
        const rotateY = (x / rect.width) * 12 - 6;
        const rotateX = (y / rect.height) * -16 + 8;
        setTransform(`perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) translateZ(10px)`);
    };

    const containerStyle: React.CSSProperties = {
        aspectRatio: String(ratio),
        width: "100%",
        display: "flex",
        transition: hoverTilt && !isHovered ? "transform 0.4s ease" : undefined,
        transform: hoverTilt ? (isHovered ? transform : "none") : undefined,
        transformStyle: hoverTilt ? "preserve-3d" : undefined,
        transformOrigin: hoverTilt ? "center center" : undefined,
        userSelect: "none",
        WebkitUserSelect: "none",
    };

    const canvasStyle: React.CSSProperties = {
        borderRadius: "6px",
        boxShadow: hoverTilt && isHovered ? "0 2px 12px rgba(0,0,0,0.3)" : "0 2px 8px rgba(0,0,0,0.2)",
        imageRendering: "pixelated",
        transition: hoverTilt && !isHovered ? "box-shadow 0.4s ease-out" : undefined,
        pointerEvents: "none",
    };

    return (
        <div
            className={className}
            style={containerStyle}
            onPointerEnter={hoverTilt ? handlePointerEnter : undefined}
            onPointerLeave={hoverTilt ? handlePointerLeave : undefined}
            onPointerMove={hoverTilt ? handlePointerMove : undefined}
        >
            <canvas ref={canvasRef} style={canvasStyle} />
        </div>
    );
}
