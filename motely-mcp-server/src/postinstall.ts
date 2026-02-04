/**
 * Post-install script to check for .NET runtime and optionally download binaries
 */

import { existsSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

function checkDotnetRuntime(): boolean {
  try {
    const output = execSync('dotnet --version', { encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] });
    console.log(`[motely-mcp] .NET runtime found: ${output.trim()}`);
    return true;
  } catch {
    return false;
  }
}

function checkPrebuiltBinary(): boolean {
  const binPaths = [
    join(__dirname, '..', 'bin', 'Motely.MCP.exe'),
    join(__dirname, '..', 'bin', 'Motely.MCP'),
  ];

  for (const p of binPaths) {
    if (existsSync(p)) {
      console.log(`[motely-mcp] Pre-built binary found: ${p}`);
      return true;
    }
  }

  return false;
}

async function main() {
  console.log('[motely-mcp] Post-install check...');

  if (checkPrebuiltBinary()) {
    console.log('[motely-mcp] ✅ Ready to use with pre-built binary');
    return;
  }

  if (checkDotnetRuntime()) {
    console.log('[motely-mcp] ✅ .NET runtime available - will run from source');
    console.log('[motely-mcp] To build the MCP server, run:');
    console.log('  cd Motely.MCP && dotnet build -c Release');
    return;
  }

  console.log('[motely-mcp] ⚠️  No .NET runtime found and no pre-built binary available');
  console.log('[motely-mcp] The MCP server will run in limited JavaScript mode.');
  console.log('[motely-mcp] For full functionality, either:');
  console.log('  1. Install .NET 10.0+ runtime: https://dotnet.microsoft.com/download');
  console.log('  2. Or download pre-built binaries from the releases page');
}

main().catch(console.error);
