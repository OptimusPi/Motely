'use client'

import React from 'react'
import { JimboText } from './jimboText.js'
import { JimboButton } from './panel.js'

export interface JimboPanelSpinnerProps {
  label?: React.ReactNode
  title: React.ReactNode
  description?: React.ReactNode
  media?: React.ReactNode
  meta?: React.ReactNode
  className?: string
  style?: React.CSSProperties
  onPrev?: () => void
  onNext?: () => void
  prevDisabled?: boolean
  nextDisabled?: boolean
}

export function JimboPanelSpinner({
  label,
  title,
  description,
  media,
  meta,
  className = '',
  style,
  onPrev,
  onNext,
  prevDisabled = false,
  nextDisabled = false,
}: JimboPanelSpinnerProps) {
  return (
    <div className={`j-panel-spinner ${className}`.trim()} style={style}>
      {label ? <div className="j-panel-spinner__label"><JimboText size="xs" tone="grey">{label}</JimboText></div> : null}
      <div className="j-panel-spinner__row">
        <JimboButton
          tone="red"
          size="lg"
          className="j-panel-spinner__arrow j-panel-spinner__arrow--left"
          onClick={onPrev}
          disabled={prevDisabled}
          aria-label="Previous"
        >
          {'<'}
        </JimboButton>
        <div className="j-panel-spinner__panel">
          {media ? <div className="j-panel-spinner__media">{media}</div> : null}
          <div className="j-panel-spinner__title"><JimboText size="md" tone="white">{title}</JimboText></div>
          {meta ? <div className="j-panel-spinner__meta">{meta}</div> : null}
          {description ? <div className="j-panel-spinner__description"><JimboText size="micro" tone="grey">{description}</JimboText></div> : null}
        </div>
        <JimboButton
          tone="red"
          size="lg"
          className="j-panel-spinner__arrow j-panel-spinner__arrow--right"
          onClick={onNext}
          disabled={nextDisabled}
          aria-label="Next"
        >
          {'>'}
        </JimboButton>
      </div>
    </div>
  )
}
