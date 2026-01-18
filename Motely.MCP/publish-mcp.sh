#!/bin/bash

RUNTIME="${1:-linux-x64}"
CONFIGURATION="${2:-Release}"
OUTPUT_DIR="${3:-publish}"

PROJECT_PATH="$(dirname "$0")/Motely.MCP.csproj"
PUBLISH_PATH="$(dirname "$0")/$OUTPUT_DIR"

if [ -d "$PUBLISH_PATH" ]; then
    rm -rf "$PUBLISH_PATH"
fi

dotnet publish "$PROJECT_PATH" \
    --configuration "$CONFIGURATION" \
    --runtime "$RUNTIME" \
    --self-contained true \
    --output "$PUBLISH_PATH" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true

if [ $? -ne 0 ]; then
    exit 1
fi

EXE_NAME="Motely.MCP"
if [[ "$RUNTIME" == win-* ]]; then
    EXE_NAME="Motely.MCP.exe"
fi

EXE_PATH="$PUBLISH_PATH/$EXE_NAME"

if [ -f "$EXE_PATH" ]; then
    echo "Executable: $EXE_PATH"
    echo ""
    echo "Cursor MCP config:"
    cat << EOF
{
  "mcpServers": {
    "balatro-seed-oracle": {
      "command": "$EXE_PATH",
      "args": ["--mcp-stdio"],
      "env": {
        "MCP_MODE": "stdio"
      }
    }
  }
}
EOF
fi
