'use client';

import React, { useState, useCallback, useEffect } from 'react';
import {
  JimboApp,
  JimboAppScroll,
  JimboAppFooter,
  JimboPanel,
  JimboStack,
  JimboRow,
  JimboText,
  JimboButton,
} from 'jaml-ui/ui';
import {
  Renderer,
  StateProvider,
  ActionProvider,
  VisibilityProvider,
  ValidationProvider,
} from '@json-render/react';
import { registry } from './SepPocUiRegistry';
import { useSepPocClient } from './SepPocUiClient';
import { SepPocActionContext } from './SepPocActionContext';
import {
  buildConnectionSpec,
  buildToolListSpec,
  buildResultsSpec,
} from './SepPocSpecBuilder';
import type { Spec } from '@json-render/core';

/**
 * SepPocApp — July 2026 SEP POC main shell.
 *
 * Fixed 320×568. Jimbo primitives only. No flex, no inline styles, no raw tags.
 * The ui:// extension is demonstrated via server-rendered json-render specs.
 */

export function SepPocApp() {
  const { state, tools, resources, connect, disconnect, callTool, readUiResource } = useSepPocClient({
    serverUrl: '/api/sep-mcp',
  });

  const [results, setResults] = useState<Array<{ id: string; tool: string; success: boolean; body: string }>>([]);
  const [executing, setExecuting] = useState<string | null>(null);
  const [connectionSpec, setConnectionSpec] = useState<Spec>(buildConnectionSpec('idle'));
  const [toolListSpec, setToolListSpec] = useState<Spec>(buildToolListSpec([]));
  const [resultsSpec, setResultsSpec] = useState<Spec>(buildResultsSpec([]));
  const [activeTab, setActiveTab] = useState<'tools' | 'results'>('tools');
  const [uiDemoSpec, setUiDemoSpec] = useState<Spec | null>(null);

  // Refresh specs when state changes
  useEffect(() => {
    setConnectionSpec(buildConnectionSpec(state, tools.length, resources.length));
    setToolListSpec(buildToolListSpec(tools));
  }, [state, tools, resources]);

  useEffect(() => {
    setResultsSpec(buildResultsSpec(results));
  }, [results]);

  // Demo: read a ui:// resource when connected
  useEffect(() => {
    if (state !== 'connected') {
      setUiDemoSpec(null);
      return;
    }
    let cancelled = false;
    readUiResource('ui://sep-poc/tool-list')
      .then((spec) => {
        if (!cancelled) setUiDemoSpec(spec);
      })
      .catch(() => {
        // ui:// read is optional demo; fail silently
      });
    return () => { cancelled = true; };
  }, [state, readUiResource]);

  const executeToolByName = useCallback(async (toolName: string, toolArgs: Record<string, unknown> = {}) => {
    setExecuting(toolName);
    try {
      const result = await callTool(toolName, toolArgs);
      setResults((prev) => [
        ...prev,
        {
          id: `${toolName}-${Date.now()}`,
          tool: toolName,
          success: !result.error,
          body: result.error ? String(result.error) : JSON.stringify(result, null, 2),
        },
      ]);
      setActiveTab('results');
    } catch (err) {
      setResults((prev) => [
        ...prev,
        {
          id: `${toolName}-${Date.now()}`,
          tool: toolName,
          success: false,
          body: (err as Error).message,
        },
      ]);
    } finally {
      setExecuting(null);
    }
  }, [callTool]);

  const handleAction = useCallback((action: string, params?: Record<string, unknown>) => {
    switch (action) {
      case 'connect': {
        connect();
        break;
      }
      case 'disconnect': {
        disconnect();
        break;
      }
      case 'executeTool': {
        const toolName = params?.name as string;
        if (toolName) executeToolByName(toolName, params ?? {});
        break;
      }
      case 'clearResults': {
        setResults([]);
        break;
      }
      case 'copySeed': {
        const seed = params?.seed as string;
        if (seed) navigator.clipboard.writeText(seed);
        break;
      }
      case 'analyzeSeed': {
        const seed = params?.seed as string;
        if (seed) executeToolByName('analyze_seed', { seed });
        break;
      }
    }
  }, [connect, disconnect, executeToolByName]);

  return (
    <JimboApp>
      <JimboPanel title="July 2026 SEP POC">
        <JimboStack gap="md" align="stretch">
          <SepPocActionContext.Provider value={handleAction}>
            <StateProvider initialState={{}}>
              <VisibilityProvider>
                <ActionProvider handlers={{}}>
                  <ValidationProvider>
                    <Renderer spec={connectionSpec} registry={registry} />
                  </ValidationProvider>
                </ActionProvider>
              </VisibilityProvider>
            </StateProvider>
          </SepPocActionContext.Provider>

          <JimboRow gap="sm" align="center" justify="between">
            <JimboButton
              tone={activeTab === 'tools' ? 'blue' : 'grey'}
              size="sm"
              onClick={() => setActiveTab('tools')}
            >
              Tools
            </JimboButton>
            <JimboButton
              tone={activeTab === 'results' ? 'blue' : 'grey'}
              size="sm"
              onClick={() => setActiveTab('results')}
            >
              Results ({results.length})
            </JimboButton>
          </JimboRow>
        </JimboStack>
      </JimboPanel>

      <JimboAppScroll>
        <JimboStack gap="md" align="stretch">
          <SepPocActionContext.Provider value={handleAction}>
            <StateProvider initialState={{}}>
              <VisibilityProvider>
                <ActionProvider handlers={{}}>
                  <ValidationProvider>
                    {activeTab === 'tools' && state === 'connected' && (
                      <Renderer spec={toolListSpec} registry={registry} />
                    )}
                    {activeTab === 'tools' && state !== 'connected' && (
                      <JimboText size="sm" tone="grey">
                        Connect to see available tools.
                      </JimboText>
                    )}
                    {activeTab === 'results' && (
                      <Renderer spec={resultsSpec} registry={registry} />
                    )}
                  </ValidationProvider>
                </ActionProvider>
              </VisibilityProvider>
            </StateProvider>
          </SepPocActionContext.Provider>

          {/* ui:// demo: render the server-provided spec */}
          {uiDemoSpec && activeTab === 'tools' && (
            <SepPocActionContext.Provider value={handleAction}>
              <StateProvider initialState={{}}>
                <VisibilityProvider>
                  <ActionProvider handlers={{}}>
                    <ValidationProvider>
                      <JimboPanel title="ui://sep-poc/tool-list">
                        <Renderer spec={uiDemoSpec} registry={registry} />
                      </JimboPanel>
                    </ValidationProvider>
                  </ActionProvider>
                </VisibilityProvider>
              </StateProvider>
            </SepPocActionContext.Provider>
          )}
        </JimboStack>
      </JimboAppScroll>

      <JimboAppFooter>
        <JimboRow gap="sm" align="center" justify="between">
          <JimboText size="xs" tone="grey">
            SEP POC v0.1.0
          </JimboText>
          {results.length > 0 && (
            <JimboButton tone="grey" size="xs" onClick={() => handleAction('clearResults')}>
              Clear
            </JimboButton>
          )}
        </JimboRow>
      </JimboAppFooter>
    </JimboApp>
  );
}
