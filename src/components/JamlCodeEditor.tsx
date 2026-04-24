"use client";

import React from "react";
import Editor, { type BeforeMount } from "@monaco-editor/react";
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
  return (
    <div style={{ width: "100%", minHeight, background: JimboColorOption.DARKEST }}>
      <Editor
        height={`${minHeight}px`}
        defaultLanguage="yaml"
        value={value}
        theme="jaml-balatro-dark"
        onChange={(next) => onChange(next ?? "")}
        beforeMount={defineBalatroTheme}
        options={{
          minimap: { enabled: false },
          scrollBeyondLastLine: false,
          fontSize: 13,
          lineHeight: 22,
          fontFamily: "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace",
          lineNumbers: "on",
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
