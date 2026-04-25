"use client";

import React, { useEffect, useRef } from "react";
import Editor, { type BeforeMount, type OnMount } from "@monaco-editor/react";
import type * as monaco from "monaco-editor";
import { JimboColorOption } from "../ui/tokens.js";

// Monaco needs hex strings for its colors API. We strip the leading `#` from
// JimboColor tokens where Monaco expects raw hex for syntax rules (token
// foreground), and pass the full `#...` form for UI colors. Alpha suffix
// (e.g. WHITE + "20") is valid for Monaco colors but not for rules.
const hex = (token: string) => token.replace(/^#/, "");

const defineBalatroTheme: BeforeMount = (monaco) => {
  monaco.editor.defineTheme("jaml-balatro-dark", {
    base: "vs-dark",
    inherit: true,
    rules: [
      { token: "comment", foreground: hex(JimboColorOption.GREY), fontStyle: "italic" },
      { token: "keyword", foreground: hex(JimboColorOption.RED) },
      { token: "string", foreground: hex(JimboColorOption.GOLD_TEXT) },
      { token: "number", foreground: hex(JimboColorOption.BLUE) },
      { token: "type", foreground: hex(JimboColorOption.GREEN_TEXT) },
    ],
    colors: {
      "editor.background": JimboColorOption.DARKEST,
      "editor.foreground": JimboColorOption.WHITE,
      "editorLineNumber.foreground": JimboColorOption.GREY,
      "editorLineNumber.activeForeground": JimboColorOption.GOLD_TEXT,
      "editor.selectionBackground": `${JimboColorOption.WHITE}20`,
      "editor.inactiveSelectionBackground": `${JimboColorOption.WHITE}10`,
      "editor.lineHighlightBackground": `${JimboColorOption.BLACK}20`,
      "editorCursor.foreground": JimboColorOption.GOLD_TEXT,
      "editorWidget.background": JimboColorOption.DARK_GREY,
      "editorWidget.border": `${JimboColorOption.WHITE}20`,
      "editorWidget.foreground": JimboColorOption.WHITE,
      "list.activeSelectionBackground": JimboColorOption.GOLD,
      "list.activeSelectionForeground": JimboColorOption.DARKEST,
      "list.hoverBackground": JimboColorOption.PANEL_EDGE,
      "list.hoverForeground": JimboColorOption.WHITE,
      "list.focusBackground": JimboColorOption.GOLD,
      "list.focusForeground": JimboColorOption.DARKEST,
    },
  });
};

export interface JamlCodeEditorProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  minHeight?: number;
}

export function JamlCodeEditor({
  value,
  onChange,
  minHeight = 320,
}: JamlCodeEditorProps) {
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | null>(null);
  const monacoRef = useRef<typeof monaco | null>(null);
  // Suppress our onChange while we're applying a programmatic edit, so the
  // streamed parent value doesn't loop back through onChange and bounce.
  const suppressEmitRef = useRef(false);
  // Capture initial value for the uncontrolled editor mount; subsequent
  // updates flow through the useEffect below.
  const initialValueRef = useRef(value);
  // Track value across renders so we can apply only the streamed delta when
  // the new value is a strict suffix-extension of what's already in the model.
  const lastSyncedValueRef = useRef(value);

  const handleMount: OnMount = (editor, m) => {
    editorRef.current = editor;
    monacoRef.current = m;
  };

  useEffect(() => {
    const editor = editorRef.current;
    const m = monacoRef.current;
    if (!editor || !m) return;
    const model = editor.getModel();
    if (!model) return;

    const current = editor.getValue();
    if (current === value) {
      lastSyncedValueRef.current = value;
      return;
    }

    suppressEmitRef.current = true;
    try {
      // Streaming-friendly path: when the new value just appends to what
      // Monaco already has, push an insert at end-of-document. Monaco
      // re-tokenizes only from the insertion point — no full-doc churn,
      // no syntax-color strobe, no cursor reset.
      if (value.length > current.length && value.startsWith(current)) {
        const suffix = value.slice(current.length);
        const lastLine = model.getLineCount();
        const lastCol = model.getLineMaxColumn(lastLine);
        model.applyEdits([
          {
            range: new m.Range(lastLine, lastCol, lastLine, lastCol),
            text: suffix,
            forceMoveMarkers: false,
          },
        ]);
      } else {
        // Real replacement (paste from outside, parent rewrite, undo, etc.).
        editor.executeEdits("", [
          {
            range: model.getFullModelRange(),
            text: value,
            forceMoveMarkers: true,
          },
        ]);
        editor.pushUndoStop();
      }
    } finally {
      suppressEmitRef.current = false;
      lastSyncedValueRef.current = value;
    }
  }, [value]);

  return (
    <div style={{ width: "100%", minHeight, background: JimboColorOption.DARKEST }}>
      {/* Kill Monaco's iPad/touch on-screen-keyboard widget — useless inside a
          chat WebView where the OS keyboard is already pinned open. */}
      <style>{`
        .monaco-editor .iPadShowKeyboard,
        .monaco-editor [class*="iPadShowKeyboard"] { display: none !important; }
      `}</style>
      <Editor
        height={`${minHeight}px`}
        defaultLanguage="yaml"
        defaultValue={initialValueRef.current}
        theme="jaml-balatro-dark"
        onChange={(next) => {
          if (suppressEmitRef.current) return;
          onChange(next ?? "");
        }}
        onMount={handleMount}
        beforeMount={defineBalatroTheme}
        options={{
          minimap: { enabled: false },
          scrollBeyondLastLine: false,
          fontSize: 13,
          lineHeight: 22,
          fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace",
          lineNumbers: "on",
          lineNumbersMinChars: 2,
          lineDecorationsWidth: 4,
          glyphMargin: false,
          folding: false,
          automaticLayout: true,
          padding: { top: 12, bottom: 12 },
          wordWrap: "on",
          formatOnPaste: true,
          formatOnType: true,
          renderLineHighlight: "line",
          scrollbar: { verticalScrollbarSize: 8, horizontalScrollbarSize: 8 },
          // Mobile/chat-WebView UX: kill the context menu ("Change All Occurrences" etc. covering half
          // the file on long-press) and the accessibility-help keyboard widget that's just clutter when
          // the OS keyboard is already open.
          contextmenu: false,
          accessibilitySupport: "off",
        }}
      />
    </div>
  );
}
