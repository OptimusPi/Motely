import { defineCatalog } from '@json-render/core';
import { schema } from '@json-render/react/schema';
import { z } from 'zod';

/**
 * SepPocCatalog — json-render catalog for the July 2026 SEP POC.
 *
 * Constrained to Jimbo design-system components. No raw HTML, no flex.
 * Every component maps to a real jaml-ui/ui primitive.
 *
 * NOTE: Actions are NOT declared in the catalog. They are routed via
 * SepPocActionContext instead of json-render's ActionProvider wire protocol.
 * This avoids the opaque handler-map API and gives direct callback access.
 */

export const sepPocCatalog = defineCatalog(schema, {
  components: {
    // ── Layout (Jimbo primitives) ──
    JimboPanel: {
      props: z.object({
        title: z.string().optional(),
        sway: z.boolean().optional(),
      }),
      description: 'Framed panel with optional title.',
    },
    JimboInnerPanel: {
      props: z.object({}),
      description: 'Inset sub-panel with silver border.',
    },
    JimboStack: {
      props: z.object({
        gap: z.enum(['xs', 'sm', 'md', 'lg', 'xl']).optional(),
        align: z.enum(['start', 'center', 'end', 'stretch']).optional(),
      }),
      description: 'Vertical stack — CSS grid column-flow.',
    },
    JimboRow: {
      props: z.object({
        gap: z.enum(['xs', 'sm', 'md', 'lg', 'xl']).optional(),
        align: z.enum(['start', 'center', 'end', 'stretch']).optional(),
        justify: z.enum(['start', 'center', 'end', 'between']).optional(),
      }),
      description: 'Horizontal row — CSS grid row-flow.',
    },

    // ── Typography ──
    JimboText: {
      props: z.object({
        text: z.string(),
        tone: z.enum(['default', 'mult', 'chips', 'gold', 'green', 'red', 'blue', 'orange', 'purple', 'grey', 'white']).optional(),
        size: z.enum(['micro', 'label', 'xs', 'body', 'sm', 'md', 'heading', 'lg', 'xl', 'display']).optional(),
      }),
      description: 'Canonical pixel-font text with Balatro drop shadow.',
    },

    // ── Controls ──
    JimboButton: {
      props: z.object({
        label: z.string(),
        tone: z.enum(['orange', 'red', 'blue', 'green', 'tarot', 'planet', 'spectral', 'grey']).optional(),
        size: z.enum(['xs', 'sm', 'md', 'lg']).optional(),
        fullWidth: z.boolean().optional(),
      }),
      description: 'Canonical flat 2D Balatro-style button.',
    },
    JimboSpinner: {
      props: z.object({
        value: z.string(),
        label: z.string().optional(),
      }),
      description: '< value > two-arrow cycler.',
    },
    JimboBadge: {
      props: z.object({
        label: z.string(),
        tone: z.enum(['dark', 'blue', 'red', 'green', 'grey', 'orange', 'purple']).optional(),
      }),
      description: 'Small colored label pill.',
    },
    JimboValueBadge: {
      props: z.object({
        value: z.number(),
        min: z.number().optional(),
        max: z.number().optional(),
      }),
      description: 'Red pill that displays a number; click to edit.',
    },
    JimboSlider: {
      props: z.object({
        value: z.number(),
        min: z.number().default(0),
        max: z.number().default(100),
        step: z.number().default(1),
      }),
      description: 'Dark trough + red fill + ValueBadge thumb.',
    },

    // ── Domain ──
    ConnectionStatus: {
      props: z.object({
        status: z.enum(['idle', 'connecting', 'connected', 'error']),
        toolCount: z.number().optional(),
        resourceCount: z.number().optional(),
      }),
      description: 'Connection status bar with colored dot and metadata.',
    },
    ToolCard: {
      props: z.object({
        name: z.string(),
        description: z.string().optional(),
        executing: z.boolean().optional(),
      }),
      description: 'Tool card with name, description, and Run button.',
    },
    ResultPanel: {
      props: z.object({
        title: z.string(),
        body: z.string(),
        variant: z.enum(['default', 'error', 'success']).optional(),
      }),
      description: 'Result panel with title and body text.',
    },
    SeedCard: {
      props: z.object({
        seed: z.string(),
        score: z.number().optional(),
        rank: z.number().optional(),
      }),
      description: 'Compact seed card with copy and analyze actions.',
    },
  },
  actions: {},
});

export type SepPocCatalog = typeof sepPocCatalog;
