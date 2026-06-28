import { defineRegistry } from '@json-render/react';
import { sepPocCatalog } from './SepPocCatalog';
import { useSepPocAction } from './SepPocActionContext';
import {
  JimboPanel as JbPanel,
  JimboInnerPanel as JbInnerPanel,
  JimboStack as JbStack,
  JimboRow as JbRow,
  JimboText as JbText,
  JimboButton as JbButton,
  JimboSpinner as JbSpinner,
  JimboBadge as JbBadge,
  JimboValueBadge as JbValueBadge,
  JimboSlider as JbSlider,
} from 'jaml-ui/ui';
import React from 'react';

// ── ConnectionDot ──
// Local primitive — not in jaml-ui yet, but follows the same rules.
function JimboConnectionDot({ status }: { status: string }) {
  const toneMap: Record<string, string> = {
    idle: 'grey',
    connecting: 'orange',
    connected: 'green',
    error: 'red',
  };
  return (
    <span className={`j-connection-dot j-connection-dot--${toneMap[status] ?? 'grey'}`} />
  );
}

// ── SepPoc Component Registry ──
// Every catalog entry maps to a real Jimbo primitive. No raw <div>, no flex, no inline styles.
// Actions are routed via SepPocActionContext instead of json-render's ActionProvider.

const components = {
  JimboPanel: ({ props, children }: any) => (
    <JbPanel title={props.title} sway={props.sway}>
      {children}
    </JbPanel>
  ),

  JimboInnerPanel: ({ props, children }: any) => (
    <JbInnerPanel {...props}>{children}</JbInnerPanel>
  ),

  JimboStack: ({ props, children }: any) => (
    <JbStack gap={props.gap} align={props.align}>
      {children}
    </JbStack>
  ),

  JimboRow: ({ props, children }: any) => (
    <JbRow gap={props.gap} align={props.align} justify={props.justify}>
      {children}
    </JbRow>
  ),

  JimboText: ({ props }: any) => (
    <JbText tone={props.tone} size={props.size}>
      {props.text}
    </JbText>
  ),

  JimboButton: ({ props }: any) => {
    const onAction = useSepPocAction();
    return (
      <JbButton
        tone={props.tone ?? 'orange'}
        size={props.size ?? 'md'}
        fullWidth={props.fullWidth}
        onClick={() => onAction('click')}
      >
        {props.label}
      </JbButton>
    );
  },

  JimboSpinner: ({ props, emit }: any) => (
    <JbSpinner
      value={props.value}
      label={props.label}
      onPrev={() => emit?.('prev')}
      onNext={() => emit?.('next')}
    />
  ),

  JimboBadge: ({ props }: any) => (
    <JbBadge tone={props.tone ?? 'dark'}>{props.label}</JbBadge>
  ),

  JimboValueBadge: ({ props, emit }: any) => (
    <JbValueBadge
      value={props.value}
      min={props.min}
      max={props.max}
      onChange={(v: number) => emit?.('change', { value: v })}
    />
  ),

  JimboSlider: ({ props, emit }: any) => (
    <JbSlider
      value={props.value}
      min={props.min}
      max={props.max}
      step={props.step}
      onChange={(v: number) => emit?.('change', { value: v })}
    />
  ),

  // ── Domain Components ──

  ConnectionStatus: ({ props }: any) => (
    <JbRow gap="sm" align="center" justify="between">
      <JbRow gap="xs" align="center">
        <JimboConnectionDot status={props.status} />
        <JbText
          size="sm"
          tone={props.status === 'connected' ? 'green' : props.status === 'error' ? 'red' : 'grey'}
        >
          {props.status}
        </JbText>
      </JbRow>
      <JbRow gap="xs" align="center">
        {props.toolCount !== undefined && (
          <JbBadge tone="blue">{props.toolCount} tools</JbBadge>
        )}
        {props.resourceCount !== undefined && (
          <JbBadge tone="purple">{props.resourceCount} ui</JbBadge>
        )}
      </JbRow>
    </JbRow>
  ),

  ToolCard: ({ props }: any) => {
    const onAction = useSepPocAction();
    return (
      <JbInnerPanel>
        <JbRow gap="sm" align="center" justify="between">
          <JbStack gap="xs" align="start">
            <JbText size="sm" tone="white">
              {props.name}
            </JbText>
            {props.description && (
              <JbText size="xs" tone="grey">
                {props.description}
              </JbText>
            )}
          </JbStack>
          <JbButton
            tone="red"
            size="sm"
            onClick={() => onAction('executeTool', { name: props.name })}
          >
            {props.executing ? '…' : 'Run'}
          </JbButton>
        </JbRow>
      </JbInnerPanel>
    );
  },

  ResultPanel: ({ props }: any) => (
    <JbInnerPanel>
      <JbStack gap="xs" align="start">
        <JbText size="sm" tone="white">
          {props.title}
        </JbText>
        <JbText
          size="xs"
          tone={props.variant === 'error' ? 'red' : props.variant === 'success' ? 'green' : 'grey'}
        >
          {props.body}
        </JbText>
      </JbStack>
    </JbInnerPanel>
  ),

  SeedCard: ({ props }: any) => {
    const onAction = useSepPocAction();
    return (
      <JbInnerPanel>
        <JbRow gap="sm" align="center" justify="between">
          <JbStack gap="xs" align="start">
            <JbText size="sm" tone="blue">
              {props.seed}
            </JbText>
            {props.score !== undefined && (
              <JbText size="xs" tone="grey">
                Score: {props.score}
              </JbText>
            )}
          </JbStack>
          <JbRow gap="xs" align="center">
            <JbButton
              tone="red"
              size="xs"
              onClick={() => onAction('copySeed', { seed: props.seed })}
            >
              Copy
            </JbButton>
            <JbButton
              tone="orange"
              size="xs"
              onClick={() => onAction('analyzeSeed', { seed: props.seed })}
            >
              Analyze
            </JbButton>
          </JbRow>
        </JbRow>
      </JbInnerPanel>
    );
  },
};

// ── Build Registry ──
export const { registry } = defineRegistry(sepPocCatalog, { components, actions: {} });
export { sepPocCatalog };
