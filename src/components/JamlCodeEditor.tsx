"use client";

import React, { useEffect, useRef } from "react";
import { EditorView, keymap, lineNumbers, highlightActiveLine, drawSelection, placeholder as cmPlaceholder } from "@codemirror/view";
import { EditorState } from "@codemirror/state";
import { yaml } from "@codemirror/lang-yaml";
import { defaultKeymap, history, historyKeymap, indentWithTab } from "@codemirror/commands";
import { syntaxHighlighting, HighlightStyle } from "@codemirror/language";
import { tags } from "@lezer/highlight";
import { JimboColorOption } from "../ui/tokens.js";

const balatroHighlight = HighlightStyle.define([
  { tag: tags.comment, color: JimboColorOption.GREY, fontStyle: "italic" },
  { tag: tags.keyword, color: JimboColorOption.RED },
  { tag: tags.string, color: JimboColorOption.GOLD_TEXT },
  { tag: tags.number, color: JimboColorOption.BLUE },
  { tag: tags.bool, color: JimboColorOption.BLUE },
  { tag: tags.null, color: JimboColorOption.GREY },
  { tag: tags.propertyName, color: JimboColorOption.GREEN_TEXT },
  { tag: tags.typeName, color: JimboColorOption.GREEN_TEXT },
]);

const balatroTheme = EditorView.theme(
  {
    "&": {
      backgroundColor: JimboColorOption.DARKEST,
      color: JimboColorOption.WHITE,
      fontSize: "13px",
      height: "100%",
    },
    ".cm-content": {
      fontFamily: "var(--j-font-code, 'JetBrains Mono', ui-monospace, monospace)",
      lineHeight: "22px",
      padding: "12px 0",
      caretColor: JimboColorOption.GOLD_TEXT,
      minHeight: "100%",
    },
    ".cm-gutters": {
      backgroundColor: JimboColorOption.DARKEST,
      color: JimboColorOption.GREY,
      border: "none",
    },
    ".cm-lineNumbers .cm-gutterElement": {
      minWidth: "2ch",
      padding: "0 6px 0 8px",
    },
    ".cm-activeLineGutter": {
      color: JimboColorOption.GOLD_TEXT,
      backgroundColor: "transparent",
    },
    ".cm-activeLine": {
      backgroundColor: `${JimboColorOption.BLACK}20`,
    },
    ".cm-selectionBackground": {
      backgroundColor: `${JimboColorOption.WHITE}20 !important`,
    },
    "&.cm-focused .cm-selectionBackground": {
      backgroundColor: `${JimboColorOption.WHITE}20`,
    },
    ".cm-cursor, .cm-dropCursor": {
      borderLeftColor: JimboColorOption.GOLD_TEXT,
    },
    ".cm-scroller": {
      overflow: "auto",
    },
    ".cm-placeholder": {
      color: JimboColorOption.GREY,
      fontStyle: "italic",
    },
  },
  { dark: true },
);

export interface JamlCodeEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  minHeight?: number;
}

export function JamlCodeEditor({
  value,
  onChange,
  placeholder = "",
  minHeight = 320,
}: JamlCodeEditorProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const viewRef = useRef<EditorView | null>(null);
  const suppressEmitRef = useRef(false);
  const lastSyncedValueRef = useRef(value);
  const onChangeRef = useRef(onChange);
  useEffect(() => { onChangeRef.current = onChange; });

  useEffect(() => {
    if (!containerRef.current) return;

    const view = new EditorView({
      state: EditorState.create({
        doc: lastSyncedValueRef.current,
        extensions: [
          history(),
          lineNumbers(),
          highlightActiveLine(),
          drawSelection(),
          yaml(),
          syntaxHighlighting(balatroHighlight),
          balatroTheme,
          EditorView.lineWrapping,
          keymap.of([indentWithTab, ...defaultKeymap, ...historyKeymap]),
          ...(placeholder ? [cmPlaceholder(placeholder)] : []),
          EditorView.updateListener.of((update) => {
            if (update.docChanged && !suppressEmitRef.current) {
              onChangeRef.current(update.state.doc.toString());
            }
          }),
        ],
      }),
      parent: containerRef.current,
    });

    viewRef.current = view;
    return () => {
      view.destroy();
      viewRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const view = viewRef.current;
    if (!view) return;

    const current = view.state.doc.toString();
    if (current === value) {
      lastSyncedValueRef.current = value;
      return;
    }

    suppressEmitRef.current = true;
    try {
      // Streaming-friendly: append only the suffix when new value extends current.
      if (value.length > current.length && value.startsWith(current)) {
        view.dispatch({
          changes: { from: view.state.doc.length, insert: value.slice(current.length) },
        });
      } else {
        view.dispatch({
          changes: { from: 0, to: view.state.doc.length, insert: value },
        });
      }
    } finally {
      suppressEmitRef.current = false;
      lastSyncedValueRef.current = value;
    }
  }, [value]);

  return (
    <div
      ref={containerRef}
      style={{ width: "100%", minHeight, background: JimboColorOption.DARKEST }}
    />
  );
}
