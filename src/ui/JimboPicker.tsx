'use client'

import React from 'react'

// ─── Picker primitives ───────────────────────────────────────────────────────
// These own the `j-picker*` structure classes so feature components
// (JokerPicker, CategoryPicker, …) compose NAMED Jimbo parts instead of raw
// `<div className="j-picker__…">`. The raw DOM tag lives here, exactly once,
// with a real name — never in the feature layer.

export type JimboPickerProps = React.HTMLAttributes<HTMLDivElement>

/** The picker shell — vertical stack of sections/search/grid. */
export function JimboPicker({ className = '', ...props }: JimboPickerProps) {
  return <div className={`j-picker ${className}`.trim()} {...props} />
}

/** A titled grouping within a picker (e.g. the Legendary row). */
export function JimboPickerSection({ className = '', ...props }: JimboPickerProps) {
  return <div className={`j-picker__section ${className}`.trim()} {...props} />
}

export interface JimboPickerGridProps extends JimboPickerProps {
  /** Wider cells for the legendary row. */
  legendary?: boolean
  /** Scrollable body with hidden scrollbar. */
  scroll?: boolean
}

/** The sprite grid. */
export function JimboPickerGrid({ legendary, scroll, className = '', ...props }: JimboPickerGridProps) {
  const classes = [
    'j-picker__grid',
    legendary && 'j-picker__grid--legendary',
    scroll && 'hide-scrollbar',
    className,
  ].filter(Boolean).join(' ')
  return <div className={classes} {...props} />
}

export interface JimboPickerItemProps extends JimboPickerProps {
  /** Dimmed (e.g. filtered-out half of a voucher pair). */
  muted?: boolean
}

/** A single clickable sprite cell. */
export function JimboPickerItem({ muted, className = '', ...props }: JimboPickerItemProps) {
  return (
    <div
      className={`j-picker__item j-juice-hover ${className}`.trim()}
      data-muted={muted}
      {...props}
    />
  )
}

/** Wrapper for the search field row. */
export function JimboPickerSearch({ className = '', ...props }: JimboPickerProps) {
  return <div className={`j-picker__search ${className}`.trim()} {...props} />
}

/** A base/upgrade pair cell (vouchers). */
export function JimboPickerPair({ className = '', ...props }: JimboPickerProps) {
  return <div className={`j-picker__pair ${className}`.trim()} {...props} />
}

/** Empty / no-matches state inside the grid. */
export function JimboPickerEmpty({ className = '', ...props }: JimboPickerProps) {
  return <div className={`j-picker__empty ${className}`.trim()} {...props} />
}

/** Recessed hint panel above a grid. */
export function JimboPickerHint({ className = '', ...props }: JimboPickerProps) {
  return <div className={`j-inner-panel j-picker__hint ${className}`.trim()} {...props} />
}
