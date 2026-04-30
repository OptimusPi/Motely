import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react'
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

  useEffect(() => {
    if (open) {
      setVisible(true)
      const frame = requestAnimationFrame(() => setOpacity(1))
      return () => cancelAnimationFrame(frame)
    } else {
      setOpacity(0)
      const t = setTimeout(() => setVisible(false), delay)
      return () => clearTimeout(t)
    }
  }, [open, delay])

  return { visible, opacity }
}

/**
 * Hook for the Balatro hypnotic swirl background.
 * Manages WebGL context, shader compilation, and animation loop.
 */
export function useBalatroBackground() {
  const canvasRef = useRef<HTMLCanvasElement>(null)

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

      const float SPIN_ROTATION = -2.0;
      const float SPIN_SPEED = 4.5;
      const vec4 COLOUR_1 = vec4(1.0, 0.2, 0.2, 1.0);
      const vec4 COLOUR_2 = vec4(0.0, 0.5, 1.0, 1.0);
      const vec4 COLOUR_3 = vec4(0.05, 0.08, 0.1, 1.0);
      const float CONTRAST = 4.5;
      const float LIGTHING = 0.5;
      const float SPIN_AMOUNT = 0.35;
      const float PIXEL_FILTER = 1024.0;
      const float PI = 3.14159265359;

      void main() {
        vec2 screenSize = u_resolution;
        float pixel_size = length(screenSize.xy) / PIXEL_FILTER;
        vec2 uv = (floor(gl_FragCoord.xy*(1.0/pixel_size))*pixel_size - 0.5*screenSize.xy)/length(screenSize.xy);
        float uv_len = length(uv);

        float speed = (SPIN_ROTATION * 0.2) + 302.2;
        float new_pixel_angle = atan(uv.y, uv.x) + speed - 20.0*(1.0*SPIN_AMOUNT*uv_len + (1.0 - 1.0*SPIN_AMOUNT));

        vec2 mid = (screenSize.xy/length(screenSize.xy))/2.0;
        uv = (vec2((uv_len * cos(new_pixel_angle) + mid.x), (uv_len * sin(new_pixel_angle) + mid.y)) - mid);

        uv *= 30.0;
        speed = u_time * SPIN_SPEED;
        vec2 uv2 = vec2(uv.x, uv.y);

        for(int i=0; i < 5; i++) {
          uv2 += sin(max(uv.x, uv.y)) + uv;
          uv  += 0.5*vec2(cos(5.1123314 + 0.353*uv2.y + speed*0.131121), sin(uv2.x - 0.113*speed));
          uv  -= 1.0*cos(uv.x + uv.y) - 1.0*sin(uv.x*0.711 - uv.y);
        }

        float contrast_mod = (0.25*CONTRAST + 0.5*SPIN_AMOUNT + 1.2);
        float paint_res = min(2.0, max(0.0, length(uv)*(0.035)*contrast_mod));
        float c1p = max(0.0, 1.0 - contrast_mod*abs(1.0 - paint_res));
        float c2p = max(0.0, 1.0 - contrast_mod*abs(paint_res));
        float c3p = 1.0 - min(1.0, c1p + c2p);
        float light = (LIGTHING - 0.2)*max(c1p*5.0 - 4.0, 0.0) + LIGTHING*max(c2p*5.0 - 4.0, 0.0);

        vec4 finalCol = (0.3/CONTRAST)*COLOUR_1 + (1.0 - 0.3/CONTRAST)*(COLOUR_1*c1p + COLOUR_2*c2p + vec4(c3p*COLOUR_3.rgb, c3p*COLOUR_1.a)) + light;

        gl_FragColor = finalCol;
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
        -1.0, -1.0,  1.0, -1.0, -1.0, 1.0,
        -1.0,  1.0,  1.0, -1.0,  1.0, 1.0,
      ]),
      gl.STATIC_DRAW,
    )

    const positionLocation = gl.getAttribLocation(program, 'position')
    gl.enableVertexAttribArray(positionLocation)
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0)

    const timeLocation = gl.getUniformLocation(program, 'u_time')
    const resolutionLocation = gl.getUniformLocation(program, 'u_resolution')

    const startTime = Date.now()
    let animationFrameId: number

    const render = () => {
      const displayWidth = canvas.clientWidth
      const displayHeight = canvas.clientHeight
      if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
        canvas.width = displayWidth
        canvas.height = displayHeight
        gl.viewport(0, 0, gl.drawingBufferWidth, gl.drawingBufferHeight)
      }

      const currentTime = (Date.now() - startTime) / 1000.0
      gl.uniform1f(timeLocation, currentTime)
      gl.uniform2f(resolutionLocation, canvas.width, canvas.height)

      gl.drawArrays(gl.TRIANGLES, 0, 6)
      animationFrameId = requestAnimationFrame(render)
    }

    render()

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
  const imageCacheRef = useRef<Map<string, HTMLImageElement>>(new Map())
  const [ratio, setRatio] = useState(3 / 4)
  const [, forceUpdate] = useState(0)
  const animationFrameRef = useRef<number | null>(null)
  const [elapsed, setElapsed] = useState(0)
  const [isHovered, setIsHovered] = useState(false)
  const [transform, setTransform] = useState('none')

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

  // Animation loop for animated layers
  useEffect(() => {
    if (!hasAnimatedLayer) return

    let startTime: number
    const animate = (timestamp: number) => {
      if (!startTime) startTime = timestamp
      const now = timestamp - startTime
      if (!animationFrameRef.current || timestamp - 100 > animationFrameRef.current) {
        animationFrameRef.current = timestamp
        setElapsed(now)
      }
      animationFrameRef.current = requestAnimationFrame(animate)
    }

    animationFrameRef.current = requestAnimationFrame(animate)
    return () => {
      if (animationFrameRef.current) cancelAnimationFrame(animationFrameRef.current)
    }
  }, [hasAnimatedLayer])

  // Core drawing logic
  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas || !layers || layers.length === 0) return
    const context = canvas.getContext('2d')
    if (!context) return
    let cancelled = false

    context.clearRect(0, 0, canvas.width, canvas.height)
    ;[...layers]
      .sort((a, b) => a.order - b.order)
      .forEach((layer) => {
        if (imageCacheRef.current.has(layer.source)) {
          const image = imageCacheRef.current.get(layer.source)
          if (!image) return
          const imageRatio = renderImage(canvas, context, image, layer, hasAnimatedLayer ? elapsed : undefined)
          if (layer.order === 0) setRatio(imageRatio)
          return
        }
        loadImage(layer.source).then((img) => {
          if (cancelled || !img) return
          const imageRatio = renderImage(canvas, context, img, layer, hasAnimatedLayer ? elapsed : undefined)
          imageCacheRef.current.set(layer.source, img)
          if (layer.order === 0) setRatio(imageRatio)
          forceUpdate((prev) => prev + 1)
        })
      })

    canvas.style.filter = invert ? 'invert(0.94)' : 'none'
    return () => { cancelled = true }
  }, [layers, elapsed, invert, hasAnimatedLayer])

  const onPointerEnter = (event: React.PointerEvent) => {
    if (!hoverTilt || event.pointerType === 'touch') return
    setIsHovered(true)
  }

  const onPointerLeave = () => {
    if (!hoverTilt) return
    setIsHovered(false)
    setTransform('none')
  }

  const onPointerMove = (event: React.PointerEvent<HTMLDivElement>) => {
    if (!hoverTilt || event.pointerType === 'touch') return
    const rect = event.currentTarget.getBoundingClientRect()
    const x = event.clientX - rect.left
    const y = event.clientY - rect.top
    const rotateY = (x / rect.width) * 12 - 6
    const rotateX = (y / rect.height) * -16 + 8
    const juiceScale = 1.05
    const juiceY = -2 // slight move up
    setTransform(`perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale(${juiceScale}) translateY(${juiceY}px)`)
  }

  const containerStyle: React.CSSProperties = {
    aspectRatio: String(ratio),
    width: '100%',
    display: 'flex',
    perspective: hoverTilt ? '1000px' : undefined,
    userSelect: 'none',
    WebkitUserSelect: 'none',
  }

  const canvasStyle: React.CSSProperties = {
    transition: hoverTilt && !isHovered ? 'transform 0.4s ease, box-shadow 0.4s ease-out' : 'transform 0.1s ease-out',
    transform: hoverTilt ? (isHovered ? transform : 'none') : undefined,
    transformStyle: hoverTilt ? 'preserve-3d' : undefined,
    transformOrigin: hoverTilt ? 'center center' : undefined,
    borderRadius: '6px',
    boxShadow: hoverTilt && isHovered ? '0 2px 12px rgba(0,0,0,0.3)' : '0 2px 8px rgba(0,0,0,0.2)',
    imageRendering: 'pixelated',
    pointerEvents: 'none',
  }

  return {
    canvasRef,
    containerStyle,
    canvasStyle,
    handlers: {
      onPointerEnter: hoverTilt ? onPointerEnter : undefined,
      onPointerLeave: hoverTilt ? onPointerLeave : undefined,
      onPointerMove: hoverTilt ? onPointerMove : undefined,
    }
  }
}

/**
 * Tracks which 'ante' section is currently most visible in a scrollable container.
 * Used in JamlAnalyzerFullscreen and AnalyzerExplorer.
 */
export function useAnteTracker(antes: { ante: number }[], options: { threshold?: number[] } = {}) {
  const [currentAnte, setCurrentAnte] = useState(antes[0]?.ante ?? 0)
  const scrollRef = useRef<HTMLDivElement>(null)
  const anteRefs = useRef<Map<number, HTMLElement>>(new Map())

  useEffect(() => {
    // Reset to first ante when list changes
    if (antes.length > 0) {
      setCurrentAnte(antes[0].ante)
    }
  }, [antes])

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

/**
 * Manages drag-and-drop state for the Jaml IDE visual filter editor.
 */
export function useJamlIdeDrag(
  filter: JamlVisualFilter,
  onChange: (filter: JamlVisualFilter) => void,
  rootRef: React.RefObject<HTMLDivElement | null>
) {
  const [drag, setDrag] = useState<DragState | null>(null)
  const [hoverZone, setHoverZone] = useState<string | null>(null)

  const onDragStart = useCallback(
    (e: React.MouseEvent | React.TouchEvent, clause: JamlVisualClause, fromZone: JamlZone) => {
      const t = 'touches' in e ? e.touches[0] : e
      const rect = (e.currentTarget as HTMLElement).getBoundingClientRect()
      setDrag({
        clause,
        fromZone,
        x: t.clientX,
        y: t.clientY,
        offX: t.clientX - rect.left,
        offY: t.clientY - rect.top,
      })
    },
    []
  )

  useEffect(() => {
    if (!drag) return

    const move = (e: MouseEvent | TouchEvent) => {
      const t = 'touches' in e ? (e as TouchEvent).touches[0] : (e as MouseEvent)
      setDrag((d) => d && { ...d, x: t.clientX, y: t.clientY })

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
      if (hoverZone && hoverZone !== drag.fromZone) {
        const to = hoverZone as JamlZone
        onChange({
          ...filter,
          [drag.fromZone]: filter[drag.fromZone].filter((c) => c.id !== drag.clause.id),
          [to]: [...filter[to], { ...drag.clause }],
        })
      }
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
  }, [drag, hoverZone, filter, onChange, rootRef])

  return {
    drag,
    hoverZone,
    onDragStart,
  }
}

/**
 * Provides a magnetic 3D tilt effect for DOM elements, replicating the 'juice' of Balatro cards.
 * Ensures the hit-detection area remains stable by separating container events from the transformed style.
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
