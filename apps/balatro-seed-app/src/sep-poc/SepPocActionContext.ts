'use client';

import { createContext, useContext } from 'react';

/**
 * SepPocActionContext — Bypasses json-render's ActionProvider for reliable
 * action routing. Registry components use this context to call back into the
 * app without guessing the json-render action wire protocol.
 */

export type SepPocActionHandler = (action: string, params?: Record<string, unknown>) => void;

export const SepPocActionContext = createContext<SepPocActionHandler>(() => {
  console.warn('[SepPoc] ActionContext not provided — action dropped');
});

export function useSepPocAction() {
  return useContext(SepPocActionContext);
}
