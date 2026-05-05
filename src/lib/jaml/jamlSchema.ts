/**
 * JAML Schema Bridge
 *
 * Derives all JAML schema constants from motely-wasm/jaml.schema.json.
 * All JAML-aware files import from here — single swap point if the API changes.
 */

import jamlSchemaJson from '../../../jaml.schema.json';

type SchemaNode = { enum?: string[]; items?: { enum?: string[] }; properties?: Record<string, SchemaNode> };

interface JamlSchema {
    version?: string;
    definitions?: {
        clause?: SchemaNode;
    };
    properties?: Record<string, SchemaNode>;
}

const schema = jamlSchemaJson as unknown as JamlSchema;
const clauseDef = schema.definitions?.clause?.properties ?? {};

export const JAML_SCHEMA_VERSION: string = schema.version ?? 'unknown';

/** The raw JSON schema object. */
export const jamlSchema = schema;

// ── Key groups ─────────────────────────────────────────────────────────────

/** Root-level metadata keys (name, author, deck, etc.) */
export const METADATA_KEYS: readonly string[] = [
  'id', 'name', 'description', 'author', 'dateCreated',
  'deck', 'stake', 'seeds', 'hashtags', 'aesthetics', 'defaults',
];

/** Section keys that contain clause arrays. */
export const SECTION_KEYS: readonly string[] = ['must', 'should', 'mustNot'];

/** Clause type keys — the primary discriminators (joker, voucher, tarot, etc.). */
export const CLAUSE_TYPE_KEYS: readonly string[] = (
  clauseDef.type?.enum ?? []
) as string[];

/** Property keys available inside a clause. */
export const PROPERTY_KEYS: readonly string[] = Object.keys(clauseDef);

/** Source-configuration keys (shopItems, boosterPacks, etc.). */
export const SOURCE_KEYS: readonly string[] = (() => {
  const srcProps = clauseDef.sources?.properties;
  return srcProps ? Object.keys(srcProps) : ['shopItems', 'boosterPacks'];
})();

// ── Value lookups ──────────────────────────────────────────────────────────

/**
 * Get the valid enum values for a given JAML key.
 * Checks top-level properties first, then falls back to clause properties.
 */
export function getValidValuesForKey(key: string): readonly string[] | null {
  // Top-level enum (deck, stake)
  const topProp = schema.properties?.[key];
  if (topProp?.enum) return topProp.enum;
  if (topProp?.items?.enum) return topProp.items.enum;

  // Clause-level enum
  const clauseProp = clauseDef[key];
  if (clauseProp?.enum) return clauseProp.enum;
  if (clauseProp?.items?.enum) return clauseProp.items.enum;

  return null;
}

/**
 * Get the available properties for a given clause type.
 * Returns all clause property keys (the type system doesn't restrict per-type in JSON schema v7).
 */
export function getAvailablePropsForType(/* _clauseType */): readonly string[] {
  return PROPERTY_KEYS;
}

/**
 * Check if a property is invalid for a clause type.
 * In the v7 schema, all properties are available on all clause types.
 */
export function isInvalidPropForType(/* _prop, _clauseType */): boolean {
  return false;
}

/**
 * Check if a value is invalid for a property.
 */
export function isInvalidValueForProp(value: string, prop: string): boolean {
  const valid = getValidValuesForKey(prop);
  if (!valid) return false; // No enum constraint — anything goes
  return !valid.includes(value);
}

export interface ValidationState {
  errors: string[];
  warnings: string[];
}

// ── Convenience lookups ────────────────────────────────────────────────────

export const DECK_VALUES = (getValidValuesForKey('deck') ?? []) as readonly string[];
export const STAKE_VALUES = (getValidValuesForKey('stake') ?? []) as readonly string[];
export const EDITION_VALUES = (getValidValuesForKey('edition') ?? []) as readonly string[];
export const SEAL_VALUES = (getValidValuesForKey('seal') ?? []) as readonly string[];
export const ENHANCEMENT_VALUES = (getValidValuesForKey('enhancement') ?? []) as readonly string[];
export const RANK_VALUES = (getValidValuesForKey('rank') ?? []) as readonly string[];
export const SUIT_VALUES = (getValidValuesForKey('suit') ?? []) as readonly string[];
export const STICKER_VALUES = (getValidValuesForKey('stickers') ?? []) as readonly string[];

/**
 * All JAML keywords — sections, metadata keys, clause types, and property keys.
 */
export const ALL_JAML_KEYWORDS: string[] = [
  ...SECTION_KEYS,
  ...METADATA_KEYS,
  ...CLAUSE_TYPE_KEYS,
  ...PROPERTY_KEYS,
  ...SOURCE_KEYS,
];
