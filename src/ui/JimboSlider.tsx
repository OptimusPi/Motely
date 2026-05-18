'use client'

import React from 'react'
import { JimboText } from './jimboText.js'

export interface JimboSliderProps {
  value: number
  min?: number
  max?: number
  step?: number
  label?: string
  onChange?: (value: number) => void
  className?: string
  id?: string
}

export function JimboSlider({
  value,
  min = 0,
  max = 100,
  step = 1,
  label,
  onChange,
  className = '',
  id,
}: JimboSliderProps) {
  const pct = max === min ? 0 : ((value - min) / (max - min)) * 100
  const generatedId = React.useId()
  const inputId = id ?? generatedId

  return (
    <div className={`j-slider-wrap ${className}`}>
      {label && (
        <label htmlFor={inputId} className="j-slider__label">
          <JimboText size="sm" tone="white">{label}</JimboText>
        </label>
      )}
      <div className="j-slider">
        <div className="j-slider__track" aria-hidden>
          <div className="j-slider__fill" style={{ width: `${pct}%` }} />
        </div>
        <input
          id={inputId}
          type="range"
          className="j-slider__input"
          min={min}
          max={max}
          step={step}
          value={value}
          onChange={(e) => onChange?.(Number(e.currentTarget.value))}
        />
        <div className="j-slider__value">
          <JimboText size="xs" tone="white">{Math.round(value)}</JimboText>
        </div>
      </div>
    </div>
  )
}
