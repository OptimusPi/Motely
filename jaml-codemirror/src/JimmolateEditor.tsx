import { javascript } from "@codemirror/lang-javascript";
import { linter, type Diagnostic } from "@codemirror/lint";
import { oneDark } from "@codemirror/theme-one-dark";
import CodeMirror from "@uiw/react-codemirror";
import { useMemo } from "react";
import { jimmolateLinter } from "./jimmolatePredicate.js";

export interface JimmolateEditorProps {
  value: string;
  onChange: (value: string) => void;
  height?: string;
  className?: string;
  placeholder?: string;
}

export function JimmolateEditor({
  value,
  onChange,
  height = "160px",
  className,
  placeholder,
}: JimmolateEditorProps) {
  const extensions = useMemo(
    () => [
      javascript(),
      linter((view) => jimmolateLinter(view.state.doc.toString()) as Diagnostic[]),
      oneDark,
    ],
    [],
  );

  return (
    <CodeMirror
      value={value}
      height={height}
      className={className}
      placeholder={placeholder}
      extensions={extensions}
      onChange={onChange}
      basicSetup={{
        lineNumbers: true,
        highlightActiveLineGutter: true,
        highlightActiveLine: true,
        foldGutter: false,
      }}
      theme="dark"
    />
  );
}
