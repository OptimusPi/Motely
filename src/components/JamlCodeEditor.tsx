"use client";

import React from "react";
import Editor, { type BeforeMount } from "@monaco-editor/react";
import { JimboColorOption } from "../ui/tokens.js";

const defineBalatroTheme: BeforeMount = (monaco) => {
  monaco.editor.defineTheme("jaml-balatro-dark", {
    base: "vs-dark",
    inherit: true,
    rules: [
      { token: "comment", foreground: "5f7377", fontStyle: "italic" },
      { token: "keyword", foreground: "fe5f55" },
      { token: "string", foreground: "eac058" },
      { token: "number", foreground: "009dff" },
      { token: "type", foreground: "4bc292" },
    ],
    colors: {
      "editor.background": JimboColorOption.DARKEST,
      "editor.foreground": JimboColorOption.WHITE,
      "editorLineNumber.foreground": "#4f6367",
      "editorLineNumber.activeForeground": "#eac058",
      "editor.selectionBackground": "#ffffff20",
      "editor.inactiveSelectionBackground": "#ffffff10",
      "editor.lineHighlightBackground": "#00000020",
      "editorCursor.foreground": "#eac058",
      "editorWidget.background": JimboColorOption.DARK_GREY,
      "editorWidget.border": "#ffffff20",
      "editorWidget.foreground": JimboColorOption.WHITE,
      "list.activeSelectionBackground": "#d8b97d",
      "list.activeSelectionForeground": JimboColorOption.DARKEST,
      "list.hoverBackground": "#2a3b3d",
      "list.hoverForeground": JimboColorOption.WHITE,
      "list.focusBackground": "#d8b97d",
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
        }}
      />
    </div>
  );
}
