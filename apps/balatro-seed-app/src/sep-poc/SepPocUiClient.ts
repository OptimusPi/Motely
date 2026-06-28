'use client';

import type { Spec } from '@json-render/core';
import { useState, useEffect, useCallback, useRef } from 'react';

/**
 * SepPocUiClient — Simple HTTP client for the SEP POC MCP server.
 *
 * Uses direct fetch() to the Next.js API route. This avoids the MCP SDK
 * protocol mismatch and lets us iterate fast on the ui:// extension.
 */

export type SepPocConnectionState = 'idle' | 'connecting' | 'connected' | 'error';

export interface SepPocTool {
  name: string;
  description?: string;
  inputSchema?: unknown;
}

export interface SepPocUiResource {
  uri: string;
  name: string;
  mimeType: string;
  description?: string;
}

export interface SepPocClientOptions {
  serverUrl: string;
  apiKey?: string;
}

export class SepPocClient {
  private state: SepPocConnectionState = 'idle';
  private tools: SepPocTool[] = [];
  private resources: SepPocUiResource[] = [];
  private listeners: Set<(state: SepPocConnectionState, tools: SepPocTool[], resources: SepPocUiResource[]) => void> = new Set();

  constructor(private options: SepPocClientOptions) {}

  getState() { return this.state; }
  getTools() { return this.tools; }
  getResources() { return this.resources; }

  onChange(cb: (state: SepPocConnectionState, tools: SepPocTool[], resources: SepPocUiResource[]) => void) {
    this.listeners.add(cb);
    return () => this.listeners.delete(cb);
  }

  private notify() {
    this.listeners.forEach((cb) => cb(this.state, this.tools, this.resources));
  }

  async connect() {
    if (this.state === 'connected' || this.state === 'connecting') return;
    this.state = 'connecting';
    this.notify();

    try {
      const res = await fetch(this.options.serverUrl, {
        headers: this.options.apiKey ? { Authorization: `Bearer ${this.options.apiKey}` } : {},
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);

      const data = await res.json();
      this.tools = (data.tools ?? []).map((t: any) => ({
        name: t.name,
        description: t.description,
        inputSchema: t.parameters,
      }));
      this.resources = (data.resources ?? []).map((r: any) => ({
        uri: r.uri,
        name: r.name,
        mimeType: r.mimeType,
        description: r.description,
      }));
      this.state = 'connected';
      this.notify();
    } catch (err) {
      console.error('SEP POC connect failed:', err);
      this.state = 'error';
      this.notify();
    }
  }

  async disconnect() {
    this.state = 'idle';
    this.tools = [];
    this.resources = [];
    this.notify();
  }

  async callTool(name: string, args: Record<string, unknown>) {
    const res = await fetch(this.options.serverUrl, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(this.options.apiKey ? { Authorization: `Bearer ${this.options.apiKey}` } : {}),
      },
      body: JSON.stringify({ name, arguments: args }),
    });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    return res.json();
  }

  /** Read a ui:// resource and return its json-render spec. */
  async readUiResource(uri: string): Promise<Spec> {
    const result = await this.callTool('ui_read', { uri });
    if (result.error) throw new Error(result.error);
    return result.spec as Spec;
  }
}

/**
 * React hook for the SEP POC client.
 */
export function useSepPocClient(options: SepPocClientOptions) {
  const [state, setState] = useState<SepPocConnectionState>('idle');
  const [tools, setTools] = useState<SepPocTool[]>([]);
  const [resources, setResources] = useState<SepPocUiResource[]>([]);
  const clientRef = useRef<SepPocClient | null>(null);

  useEffect(() => {
    const client = new SepPocClient(options);
    clientRef.current = client;

    const unsub = client.onChange((s, t, r) => {
      setState(s);
      setTools(t);
      setResources(r);
    });

    return () => {
      unsub();
      client.disconnect();
      clientRef.current = null;
    };
  }, [options.serverUrl, options.apiKey]);

  const connect = useCallback(() => clientRef.current?.connect(), []);
  const disconnect = useCallback(() => clientRef.current?.disconnect(), []);

  const callTool = useCallback(
    async (name: string, args: Record<string, unknown>) => {
      if (!clientRef.current) throw new Error('SEP POC client not initialized');
      return clientRef.current.callTool(name, args);
    },
    []
  );

  const readUiResource = useCallback(
    async (uri: string) => {
      if (!clientRef.current) throw new Error('SEP POC client not initialized');
      return clientRef.current.readUiResource(uri);
    },
    []
  );

  return { state, tools, resources, connect, disconnect, callTool, readUiResource, client: clientRef.current };
}
