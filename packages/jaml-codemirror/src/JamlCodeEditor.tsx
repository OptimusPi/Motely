import { autocompletion } from "@codemirror/autocomplete";
import { yaml } from "@codemirror/lang-yaml";
import { linter, type Diagnostic } from "@codemirror/lint";
import { oneDark } from "@codemirror/theme-one-dark";
import CodeMirror from "@uiw/react-codemirror";
import { useMemo } from "react";
import { jamlCompletions } from "./jamlCompletions.js";
import { jamlLinter } from "./jamlLinter.js";

export interface JamlCodeEditorProps {
  value: string;
  onChange: (value: string) => void;
  height?: string;
  className?: string;
  placeholder?: string;
}

export function JamlCodeEditor({
  value,
  onChange,
  height = "320px",
  className,
  placeholder,
}: JamlCodeEditorProps) {
  const extensions = useMemo(
    () => [
      yaml(),
      linter((view) => jamlLinter(view.state.doc.toString()) as Promise<Diagnostic[]>),
      autocompletion({ override: [jamlCompletions] }),
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
        foldGutter: true,
      }}
      theme="dark"
    />
  );
}
