'use client'

import React, { useEffect, useRef, useState } from 'react'
import { JimboColorOption } from './tokens.js'

export type JimboTooltipMode = 'snap' | 'mouse'

export type JimboTooltipPlacement = 'top' | 'bottom' | 'auto'

export interface JimboTooltipProps {
  /** Content rendered inside the tooltip panel. Typically a card's ability text, joker description, etc. */
  content: React.ReactNode
  /** The target element the tooltip anchors to. */
  children: React.ReactElement
  /** `snap` (default): tooltip sits above/below the target's bounding rect.
   *  `mouse`: tooltip follows the mouse position while hovering. */
  mode?: JimboTooltipMode
  /** Only used in `snap` mode. Default `auto` picks top if there's room, else bottom. */
  placement?: JimboTooltipPlacement
  /** Hover delay in ms before the tooltip appears. Default 80. */
  delay?: number
  /** Max width of the tooltip panel (px). Default 280. */
  maxWidth?: number
  /** Disable the tooltip entirely (e.g. when the target is disabled). */
  disabled?: boolean
}

/**
 * Canonical Balatro-style tooltip: dark panel, silver border, pixel font.
 * Wrap any target to get a hover/focus popover.
 *
 *   <JimboTooltip content={<JimboText size="sm">Gains +4 mult per face card</JimboText>}>
 *     <GameCard joker="Blueprint" />
 *   </JimboTooltip>
 *
 * Modes:
 *  - `snap` (default): anchored to the target's bounding rect, chooses
 *    top or bottom automatically so it stays in viewport.
 *  - `mouse`: follows the mouse position — useful for large targets
 *    (full card fans, zone rails) where "above the element" is imprecise.
 */
export function JimboTooltip({
  content,
  children,
  mode = 'snap',
  placement = 'auto',
  delay = 80,
  maxWidth = 280,
  disabled = false,
}: JimboTooltipProps) {
  const [visible, setVisible] = useState(false)
  const [pos, setPos] = useState<{ left: number; top: number; align: 'top' | 'bottom' } | null>(null)
  const targetRef = useRef<HTMLElement | null>(null)
  const tooltipRef = useRef<HTMLDivElement | null>(null)
  const delayTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  useEffect(() => () => {
    if (delayTimerRef.current) clearTimeout(delayTimerRef.current)
  }, [])

  const show = () => {
    if (disabled) return
    if (delayTimerRef.current) clearTimeout(delayTimerRef.current)
    delayTimerRef.current = setTimeout(() => setVisible(true), delay)
  }

  const hide = () => {
    if (delayTimerRef.current) clearTimeout(delayTimerRef.current)
    setVisible(false)
    setPos(null)
  }

  const computeSnapPos = () => {
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
    setPos({ left: Math.max(8, Math.min(window.innerWidth - tipRect.width - 8, left)), top, align })
  }

  useEffect(() => {
    if (!visible || mode !== 'snap') return
    // Recompute after the tooltip renders so its size is known.
    const raf = requestAnimationFrame(computeSnapPos)
    window.addEventListener('resize', computeSnapPos)
    window.addEventListener('scroll', computeSnapPos, true)
    return () => {
      cancelAnimationFrame(raf)
      window.removeEventListener('resize', computeSnapPos)
      window.removeEventListener('scroll', computeSnapPos, true)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible, mode, placement])

  const handleMouseMove = (e: React.MouseEvent) => {
    if (mode !== 'mouse') return
    setPos({ left: e.clientX + 12, top: e.clientY + 16, align: 'bottom' })
  }

  const child = React.Children.only(children) as React.ReactElement<{
    ref?: React.Ref<HTMLElement>
    onMouseEnter?: (e: React.MouseEvent) => void
    onMouseLeave?: (e: React.MouseEvent) => void
    onFocus?: (e: React.FocusEvent) => void
    onBlur?: (e: React.FocusEvent) => void
    onMouseMove?: (e: React.MouseEvent) => void
  }>

  const refHandler = (node: HTMLElement | null) => {
    targetRef.current = node
    const childRef = (child as unknown as { ref?: React.Ref<HTMLElement> }).ref
    if (typeof childRef === 'function') childRef(node)
    else if (childRef && 'current' in childRef) (childRef as React.MutableRefObject<HTMLElement | null>).current = node
  }

  const wrapped = React.cloneElement(child, {
    ref: refHandler,
    onMouseEnter: (e: React.MouseEvent) => { show(); child.props.onMouseEnter?.(e) },
    onMouseLeave: (e: React.MouseEvent) => { hide(); child.props.onMouseLeave?.(e) },
    onFocus:      (e: React.FocusEvent) => { show(); child.props.onFocus?.(e) },
    onBlur:       (e: React.FocusEvent) => { hide(); child.props.onBlur?.(e) },
    onMouseMove:  (e: React.MouseEvent) => { handleMouseMove(e); child.props.onMouseMove?.(e) },
  })

  return (
    <>
      {wrapped}
      {visible ? (
        <div
          ref={tooltipRef}
          role="tooltip"
          style={{
            position: 'fixed',
            left: pos?.left ?? -9999,
            top: pos?.top ?? -9999,
            maxWidth,
            padding: '6px 10px',
            borderRadius: 6,
            background: JimboColorOption.DARKEST,
            border: `2px solid ${JimboColorOption.BORDER_SILVER}`,
            boxShadow: `0 2px 0 ${JimboColorOption.BLACK}cc`,
            color: JimboColorOption.WHITE,
            pointerEvents: 'none',
            zIndex: 10000,
            opacity: pos ? 1 : 0,
            transition: 'opacity 120ms ease',
          }}
        >
          {content}
        </div>
      ) : null}
    </>
  )
}
