import React from "react"
import { Button, Group } from "@mantine/core"
import { IconPlayerPlay, IconDeviceFloppy } from "@tabler/icons-react"
import { useJamlStore } from "../store/JamlStore"
import * as monaco from "monaco-editor"
import editorWorker from "monaco-editor/esm/vs/editor/editor.worker?worker"
import yamlWorker from "monaco-editor/esm/vs/language/yaml/yaml.worker?worker"

// Configure workers
self.MonacoEnvironment = {
  getWorker(_, label) {
    if (label === "yaml") return new yamlWorker()
    return new editorWorker()
  }
}

export function JamlEditor() {
  const { jamlState, setJamlContent, addFilter } = useJamlStore()
  const editorRef = React.useRef<monaco.editor.IStandaloneCodeEditor | null>(null)
  
  const handleSave = () => {
    if (!jamlState.content.trim()) return
    
    const filter = {
      id: Date.now().toString(),
      name: `Filter ${new Date().toLocaleString()}`,
      content: jamlState.content,
      created: new Date().toISOString()
    }
    
    addFilter(filter)
  }
  
  const handleSearch = () => {
    // TODO: Implement search functionality
    console.log("Starting search with:", jamlState.content)
  }
  
  return (
    <div style={{ height: "100%", display: "flex", flexDirection: "column" }}>
      <div style={{ flex: 1, minHeight: 0, border: "1px solid #333" }}>
        <Editor
          height="100%"
          defaultLanguage="yaml"
          value={jamlState.content}
          onChange={(value) => setJamlContent(value || "")}
          theme="vs-dark"
          options={{
            minimap: { enabled: false },
            fontSize: 14,
            lineNumbers: "on",
            scrollBeyondLastLine: false,
            automaticLayout: true,
            tabSize: 2,
            wordWrap: "on",
            theme: "vs-dark"
          }}
          onMount={(editor) => {
            editorRef.current = editor
          }}
        />
      </div>
      
      <Group mt="sm">
        <Button
          size="sm"
          variant="filled"
          color="green"
          leftSection={<IconPlayerPlay size={16} />}
          onClick={handleSearch}
        >
          Search
        </Button>
        
        <Button
          size="sm"
          variant="outline"
          color="blue"
          leftSection={<IconDeviceFloppy size={16} />}
          onClick={handleSave}
        >
          Save Filter
        </Button>
      </Group>
    </div>
  )
}
