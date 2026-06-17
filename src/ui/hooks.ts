import React, { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { JamlVisualClause, JamlVisualFilter, JamlZone } from '../components/JamlIdeVisual.js'
import { JIMBO_ANIMATIONS } from './tokens.js'
import { Layer } from '../render/Layer.js'
import { SPRITE_SHEETS } from '../sprites/spriteData.js'

function loadImage(url: string): Promise<HTMLImageElement | null> {
  return new Promise((resolve) => {
    const image = new window.Image()
    image.addEventListener('load', () => resolve(image))
    image.addEventListener('error', () => resolve(null))
    image.src = url
  })
}

function renderImage(
  canvas: HTMLCanvasElement,
  context: CanvasRenderingContext2D,
  image: HTMLImageElement,
  layer: Layer,
  timestamp?: number,
) {
  if (!image || !layer || !layer?.pos) return 0
  const cardWidth = image.width / layer.columns
  const cardHeight = image.height / layer.rows
  const canvasStyle = canvas.style

  if (layer.order === 0) {
    canvas.width = cardWidth
    canvas.height = cardHeight
    canvasStyle.width = `${cardWidth}px`
    canvasStyle.height = `${cardHeight}px`
  }

  canvasStyle.imageRendering = 'pixelated'
  context.imageSmoothingEnabled = true

  context.save()

  if (layer.animated && timestamp) {
    const elapsed = timestamp
    const yOffset = Math.sin(elapsed / 1000) * 3
    const xOffset = Math.sin(elapsed / 1500) * 1.5
    context.globalAlpha = 0.65 + (Math.sin(elapsed / 2000) + 1) * 0.075
    context.translate(xOffset, yOffset)
  }

  context.drawImage(image, layer.pos.x * cardWidth, layer.pos.y * cardHeight, cardWidth, cardHeight, 0, 0, canvas.width, canvas.height)
  context.restore()

  return cardWidth / cardHeight
}

/**
 * Sway animation for Balatro-style UI elements.
 */
export function useSway(active: boolean) {
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!active || !ref.current) return
    let frame: number
    const start = Date.now()
    const el = ref.current
    const tick = () => {
      const t = ((Date.now() - start) % JIMBO_ANIMATIONS.SWAY_DURATION) / JIMBO_ANIMATIONS.SWAY_DURATION * Math.PI * 2
      el.style.transform = `translate(${Math.sin(t) * JIMBO_ANIMATIONS.SWAY_AMOUNT * 0.3}px, ${Math.sin(t * 0.8) * JIMBO_ANIMATIONS.SWAY_AMOUNT}px)`
      frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => {
      cancelAnimationFrame(frame)
      if (el) el.style.transform = ''
    }
  }, [active])

  return ref
}

/**
 * Handles delayed visibility for transitions (e.g. modals).
 */
export function useDelayedVisibility(open: boolean, delay: number) {
  const [visible, setVisible] = useState(open)
  const [opacity, setOpacity] = useState(open ? 1 : 0)
  const [prevOpen, setPrevOpen] = useState(open)

  if (open !== prevOpen) {
    setPrevOpen(open)
    if (open) {
      setVisible(true)
    } else {
      setOpacity(0)
    }
  }

  useEffect(() => {
    if (open) {
      const frame = requestAnimationFrame(() => setOpacity(1))
      return () => cancelAnimationFrame(frame)
    } else {
      const t = setTimeout(() => setVisible(false), delay)
      return () => clearTimeout(t)
    }
  }, [open, delay])

  return { visible, opacity }
}

export type JimboBackgroundColor = string | [number, number, number]

export interface JimboBackgroundConfig {
  /** Three palette colors. Each can be `#RRGGBB` / `#RGB` or `[r,g,b]` in 0..1. */
  primary?: JimboBackgroundColor
  secondary?: JimboBackgroundColor
  dark?: JimboBackgroundColor
  /** Animation speed multiplier on top of the base SPIN_SPEED. Default 1. */
  speed?: number
  /** Twirl rotation seed (default -2). */
  spinRotation?: number
  /** How much the twirl warps the field, 0..1ish (default 0.35). */
  spinAmount?: number
  /** Pixelation. Higher = finer pixels (default ~244 = 740*0.33). */
  pixelFilter?: number
  /** Contrast multiplier (default 4.5). */
  contrast?: number
  /** Bright lighting amount (default 0.5). */
  lighting?: number
  /** Color transition duration in ms when palette changes. Default 800. */
  transitionMs?: number
}

const BALATRO_DEFAULTS = {
  primary:    [1.0,  0.2,  0.2 ] as [number, number, number], // red
  secondary:  [0.0,  0.5,  1.0 ] as [number, number, number], // blue
  dark:       [0.05, 0.08, 0.1 ] as [number, number, number], // near-black
  speed: 1.0,
  spinRotation: -2.0,
  spinAmount: 0.35,
  pixelFilter: 740.0 * 0.33,
  contrast: 4.5,
  lighting: 0.5,
  transitionMs: 800,
}

function parseColor(c: JimboBackgroundColor | undefined, fallback: [number, number, number]): [number, number, number] {
  if (!c) return fallback
  if (Array.isArray(c)) return [c[0], c[1], c[2]]
  let hex = c.trim().replace(/^#/, '')
  if (hex.length === 3) hex = hex.split('').map((ch) => ch + ch).join('')
  if (hex.length !== 6) return fallback
  const n = parseInt(hex, 16)
  if (Number.isNaN(n)) return fallback
  return [((n >> 16) & 0xff) / 255, ((n >> 8) & 0xff) / 255, (n & 0xff) / 255]
}

/**
 * Hook for the Balatro hypnotic swirl background.
 * Manages WebGL context, shader compilation, and animation loop.
 *
 * All shader constants are exposed as uniforms so they can be tuned at
 * runtime. Palette and scalar uniforms (pixelFilter, contrast, spin, etc.)
 * interpolate over `transitionMs` so control changes fade smoothly.
 */
export function useBalatroBackground(config: JimboBackgroundConfig = {}) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  // Latest config lives in a ref so the render loop can read fresh values
  // without re-creating the WebGL context on every prop change.
  const configRef = useRef(config)
  useLayoutEffect(() => {
    configRef.current = config
  })

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const gl = canvas.getContext('webgl')
    if (!gl) return

    const vsSource = `
      attribute vec2 position;
      void main() {
        gl_Position = vec4(position, 0.0, 1.0);
      }
    `

    const fsSource = `
      precision mediump float;

      uniform float u_time;
      uniform vec2 u_resolution;
      uniform vec3 u_color1;
      uniform vec3 u_color2;
      uniform vec3 u_color3;
      uniform float u_spinRotation;
      uniform float u_spinSpeed;
      uniform float u_spinAmount;
      uniform float u_pixelFilter;
      uniform float u_contrast;
      uniform float u_lighting;

      void main() {
        vec2 screenSize = u_resolution;
        float pixel_size = length(screenSize.xy) / max(1.0, u_pixelFilter);
        vec2 uv = (floor(gl_FragCoord.xy*(1.0/pixel_size))*pixel_size - 0.5*screenSize.xy)/length(screenSize.xy);
        float uv_len = length(uv);

        float speed = (u_spinRotation * 0.2) + 302.2;
        float new_pixel_angle = atan(uv.y, uv.x) + speed - 20.0*(u_spinAmount*uv_len + (1.0 - u_spinAmount));

        vec2 mid = (screenSize.xy/length(screenSize.xy))/2.0;
        uv = (vec2((uv_len * cos(new_pixel_angle) + mid.x), (uv_len * sin(new_pixel_angle) + mid.y)) - mid);

        uv *= 30.0;
        speed = u_time * u_spinSpeed;
        vec2 uv2 = vec2(uv.x, uv.y);

        for(int i=0; i < 5; i++) {
          uv2 += sin(max(uv.x, uv.y)) + uv;
          uv  += 0.5*vec2(cos(5.1123314 + 0.353*uv2.y + speed*0.131121), sin(uv2.x - 0.113*speed));
          uv  -= 1.0*cos(uv.x + uv.y) - 1.0*sin(uv.x*0.711 - uv.y);
        }

        float contrast_mod = (0.25*u_contrast + 0.5*u_spinAmount + 1.2);
        float paint_res = min(2.0, max(0.0, length(uv)*(0.035)*contrast_mod));
        float c1p = max(0.0, 1.0 - contrast_mod*abs(1.0 - paint_res));
        float c2p = max(0.0, 1.0 - contrast_mod*abs(paint_res));
        float c3p = 1.0 - min(1.0, c1p + c2p);
        float light = (u_lighting - 0.2)*max(c1p*5.0 - 4.0, 0.0) + u_lighting*max(c2p*5.0 - 4.0, 0.0);

        vec3 base = (0.3/u_contrast)*u_color1 + (1.0 - 0.3/u_contrast)*(u_color1*c1p + u_color2*c2p + c3p*u_color3) + vec3(light);
        gl_FragColor = vec4(base, 1.0);
      }
    `

    const createShader = (type: number, source: string) => {
      const shader = gl.createShader(type)
      if (!shader) return null
      gl.shaderSource(shader, source)
      gl.compileShader(shader)
      if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        console.error('[JimboBackground] shader compile error:', gl.getShaderInfoLog(shader))
        gl.deleteShader(shader)
        return null
      }
      return shader
    }

    const vertexShader = createShader(gl.VERTEX_SHADER, vsSource)
    const fragmentShader = createShader(gl.FRAGMENT_SHADER, fsSource)
    if (!vertexShader || !fragmentShader) return

    const program = gl.createProgram()
    if (!program) return
    gl.attachShader(program, vertexShader)
    gl.attachShader(program, fragmentShader)
    gl.linkProgram(program)
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
      console.error('[JimboBackground] program link error:', gl.getProgramInfoLog(program))
      return
    }

    gl.useProgram(program)

    const positionBuffer = gl.createBuffer()
    gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer)
    gl.bufferData(
      gl.ARRAY_BUFFER,
      new Float32Array([
        -1.0, -1.0, 1.0, -1.0, -1.0, 1.0,
        -1.0, 1.0, 1.0, -1.0, 1.0, 1.0,
      ]),
      gl.STATIC_DRAW,
    )

    const positionLocation = gl.getAttribLocation(program, 'position')
    gl.enableVertexAttribArray(positionLocation)
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0)

    const u_time = gl.getUniformLocation(program, 'u_time')
    const u_resolution = gl.getUniformLocation(program, 'u_resolution')
    const u_color1 = gl.getUniformLocation(program, 'u_color1')
    const u_color2 = gl.getUniformLocation(program, 'u_color2')
    const u_color3 = gl.getUniformLocation(program, 'u_color3')
    const u_spinRotation = gl.getUniformLocation(program, 'u_spinRotation')
    const u_spinSpeed = gl.getUniformLocation(program, 'u_spinSpeed')
    const u_spinAmount = gl.getUniformLocation(program, 'u_spinAmount')
    const u_pixelFilter = gl.getUniformLocation(program, 'u_pixelFilter')
    const u_contrast = gl.getUniformLocation(program, 'u_contrast')
    const u_lighting = gl.getUniformLocation(program, 'u_lighting')

    const resolveScalars = (cfg: JimboBackgroundConfig) => ({
      spinRotation: cfg.spinRotation ?? BALATRO_DEFAULTS.spinRotation,
      spinSpeed: 4.5 * (cfg.speed ?? BALATRO_DEFAULTS.speed),
      spinAmount: cfg.spinAmount ?? BALATRO_DEFAULTS.spinAmount,
      pixelFilter: cfg.pixelFilter ?? BALATRO_DEFAULTS.pixelFilter,
      contrast: cfg.contrast ?? BALATRO_DEFAULTS.contrast,
      lighting: cfg.lighting ?? BALATRO_DEFAULTS.lighting,
    })

    // Interpolated palette + shader scalars — all ease toward configRef over transitionMs.
    const boot = configRef.current
    const current: [number, number, number][] = [
      parseColor(boot.primary, BALATRO_DEFAULTS.primary),
      parseColor(boot.secondary, BALATRO_DEFAULTS.secondary),
      parseColor(boot.dark, BALATRO_DEFAULTS.dark),
    ]
    const currentScalars = resolveScalars(boot)
    const startTime = performance.now()
    let lastFrame = startTime
    let animationFrameId = 0

    const render = () => {
      const now = performance.now()
      const dt = Math.max(0, now - lastFrame)
      lastFrame = now

      const cfg = configRef.current
      const target: [number, number, number][] = [
        parseColor(cfg.primary,   BALATRO_DEFAULTS.primary),
        parseColor(cfg.secondary, BALATRO_DEFAULTS.secondary),
        parseColor(cfg.dark,      BALATRO_DEFAULTS.dark),
      ]
      const targetScalars = resolveScalars(cfg)
      const transitionMs = Math.max(1, cfg.transitionMs ?? BALATRO_DEFAULTS.transitionMs)
      // Frame-rate-independent lerp: alpha = 1 - exp(-dt / tau).
      const alpha = 1 - Math.exp(-dt / transitionMs)
      for (let i = 0; i < 3; i++) {
        for (let c = 0; c < 3; c++) {
          current[i][c] += (target[i][c] - current[i][c]) * alpha
        }
      }
      const lerpScalar = (key: keyof typeof currentScalars) => {
        currentScalars[key] += (targetScalars[key] - currentScalars[key]) * alpha
      }
      lerpScalar('spinRotation')
      lerpScalar('spinSpeed')
      lerpScalar('spinAmount')
      lerpScalar('pixelFilter')
      lerpScalar('contrast')
      lerpScalar('lighting')

      const displayWidth = canvas.clientWidth
      const displayHeight = canvas.clientHeight
      if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
        canvas.width = displayWidth
        canvas.height = displayHeight
        gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight)
      }

      gl.uniform1f(u_time, (now - startTime) / 1000.0)
      gl.uniform2f(u_resolution, canvas.width, canvas.height)
      gl.uniform3f(u_color1, current[0][0], current[0][1], current[0][2])
      gl.uniform3f(u_color2, current[1][0], current[1][1], current[1][2])
      gl.uniform3f(u_color3, current[2][0], current[2][1], current[2][2])
      gl.uniform1f(u_spinRotation, currentScalars.spinRotation)
      gl.uniform1f(u_spinSpeed, currentScalars.spinSpeed)
      gl.uniform1f(u_spinAmount, currentScalars.spinAmount)
      gl.uniform1f(u_pixelFilter, currentScalars.pixelFilter)
      gl.uniform1f(u_contrast, currentScalars.contrast)
      gl.uniform1f(u_lighting, currentScalars.lighting)

      gl.drawArrays(gl.TRIANGLES, 0, 6)
      animationFrameId = requestAnimationFrame(render)
    }

    animationFrameId = requestAnimationFrame(render)

    return () => {
      cancelAnimationFrame(animationFrameId)
      gl.deleteProgram(program)
      gl.deleteShader(vertexShader)
      gl.deleteShader(fragmentShader)
      gl.deleteBuffer(positionBuffer)
    }
  }, [])

  return canvasRef
}

export type JimboTooltipMode = 'snap' | 'mouse'
export type JimboTooltipPlacement = 'top' | 'bottom' | 'auto'

/**
 * Hook for managing JimboTooltip state and positioning.
 */
export function useJimboTooltip({
  mode = 'snap',
  placement = 'auto',
  delay = 80,
  disabled = false,
}: {
  mode?: JimboTooltipMode
  placement?: JimboTooltipPlacement
  delay?: number
  disabled?: boolean
}) {
  const [visible, setVisible] = useState(false)
  const [pos, setPos] = useState<{ left: number; top: number; align: 'top' | 'bottom' } | null>(null)
  const targetRef = useRef<HTMLElement | null>(null)
  const tooltipRef = useRef<HTMLDivElement | null>(null)
  const delayTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const show = useCallback(() => {
    if (disabled) return
    if (delayTimerRef.current) clearTimeout(delayTimerRef.current)
    delayTimerRef.current = setTimeout(() => setVisible(true), delay)
  }, [disabled, delay])

  const hide = useCallback(() => {
    if (delayTimerRef.current) clearTimeout(delayTimerRef.current)
    setVisible(false)
    setPos(null)
  }, [])

  const computeSnapPos = useCallback(() => {
    const el = targetRef.current
    const tip = tooltipRef.current
    if (!el || !tip) return
    const rect = el.getBoundingClientRect()
    const tipRect = tip.getBoundingClientRect()
    const roomAbove = rect.top
    const align: 'top' | 'bottom' =
      placement === 'top' ? 'top'
        : placement === 'bottom' ? 'bottom'
          : roomAbove >= tipRect.height + 12 ? 'top' : 'bottom'

    const left = rect.left + rect.width / 2 - tipRect.width / 2
    const top = align === 'top' ? rect.top - tipRect.height - 8 : rect.bottom + 8
    setPos({
      left: Math.max(8, Math.min(window.innerWidth - tipRect.width - 8, left)),
      top,
      align
    })
  }, [placement])

  useEffect(() => {
    if (!visible || mode !== 'snap') return
    const raf = requestAnimationFrame(computeSnapPos)
    window.addEventListener('resize', computeSnapPos)
    window.addEventListener('scroll', computeSnapPos, true)
    return () => {
      cancelAnimationFrame(raf)
      window.removeEventListener('resize', computeSnapPos)
      window.removeEventListener('scroll', computeSnapPos, true)
    }
  }, [visible, mode, computeSnapPos])

  useEffect(() => () => {
    if (delayTimerRef.current) clearTimeout(delayTimerRef.current)
  }, [])

  const handleMouseMove = useCallback((e: React.MouseEvent | React.PointerEvent) => {
    if (mode !== 'mouse') return
    setPos({ left: e.clientX + 12, top: e.clientY + 16, align: 'bottom' })
  }, [mode])

  return {
    visible,
    pos,
    targetRef,
    tooltipRef,
    show,
    hide,
    handleMouseMove,
  }
}

/**
 * Hook for managing the JamlCardRenderer logic.
 */
export function useJamlCardRenderer({
  layers,
  invert = false,
  hoverTilt = false,
}: {
  layers: Layer[]
  invert?: boolean
  hoverTilt?: boolean
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map())
  const [ratio, setRatio] = useState(3 / 4)
  const [, forceUpdate] = useState(0)
  const frameRef = useRef<number | null>(null)
  const targetRef = useRef({ rx: 0, ry: 0, rz: 0 })
  const currentRef = useRef({ rx: 0, ry: 0, rz: 0 })

  const hasAnimatedLayer = layers?.some((layer) => layer.animated)

  // Preload all known sheets once
  useEffect(() => {
    let cancelled = false
    const imageCache = imageCacheRef.current
    const preload = async () => {
      const urls = Array.from(new Set(Object.values(SPRITE_SHEETS).map((sheet) => sheet.src)))
      const images = await Promise.all(urls.map((url) => loadImage(url)))
      if (cancelled) return
      images.forEach((image, index) => {
        if (image) imageCache.set(urls[index], image)
      })
      forceUpdate((prev) => prev + 1)
    }

    preload().catch((err) => console.error('[JamlCardRenderer]', err))

    return () => {
      cancelled = true
      imageCache.clear()
    }
  }, [])

  // Drawing: RAF loop for animated layers, single paint otherwise. Previously
  // the canvas redrew via an Effect that depended on an `elapsed` state which
  // a separate ~10fps RAF loop kept ticking — re-running setup/teardown of
  // the drawing Effect every animation frame. Now drawing lives inside the
  // RAF loop and the Effect only re-runs on layer / invert / animated changes.
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas || !layers || layers.length === 0) return
    const context = canvas.getContext('2d')
    if (!context) return

    let cancelled = false
    let frame: number | null = null
    let startTime: number | undefined

    const drawOnce = (animTime?: number) => {
      context.clearRect(0, 0, canvas.width, canvas.height)
      ;[...layers]
        .sort((a, b) => a.order - b.order)
        .forEach((layer) => {
          const cached = imageCacheRef.current.get(layer.source)
          if (cached) {
            const imageRatio = renderImage(canvas, context, cached, layer, hasAnimatedLayer ? animTime : undefined)
            if (layer.order === 0) setRatio(imageRatio)
            return
          }
          loadImage(layer.source).then((img) => {
            if (cancelled || !img) return
            imageCacheRef.current.set(layer.source, img)
            // For non-animated layers there's no RAF tick to repaint the
            // newly-loaded image, so paint it now.
            if (!hasAnimatedLayer) {
              const imageRatio = renderImage(canvas, context, img, layer)
              if (layer.order === 0) setRatio(imageRatio)
            }
            forceUpdate((prev) => prev + 1)
          })
        })
      canvas.style.filter = invert ? 'invert(0.94)' : 'none'
    }

    if (hasAnimatedLayer) {
      const tick = (timestamp: number) => {
        if (cancelled) return
        if (startTime === undefined) startTime = timestamp
        drawOnce(timestamp - startTime)
        frame = requestAnimationFrame(tick)
      }
      frame = requestAnimationFrame(tick)
    } else {
      drawOnce()
    }

    return () => {
      cancelled = true
      if (frame !== null) cancelAnimationFrame(frame)
    }
  }, [layers, invert, hasAnimatedLayer])

  const stopTiltLoop = useCallback(() => {
    if (frameRef.current !== null) {
      cancelAnimationFrame(frameRef.current)
      frameRef.current = null
    }
  }, [])

  const writeCardTransform = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const c = currentRef.current
    canvas.style.transform = `rotateX(${c.rx}deg) rotateY(${c.ry}deg) rotateZ(${c.rz}deg)`
  }, [])

  const runTiltLoop = useCallback((rate: number, onSettled?: () => void) => {
    stopTiltLoop()
    let previous = performance.now()
    const tick = (now: number) => {
      const dt = Math.min(0.05, (now - previous) / 1000)
      previous = now
      const target = targetRef.current
      const current = currentRef.current
      const alpha = 1 - Math.exp(-rate * dt)
      current.rx += (target.rx - current.rx) * alpha
      current.ry += (target.ry - current.ry) * alpha
      current.rz += (target.rz - current.rz) * alpha
      writeCardTransform()
      const settled =
        Math.abs(target.rx - current.rx) < 0.02 &&
        Math.abs(target.ry - current.ry) < 0.02 &&
        Math.abs(target.rz - current.rz) < 0.02
      if (settled) {
        current.rx = target.rx
        current.ry = target.ry
        current.rz = target.rz
        writeCardTransform()
        frameRef.current = null
        onSettled?.()
        return
      }
      frameRef.current = requestAnimationFrame(tick)
    }
    frameRef.current = requestAnimationFrame(tick)
  }, [stopTiltLoop, writeCardTransform])

  useEffect(() => () => stopTiltLoop(), [stopTiltLoop])

  const onPointerEnter = (event: React.PointerEvent) => {
    if (!hoverTilt || event.pointerType === 'touch') return
    containerRef.current?.setAttribute('data-hovered', 'true')
    runTiltLoop(22)
  }

  const onPointerLeave = () => {
    if (!hoverTilt) return
    containerRef.current?.setAttribute('data-hovered', 'false')
    targetRef.current = { rx: 0, ry: 0, rz: 0 }
    runTiltLoop(14, () => {
      const canvas = canvasRef.current
      if (canvas) canvas.style.transform = ''
    })
  }

  const onPointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!hoverTilt || event.pointerType === 'touch') return
    const rect = event.currentTarget.getBoundingClientRect()
    const nx = Math.max(-1, Math.min(1, ((event.clientX - rect.left) / rect.width - 0.5) * 2))
    const ny = Math.max(-1, Math.min(1, ((event.clientY - rect.top) / rect.height - 0.5) * 2))
    targetRef.current = {
      rx: ny * -10,
      ry: nx * 12,
      rz: nx * ny * -1.2,
    }
    containerRef.current?.style.setProperty('--j-card-glare-x', `${(nx + 1) * 50}%`)
    containerRef.current?.style.setProperty('--j-card-glare-y', `${(1 - ny) * 50}%`)
    runTiltLoop(22)
  }

  return {
    canvasRef,
    containerRef,
    ratio,
    handlers: {
      onPointerEnter: hoverTilt ? onPointerEnter : undefined,
      onPointerLeave: hoverTilt ? onPointerLeave : undefined,
      onPointerMove: hoverTilt ? onPointerMove : undefined,
    }
  }
}

/**
 * Tracks which 'ante' section is currently most visible in a scrollable container.
 */
export function useAnteTracker(antes: { ante: number }[], options: { threshold?: number[] } = {}) {
  const [currentAnte, setCurrentAnte] = useState(antes[0]?.ante ?? 0)
  const [prevFirstAnte, setPrevFirstAnte] = useState(antes[0]?.ante)
  const scrollRef = useRef<HTMLDivElement>(null)
  const anteRefs = useRef<Map<number, HTMLElement>>(new Map())

  if (antes[0]?.ante !== prevFirstAnte) {
    setPrevFirstAnte(antes[0]?.ante)
    if (antes.length > 0) {
      setCurrentAnte(antes[0].ante)
    }
  }

  useEffect(() => {
    const root = scrollRef.current
    if (!root || antes.length === 0) return

    const observer = new IntersectionObserver(
      (entries) => {
        // Find the entry with the highest intersection ratio
        const mostVisible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0]

        if (mostVisible) {
          const ante = Number((mostVisible.target as HTMLElement).dataset.ante)
          if (!Number.isNaN(ante)) {
            setCurrentAnte(ante)
          }
        }
      },
      {
        root,
        threshold: options.threshold ?? [0.4, 0.6, 0.8],
      }
    )

    anteRefs.current.forEach((el) => observer.observe(el))

    return () => observer.disconnect()
  }, [antes, options.threshold])

  const scrollToAnte = useCallback((ante: number) => {
    const el = anteRefs.current.get(ante)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [])

  const registerAnteRef = useCallback((ante: number, el: HTMLElement | null) => {
    if (el) {
      anteRefs.current.set(ante, el)
    } else {
      anteRefs.current.delete(ante)
    }
  }, [])

  return {
    currentAnte,
    scrollRef,
    scrollToAnte,
    registerAnteRef,
  }
}

export interface DragState {
  clause: JamlVisualClause
  fromZone: JamlZone
  x: number
  y: number
  offX: number
  offY: number
}

interface PendingDragState {
  clause: JamlVisualClause
  fromZone: JamlZone
  x: number
  y: number
  offX: number
  offY: number
}

/**
 * Manages drag-and-drop state for the Jaml IDE visual filter editor.
 */
export function useJamlIdeDrag(
  filter: JamlVisualFilter,
  onChange: (filter: JamlVisualFilter) => void,
  rootRef: React.RefObject<HTMLDivElement | null>
) {
  const [drag, setDrag] = useState<DragState | null>(null)
  const [pendingDrag, setPendingDrag] = useState<PendingDragState | null>(null)
  const [hoverZone, setHoverZone] = useState<string | null>(null)

  const onDragStart = useCallback(
    (e: React.MouseEvent | React.TouchEvent, clause: JamlVisualClause, fromZone: JamlZone) => {
      const t = 'touches' in e ? e.touches[0] : e
      const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
      setPendingDrag({
        clause,
        fromZone,
        x: t.clientX,
        y: t.clientY,
        offX: t.clientX - rect.left,
        offY: t.clientY - rect.top,
      })
      setHoverZone(null)
    },
    []
  )

  // Mirror live state into refs so the window listeners can read fresh values
  // without re-binding on every drag tick.
  const dragRef = useRef(drag)
  const pendingRef = useRef(pendingDrag)
  const hoverRef = useRef(hoverZone)
  const filterRef = useRef(filter)
  const onChangeRef = useRef(onChange)
  useLayoutEffect(() => {
    dragRef.current = drag
    pendingRef.current = pendingDrag
    hoverRef.current = hoverZone
    filterRef.current = filter
    onChangeRef.current = onChange
  })

  const active = drag !== null || pendingDrag !== null

  useEffect(() => {
    if (!active) return

    const move = (e: MouseEvent | TouchEvent) => {
      const touchEvent = 'touches' in e ? (e as TouchEvent) : null
      const t = touchEvent ? touchEvent.touches[0] : (e as MouseEvent)
      if (!t) return

      let activeDrag = dragRef.current

      if (!activeDrag && pendingRef.current) {
        const dx = t.clientX - pendingRef.current.x
        const dy = t.clientY - pendingRef.current.y
        if (Math.hypot(dx, dy) < 8) return
        activeDrag = {
          ...pendingRef.current,
          x: t.clientX,
          y: t.clientY,
        }
        setPendingDrag(null)
        setDrag(activeDrag)
      } else if (activeDrag) {
        activeDrag = { ...activeDrag, x: t.clientX, y: t.clientY }
        setDrag(activeDrag)
      }

      if (!activeDrag) return
      if (touchEvent?.cancelable) touchEvent.preventDefault()

      const rails = rootRef.current?.querySelectorAll('[data-zone]') ?? []
      let found: string | null = null
      for (const r of rails) {
        const rc = r.getBoundingClientRect()
        if (t.clientX >= rc.left && t.clientX <= rc.right && t.clientY >= rc.top && t.clientY <= rc.bottom) {
          found = r.getAttribute('data-zone')
          break
        }
      }
      setHoverZone(found)
    }

    const up = () => {
      const d = dragRef.current
      const h = hoverRef.current
      if (d && h && h !== d.fromZone) {
        const to = h as JamlZone
        const f = filterRef.current
        onChangeRef.current({
          ...f,
          [d.fromZone]: f[d.fromZone].filter((c) => c.id !== d.clause.id),
          [to]: [...f[to], { ...d.clause }],
        })
      }
      setPendingDrag(null)
      setDrag(null)
      setHoverZone(null)
    }

    window.addEventListener('mousemove', move)
    window.addEventListener('mouseup', up)
    window.addEventListener('touchmove', move, { passive: false })
    window.addEventListener('touchend', up)

    return () => {
      window.removeEventListener('mousemove', move)
      window.removeEventListener('mouseup', up)
      window.removeEventListener('touchmove', move)
      window.removeEventListener('touchend', up)
    }
  }, [active, rootRef])

  return {
    drag,
    hoverZone,
    onDragStart,
  }
}

/**
 * Provides a magnetic 3D tilt effect for DOM elements, replicating the hover-follow
 * of Balatro cards. Continuous (tracks the cursor). Pure CSS transform — no 3D deps.
 */
export function useDOMMagneticTilt(enabled: boolean = true) {
  const [isHovered, setIsHovered] = useState(false)
  const [transform, setTransform] = useState('none')

  const onPointerEnter = (event: React.PointerEvent) => {
    if (!enabled || event.pointerType === 'touch') return
    setIsHovered(true)
  }

  const onPointerLeave = () => {
    if (!enabled) return
    setIsHovered(false)
    setTransform('none')
  }

  const onPointerMove = (event: React.PointerEvent) => {
    if (!enabled || event.pointerType === 'touch') return
    const rect = event.currentTarget.getBoundingClientRect()
    const x = event.clientX - rect.left
    const y = event.clientY - rect.top
    const rotateY = (x / rect.width) * 12 - 6
    const rotateX = (y / rect.height) * -16 + 8
    const juiceScale = 1.05
    const juiceY = -2 // slight move up
    setTransform(`perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale(${juiceScale}) translateY(${juiceY}px)`)
  }

  const handlers = {
    onPointerEnter: enabled ? onPointerEnter : undefined,
    onPointerLeave: enabled ? onPointerLeave : undefined,
    onPointerMove: enabled ? onPointerMove : undefined,
  }

  const tiltStyle: React.CSSProperties = {
    transition: enabled && !isHovered ? 'transform 0.4s ease, box-shadow 0.4s ease-out' : 'transform 0.1s ease-out',
    transform: enabled ? (isHovered ? transform : 'none') : undefined,
    transformStyle: enabled ? 'preserve-3d' : undefined,
    transformOrigin: enabled ? 'center center' : undefined,
    willChange: enabled ? 'transform' : undefined,
    pointerEvents: enabled ? 'none' : undefined,
  }

  return { handlers, tiltStyle, isHovered }
}
