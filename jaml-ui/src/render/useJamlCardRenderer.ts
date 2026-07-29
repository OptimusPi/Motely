import { useCallback, useEffect, useRef, useState } from "react";
import type { Layer } from "./Layer.js";
import { SPRITE_SHEETS } from "../sprites/spriteData.js";

function loadImage(url: string): Promise<HTMLImageElement | null> {
  return new Promise((resolve) => {
    const image = new window.Image();
    image.addEventListener("load", () => resolve(image));
    image.addEventListener("error", () => resolve(null));
    image.src = url;
  });
}

function renderImage(
  canvas: HTMLCanvasElement,
  context: CanvasRenderingContext2D,
  image: HTMLImageElement,
  layer: Layer,
  timestamp?: number
) {
  if (!image || !layer || !layer?.pos) return 0;
  const cardWidth = image.width / layer.columns;
  const cardHeight = image.height / layer.rows;
  const canvasStyle = canvas.style;

  if (layer.order === 0) {
    canvas.width = cardWidth;
    canvas.height = cardHeight;
    // Let the CSS class size the canvas to its container; the intrinsic
    // width/height above determine the drawing resolution.
    canvasStyle.width = "";
    canvasStyle.height = "";
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

  context.drawImage(
    image,
    layer.pos.x * cardWidth,
    layer.pos.y * cardHeight,
    cardWidth,
    cardHeight,
    0,
    0,
    canvas.width,
    canvas.height
  );
  context.restore();

  return cardWidth / cardHeight;
}

export function useJamlCardRenderer({
  layers,
  invert = false,
  hoverTilt = false,
}: {
  layers: Layer[];
  invert?: boolean;
  hoverTilt?: boolean;
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map());
  const [ratio, setRatio] = useState(3 / 4);
  const [, forceUpdate] = useState(0);
  const frameRef = useRef<number | null>(null);
  const targetRef = useRef({ rx: 0, ry: 0, rz: 0 });
  const currentRef = useRef({ rx: 0, ry: 0, rz: 0 });

  const hasAnimatedLayer = layers?.some((layer) => layer.animated);

  // Preload all known sheets once
  useEffect(() => {
    let cancelled = false;
    const imageCache = imageCacheRef.current;
    const preload = async () => {
      const urls = Array.from(new Set(Object.values(SPRITE_SHEETS).map((sheet) => sheet.src)));
      const images = await Promise.all(urls.map((url) => loadImage(url)));
      if (cancelled) return;
      images.forEach((image, index) => {
        if (image) imageCache.set(urls[index], image);
      });
      forceUpdate((prev) => prev + 1);
    };

    preload().catch((err) => console.error("[JamlCardRenderer]", err));

    return () => {
      cancelled = true;
      imageCache.clear();
    };
  }, []);

  // Drawing: RAF loop for animated layers, single paint otherwise.
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !layers || layers.length === 0) return;
    const context = canvas.getContext("2d");
    if (!context) return;

    let cancelled = false;
    let frame: number | null = null;
    let startTime: number | undefined;

    const drawOnce = (animTime?: number) => {
      context.clearRect(0, 0, canvas.width, canvas.height);
      ;[...layers]
        .sort((a, b) => a.order - b.order)
        .forEach((layer) => {
          const cached = imageCacheRef.current.get(layer.source);
          if (cached) {
            const imageRatio = renderImage(canvas, context, cached, layer, hasAnimatedLayer ? animTime : undefined);
            if (layer.order === 0) setRatio(imageRatio);
            return;
          }
          loadImage(layer.source).then((img) => {
            if (cancelled || !img) return;
            imageCacheRef.current.set(layer.source, img);
            if (!hasAnimatedLayer) {
              const imageRatio = renderImage(canvas, context, img, layer);
              if (layer.order === 0) setRatio(imageRatio);
            }
            forceUpdate((prev) => prev + 1);
          });
        });
      canvas.style.filter = invert ? "invert(0.94)" : "none";
    };

    if (hasAnimatedLayer) {
      const tick = (timestamp: number) => {
        if (cancelled) return;
        if (startTime === undefined) startTime = timestamp;
        drawOnce(timestamp - startTime);
        frame = requestAnimationFrame(tick);
      };
      frame = requestAnimationFrame(tick);
    } else {
      drawOnce();
    }

    return () => {
      cancelled = true;
      if (frame !== null) cancelAnimationFrame(frame);
    };
  }, [layers, invert, hasAnimatedLayer]);

  const stopTiltLoop = useCallback(() => {
    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current);
      frameRef.current = null;
    }
  }, []);

  const writeCardTransform = useCallback(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const c = currentRef.current;
    canvas.style.transform = `rotateX(${c.rx}deg) rotateY(${c.ry}deg) rotateZ(${c.rz}deg)`;
  }, []);

  const runTiltLoop = useCallback(
    (rate: number, onSettled?: () => void) => {
      stopTiltLoop();
      let previous = performance.now();
      const tick = (now: number) => {
        const dt = Math.min(0.05, (now - previous) / 1000);
        previous = now;
        const target = targetRef.current;
        const current = currentRef.current;
        const alpha = 1 - Math.exp(-rate * dt);
        current.rx += (target.rx - current.rx) * alpha;
        current.ry += (target.ry - current.ry) * alpha;
        current.rz += (target.rz - current.rz) * alpha;
        writeCardTransform();
        const settled =
          Math.abs(target.rx - current.rx) < 0.02 &&
          Math.abs(target.ry - current.ry) < 0.02 &&
          Math.abs(target.rz - current.rz) < 0.02;
        if (settled) {
          current.rx = target.rx;
          current.ry = target.ry;
          current.rz = target.rz;
          writeCardTransform();
          frameRef.current = null;
          onSettled?.();
          return;
        }
        frameRef.current = requestAnimationFrame(tick);
      };
      frameRef.current = requestAnimationFrame(tick);
    },
    [stopTiltLoop, writeCardTransform]
  );

  useEffect(() => () => stopTiltLoop(), [stopTiltLoop]);

  const onPointerEnter = (event: React.PointerEvent) => {
    if (!hoverTilt || event.pointerType === "touch") return;
    containerRef.current?.setAttribute("data-hovered", "true");
    runTiltLoop(22);
  };

  const onPointerLeave = () => {
    if (!hoverTilt) return;
    containerRef.current?.setAttribute("data-hovered", "false");
    targetRef.current = { rx: 0, ry: 0, rz: 0 };
    runTiltLoop(14, () => {
      const canvas = canvasRef.current;
      if (canvas) canvas.style.transform = "";
    });
  };

  const onPointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!hoverTilt || event.pointerType === "touch") return;
    const rect = event.currentTarget.getBoundingClientRect();
    const nx = Math.max(-1, Math.min(1, ((event.clientX - rect.left) / rect.width - 0.5) * 2));
    const ny = Math.max(-1, Math.min(1, ((event.clientY - rect.top) / rect.height - 0.5) * 2));
    targetRef.current = {
      rx: ny * -10,
      ry: nx * 12,
      rz: nx * ny * -1.2,
    };
    containerRef.current?.style.setProperty("--j-card-glare-x", `${(nx + 1) * 50}%`);
    containerRef.current?.style.setProperty("--j-card-glare-y", `${(1 - ny) * 50}%`);
    runTiltLoop(22);
  };

  return {
    canvasRef,
    containerRef,
    ratio,
    handlers: {
      onPointerEnter: hoverTilt ? onPointerEnter : undefined,
      onPointerLeave: hoverTilt ? onPointerLeave : undefined,
      onPointerMove: hoverTilt ? onPointerMove : undefined,
    },
  };
}
