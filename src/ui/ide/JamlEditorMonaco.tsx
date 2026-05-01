"use client";

import React, { useRef } from 'react';
import Editor, { OnMount, loader } from "@monaco-editor/react";
import { Copy, RotateCcw, Check, AlertCircle } from 'lucide-react';
import { cn } from '@/lib/utils';


interface JamlEditorMonacoProps {
    value: string;
    onChange: (value: string | undefined) => void;
    diagnostics?: any;
    className?: string;
}

export default function JamlEditorMonaco({ value, onChange, diagnostics, className }: JamlEditorMonacoProps) {
    const editorRef = useRef<any>(null);

    const handleEditorWillMount = (monaco: any) => {
        // Define Balatro Theme
        monaco.editor.defineTheme('balatro-dark', {
            base: 'vs-dark',
            inherit: true,
            rules: [
                { token: 'comment', foreground: '5f7377', fontStyle: 'italic' },
                { token: 'keyword', foreground: 'fe5f55' }, // Balatro Red
                { token: 'string', foreground: 'eac058' },  // Balatro Gold
                { token: 'number', foreground: '009dff' },  // Balatro Blue
                { token: 'type', foreground: '4bc292' },    // Balatro Green
            ],
            colors: {
                'editor.background': '#1e2b2d', // Authentic G.C.BLACK variant
                'editor.foreground': '#ffffff',
                'editorLineNumber.foreground': '#4f6367',
                'editorLineNumber.activeForeground': '#eac058',
                'editor.selectionBackground': '#ffffff20',
                'editor.inactiveSelectionBackground': '#ffffff10',
                'editor.lineHighlightBackground': '#00000020',
                'editorCursor.foreground': '#eac058',

                // Widget/Popover Colors (Fixes User Issue: "White text on white background")
                'editorWidget.background': '#1e2b2d', // Balatro Dark
                'editorWidget.border': '#ffffff20',
                'editorWidget.foreground': '#ffffff',

                // Suggestion List Colors
                'list.activeSelectionBackground': '#d8b97d', // Balatro Gold Selection
                'list.activeSelectionForeground': '#1e2b2d', // Dark text on gold
                'list.hoverBackground': '#2a3b3d',
                'list.hoverForeground': '#ffffff',
                'list.focusBackground': '#d8b97d',
                'list.focusForeground': '#1e2b2d',
            }
        });
    };

    const handleEditorDidMount: OnMount = (editor, monaco) => {
        editorRef.current = editor;

        // Focus editor
        editor.focus();

        // Optional: Add custom JAML commands/actions here
    };

    const handleFormat = () => {
        if (editorRef.current) {
            editorRef.current.getAction('editor.action.formatDocument').run();
        }
    };

    const handleCopy = () => {
        navigator.clipboard.writeText(value);
    };

    const hasErrors = diagnostics?.errors?.length > 0;

    return (
        <div className={cn("flex flex-col h-full bg-[#1e2b2d] border border-white/10 rounded-xl overflow-hidden shadow-2xl group", className)}>
            {/* Toolbar */}
            <div className="h-10 bg-black/40 border-b border-white/5 flex items-center justify-between px-4 shrink-0 overflow-hidden">
                <div className="flex items-center gap-4">
                    <span className="font-header text-xs text-[var(--balatro-red)] tracking-widest uppercase flex items-center gap-2">
                        <div className="w-2 h-2 rounded-full bg-[var(--balatro-red)] animate-pulse" />
                        JAML EDITOR
                    </span>
                    <div className="h-4 w-px bg-white/10" />
                    <div className="flex items-center gap-3">
                        <button onClick={handleFormat} className="text-[9px] font-pixel text-white/30 hover:text-white/80 uppercase tracking-tighter transition-colors">Format</button>
                        <button onClick={handleCopy} className="text-[9px] font-pixel text-white/30 hover:text-white/80 uppercase tracking-tighter transition-colors flex items-center gap-1">
                            <Copy size={10} /> Copy
                        </button>
                    </div>
                </div>

                <div className="flex items-center gap-2">
                    {hasErrors ? (
                        <div className="flex items-center gap-1.5 px-2 py-0.5 bg-red-500/10 border border-red-500/20 rounded text-[9px] font-pixel text-red-400">
                            <AlertCircle size={10} />
                            <span>{diagnostics.errors.length} SYNTAX ERROR(S)</span>
                        </div>
                    ) : (
                        <div className="flex items-center gap-1.5 px-2 py-0.5 bg-green-500/10 border border-green-500/20 rounded text-[9px] font-pixel text-green-400 opacity-60">
                            <Check size={10} />
                            <span>SYNTAX VALID</span>
                        </div>
                    )}
                </div>
            </div>

            {/* Editor Area */}
            <div className="flex-1 min-h-0 relative">
                <Editor
                    height="100%"
                    defaultLanguage="yaml"
                    value={value}
                    theme="balatro-dark"
                    onChange={onChange}
                    onMount={handleEditorDidMount}
                    beforeMount={handleEditorWillMount}
                    options={{
                        minimap: { enabled: false },
                        scrollBeyondLastLine: false,
                        fontSize: 14,
                        fontFamily: 'var(--font-mono)',
                        lineNumbers: 'on',
                        roundedSelection: true,
                        readOnly: false,
                        cursorStyle: 'line',
                        automaticLayout: true,
                        padding: { top: 16, bottom: 16 },
                        wordWrap: 'on',
                        formatOnPaste: true,
                        formatOnType: true,
                        suggest: {
                            showKeywords: true,
                            showSnippets: true,
                        }
                    }}
                />
            </div>

            {/* Quick Status Footer */}
            <div className="h-6 bg-black/20 border-t border-white/5 flex items-center px-4 justify-between shrink-0">
                <div className="text-[8px] font-pixel text-white/20 uppercase">
                    Lines: {value.split('\n').length} | Encoding: UTF-8
                </div>
                <div className="text-[8px] font-pixel text-white/20 uppercase tracking-widest">
                    Motely JAML Engine v1.0.4
                </div>
            </div>
        </div>
    );
}
