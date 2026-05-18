// PORTABLE — intended for jaml-ui/src/ui/jimboInputModal.tsx
// On paste, replace `from 'jaml-ui'` with `from './panel.js'`/`./tokens.js` as appropriate.
"use client";

import { JimboButton, JimboModal } from "./panel.js";
import { JimboText } from "./jimboText.js";
import { JimboTextInput } from "./JimboTextInput.js";
import { useEffect, useRef, useState } from "react";

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
          className="j-input-modal__message"
          tone="grey"
        >
          {message}
        </JimboText>
      )}
      <JimboTextInput
        invalid={!!error}
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
        type="text"
        value={value}
      />
      {error && (
        <JimboText
          size="sm"
          className="j-input-modal__error"
          tone="red"
        >
          {error}
        </JimboText>
      )}
      <div className="j-input-modal__actions">
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
