import type { Spec } from '@json-render/core';

/**
 * SepPocSpecBuilder — Build json-render specs for ui:// resources.
 *
 * Pure functions. No side effects. Returns SpecType trees that the
 * Renderer + SepPocUiRegistry turn into real Jimbo components.
 */

export function buildConnectionSpec(
  status: 'idle' | 'connecting' | 'connected' | 'error',
  toolCount?: number,
  resourceCount?: number
): Spec {
  const isConnected = status === 'connected';
  return {
    root: 'root',
    elements: {
      root: {
        type: 'JimboStack',
        props: { gap: 'md', align: 'stretch' },
        children: ['status', 'actions'],
      },
      status: {
        type: 'ConnectionStatus',
        props: { status, toolCount, resourceCount },
        children: [],
      },
      actions: {
        type: 'JimboStack',
        props: { gap: 'sm', align: 'stretch' },
        children: isConnected ? ['disconnect-btn'] : ['connect-btn'],
      },
      'connect-btn': {
        type: 'JimboButton',
        props: { label: 'Connect', tone: 'blue', fullWidth: true, action: 'connect' },
        children: [],
      },
      'disconnect-btn': {
        type: 'JimboButton',
        props: { label: 'Disconnect', tone: 'grey', fullWidth: true, action: 'disconnect' },
        children: [],
      },
    },
  };
}

export function buildToolListSpec(tools: Array<{ name: string; description?: string }>): Spec {
  const toolChildren: string[] = tools.map((t, i) => `tool-${i}`);
  const elements: Record<string, any> = {};

  tools.forEach((t, i) => {
    elements[`tool-${i}`] = {
      type: 'ToolCard',
      props: { name: t.name, description: t.description },
      children: [],
    };
  });

  return {
    root: 'root',
    elements: {
      root: {
        type: 'JimboStack',
        props: { gap: 'sm', align: 'stretch' },
        children: toolChildren.length > 0 ? toolChildren : ['empty'],
      },
      ...elements,
      empty: {
        type: 'JimboText',
        props: { text: 'No tools available. Connect to the MCP server.', tone: 'grey', size: 'sm' },
        children: [],
      },
    },
  };
}

export function buildResultsSpec(
  results: Array<{ id: string; tool: string; success: boolean; body: string }>
): Spec {
  const resultChildren: string[] = results.map((r) => `res-${r.id}`);
  const elements: Record<string, any> = {};

  results.forEach((r) => {
    elements[`res-${r.id}`] = {
      type: 'ResultPanel',
      props: {
        title: r.tool,
        body: r.body,
        variant: r.success ? 'success' : 'error',
      },
      children: [],
    };
  });

  return {
    root: 'root',
    elements: {
      root: {
        type: 'JimboStack',
        props: { gap: 'sm', align: 'stretch' },
        children: resultChildren.length > 0 ? resultChildren : ['empty'],
      },
      ...elements,
      empty: {
        type: 'JimboText',
        props: { text: 'No results yet. Run a tool to see output here.', tone: 'grey', size: 'sm' },
        children: [],
      },
    },
  };
}

export function buildSeedResultsSpec(
  seeds: Array<{ seed: string; score?: number }>
): Spec {
  const seedChildren: string[] = seeds.map((s, i) => `seed-${i}`);
  const elements: Record<string, any> = {};

  seeds.forEach((s, i) => {
    elements[`seed-${i}`] = {
      type: 'SeedCard',
      props: { seed: s.seed, score: s.score, rank: i + 1 },
      children: [],
    };
  });

  return {
    root: 'root',
    elements: {
      root: {
        type: 'JimboStack',
        props: { gap: 'sm', align: 'stretch' },
        children: seedChildren.length > 0 ? seedChildren : ['empty'],
      },
      ...elements,
      empty: {
        type: 'JimboText',
        props: { text: 'No seeds found.', tone: 'grey', size: 'sm' },
        children: [],
      },
    },
  };
}

export function buildLoadingSpec(message: string): Spec {
  return {
    root: 'root',
    elements: {
      root: {
        type: 'JimboPanel',
        props: { title: message, sway: false },
        children: [],
      },
    },
  };
}
