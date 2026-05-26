"use client";

import React, { useCallback, useRef, useState } from "react";
import { JimboText } from "../ui/jimboText.js";
import { JimboTextInput } from "../ui/JimboTextInput.js";
import { normalizeJamlSeed } from "./jamlSeedUtils.js";

export type JamlSeedInputVariant = "normal" | "dark" | "alt";

export interface JamlSeedInputProps {
  value?: string;
  onChange?: (seed: string) => void;
  placeholder?: string;
  label?: React.ReactNode;
  variant?: JamlSeedInputVariant;
  className?: string;
  style?: React.CSSProperties;
  autoFocus?: boolean;
  onKeyDown?: React.KeyboardEventHandler<HTMLInputElement>;
  onBlur?: React.FocusEventHandler<HTMLInputElement>;
  onFocus?: React.FocusEventHandler<HTMLInputElement>;
  disabled?: boolean;
  name?: string;
  id?: string;
  title?: string;
  "aria-label"?: string;
}

/**
 * Balatro-style seed input constrained to the real 8-character format.
 */
export const JamlSeedInput = React.forwardRef<HTMLInputElement, JamlSeedInputProps>(function JamlSeedInput(
  {
    value,
    onChange,
    placeholder = "Aleeb",
    label = "Seed",
    variant = "normal",
    className,
    style,
    autoFocus,
    onKeyDown,
    onBlur,
    onFocus,
    disabled = false,
    name,
    id,
    title,
    "aria-label": ariaLabel,
  }: JamlSeedInputProps,
  forwardedRef,
) {
  const [internal, setInternal] = useState(() => normalizeJamlSeed(value ?? ""));
  const inputRef = useRef<HTMLInputElement | null>(null);
  const display = value === undefined ? internal : normalizeJamlSeed(value);
  const validState = display.length === 8 ? "true" : "partial";

  const setRefs = useCallback((node: HTMLInputElement | null) => {
    inputRef.current = node;
    if (typeof forwardedRef === "function") {
      forwardedRef(node);
    } else if (forwardedRef) {
      forwardedRef.current = node;
    }
  }, [forwardedRef]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const raw = normalizeJamlSeed(e.target.value);
    setInternal(raw);
    onChange?.(raw);
  };

  return (
    <div className={`j-seed-input j-seed-input--${variant} ${className ?? ""}`.trim()} style={style}>
      {label ? <JimboText size="xs" tone="grey">{label}</JimboText> : null}
      <div
        className="j-seed-input__shell"
        data-valid={validState}
        onClick={() => inputRef.current?.focus()}
        title={title}
      >
        <JimboTextInput
          ref={setRefs}
          type="text"
          className="j-seed-input__field"
          value={display}
          onChange={handleChange}
          onKeyDown={onKeyDown}
          onBlur={onBlur}
          onFocus={onFocus}
          placeholder={placeholder}
          maxLength={8}
          spellCheck={false}
          autoComplete="off"
          autoCorrect="off"
          autoCapitalize="characters"
          inputMode="text"
          enterKeyHint="done"
          autoFocus={autoFocus}
          disabled={disabled}
          name={name}
          id={id}
          aria-label={ariaLabel ?? (typeof label === "string" ? label : "Seed")}
        />
      </div>
    </div>
  );
});
