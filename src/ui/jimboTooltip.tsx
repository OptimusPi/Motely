import React, { useCallback } from 'react'
import { useJimboTooltip, type JimboTooltipMode, type JimboTooltipPlacement } from './hooks.js'

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
  const {
    visible,
    pos,
    targetRef,
    tooltipRef,
    show,
    hide,
    handleMouseMove,
  } = useJimboTooltip({ mode, placement, delay, disabled })

  const child = React.Children.only(children) as React.ReactElement<{
    ref?: React.Ref<HTMLElement>
    onMouseEnter?: (e: React.MouseEvent) => void
    onMouseLeave?: (e: React.MouseEvent) => void
    onFocus?: (e: React.FocusEvent) => void
    onBlur?: (e: React.FocusEvent) => void
    onMouseMove?: (e: React.MouseEvent) => void
  }>

  const refHandler = useCallback((node: HTMLElement | null) => {
    targetRef.current = node
    const childRef = (child as any).ref
    if (typeof childRef === 'function') childRef(node)
    else if (childRef && 'current' in childRef) childRef.current = node
  }, [child, targetRef])

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
          className="j-tooltip"
          style={{
            left: pos?.left ?? -9999,
            top: pos?.top ?? -9999,
            maxWidth,
            opacity: pos ? 1 : 0,
          }}
        >
          {content}
        </div>
      ) : null}
    </>
  )
}
