// PORTABLE — intended for jaml-ui/src/ui/jimboInputModal.tsx
// On paste, replace `from 'jaml-ui'` with `from './panel.js'`/`./tokens.js` as appropriate.
"use client";

import { JimboButton, JimboModal } from "./panel.js";
import { JimboColorOption } from "./tokens.js";
import { JimboText } from "./jimboText.js";
import { useEffect, useRef, useState } from "react";

const C = JimboColorOption;

export interface JimboInputModalProps {
  cancelLabel?: string;
  confirmLabel?: string;
  initialValue?: string;
  message?: string;
  onCancel: () => void;
  onConfirm: (value: string) => void;
  open: boolean;
  placeholder?: string;
  title: string;
  validate?: (value: string) => string | null;
}

export function JimboInputModal({
  open,
  title,
  message,
  placeholder,
  initialValue = "",
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  validate,
  onConfirm,
  onCancel,
}: JimboInputModalProps) {
  const [value, setValue] = useState(initialValue);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const [prevOpen, setPrevOpen] = useState(open);

  if (open !== prevOpen) {
    setPrevOpen(open);
    if (open) {
      setValue(initialValue);
      setError(null);
    }
  }

  useEffect(() => {
    if (open) {
      const t = setTimeout(() => inputRef.current?.focus(), 30);
      return () => clearTimeout(t);
    }
  }, [open]);

  function submit() {
    const err = validate?.(value) ?? null;
    if (err) {
      setError(err);
      return;
    }
    onConfirm(value);
  }

  return (
    <JimboModal onClose={onCancel} open={open} title={title}>
      {message && (
        <JimboText
          size="sm"
          style={{ display: "block", marginBottom: 8 }}
          tone="grey"
        >
          {message}
        </JimboText>
      )}
      <input
        aria-invalid={error ? "true" : "false"}
        onChange={(e) => {
          setValue(e.target.value);
          if (error) {
            setError(null);
          }
        }}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            submit();
          }
          if (e.key === "Escape") {
            onCancel();
          }
        }}
        placeholder={placeholder}
        ref={inputRef}
        style={{
          width: "100%",
          padding: "8px 10px",
          background: C.DARKEST,
          color: C.WHITE,
          border: `1px solid ${error ? C.RED : C.PANEL_EDGE}`,
          borderRadius: 4,
          fontSize: 13,
          fontFamily: "m6x11plus, monospace",
          letterSpacing: 1,
          outline: "none",
          boxSizing: "border-box",
        }}
        type="text"
        value={value}
      />
      {error && (
        <JimboText
          size="sm"
          style={{ display: "block", marginTop: 6 }}
          tone="red"
        >
          {error}
        </JimboText>
      )}
      <div
        style={{
          display: "flex",
          justifyContent: "flex-end",
          gap: 8,
          marginTop: 14,
        }}
      >
        <JimboButton onClick={onCancel} size="sm" tone="red">
          {cancelLabel}
        </JimboButton>
        <JimboButton onClick={submit} size="sm" tone="blue">
          {confirmLabel}
        </JimboButton>
      </div>
    </JimboModal>
  );
}
