import type React from "react";
import { type ReactNode, Fragment } from "react";

/**
 * json-render — Zero-dependency JSON-to-React engine.
 *
 * A JsonNode is a plain JSON object describing a UI tree.
 * A Registry maps type names to React components.
 * render() walks the tree and returns React nodes.
 */

export interface JsonNode {
  type: string;
  props?: Record<string, unknown>;
  children?: JsonNode[];
}

export type ComponentProps<P = Record<string, unknown>> = P & {
  children?: ReactNode;
};

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type Registry = Record<string, React.ComponentType<any>>;

/**
 * Render a JsonNode tree using the provided Registry.
 *
 * Unknown types log a warning and render null (graceful degradation).
 * Each child gets a stable key from its index in the children array.
 */
export function render(node: JsonNode, registry: Registry): ReactNode {
  const Component = registry[node.type];

  if (!Component) {
    console.warn(`[json-render] Unknown type: "${node.type}"`);
    return null;
  }

  const children = node.children?.map((child, i) => (
    <Fragment key={i}>{render(child, registry)}</Fragment>
  ));

  return <Component {...(node.props ?? {})}>{children}</Component>;
}

/**
 * Render a list of JsonNode trees.
 */
export function renderList(
  nodes: JsonNode[],
  registry: Registry
): ReactNode[] {
  return nodes.map((node, i) => (
    <Fragment key={i}>{render(node, registry)}</Fragment>
  ));
}

/**
 * Type-safe catalog builder.
 *
 * Just returns the catalog object as-is, but enforces types at compile time.
 * No runtime overhead. No zod. Just TypeScript.
 */
export function defineCatalog<C extends Record<string, { props?: object; description?: string }>>(
  catalog: C
): C {
  return catalog;
}
