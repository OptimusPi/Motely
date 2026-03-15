// Motely Node.js AOT Addon - platform detection + JS wrapper (camelCase API)
import { existsSync } from 'node:fs';
import { platform } from 'node:os';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const require = createRequire(import.meta.url);
const __dirname = dirname(fileURLToPath(import.meta.url));
const SEED_DIGITS = new Set('123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'.split(''));

function getRid() {
  const pl = platform();
  const rid = pl === 'darwin' ? 'osx-x64' : pl === 'win32' ? 'win-x64' : pl === 'linux' ? 'linux-x64' : null;
  if (!rid) throw new Error(`Unsupported platform: ${pl}`);
  return rid;
}

function resolveAddonPath(options = {}) {
  if (options.addonPath) {
    return options.addonPath;
  }

  const rid = getRid();
  const baseDirectory = options.addonDirectory ?? options.frameworkPath ?? __dirname;
  const candidates = options.addonDirectory || options.frameworkPath
    ? [
        join(baseDirectory, 'Motely.NodeAddon.node'),
        join(baseDirectory, rid, 'Motely.NodeAddon.node'),
        join(baseDirectory, 'bin', rid, 'Motely.NodeAddon.node'),
        join(baseDirectory, 'addon', rid, 'Motely.NodeAddon.node'),
      ]
    : [
        join(baseDirectory, 'bin', rid, 'Motely.NodeAddon.node'),
        join(baseDirectory, 'addon', rid, 'Motely.NodeAddon.node'),
      ];

  for (const candidate of candidates) {
    if (existsSync(candidate)) {
      return candidate;
    }
  }

  throw new Error(
    `Unable to locate Motely.NodeAddon.node for RID ${rid}. Tried: ${candidates.join(', ')}`
  );
}

function loadRawAddon(options) {
  const addonPath = resolveAddonPath(options);
  const addon = require(addonPath);
  return addon.MotelyNodeExports;
}

function parseJson(json, errKey = 'error') {
  const obj = typeof json === 'string' ? JSON.parse(json) : json;
  if (obj && obj[errKey] && !obj.seed && !obj.seeds) throw new Error(obj[errKey]);
  return obj;
}

function normalizeSeed(value, fieldName) {
  if (typeof value !== 'string' || value.trim() === '') {
    throw new Error(`${fieldName} must be a non-empty string.`);
  }

  return value.trim().toUpperCase();
}

function normalizeKeyword(value, fieldName) {
  if (typeof value !== 'string' || value.trim() === '') {
    throw new Error(`${fieldName} must be a non-empty string.`);
  }

  const normalized = value.trim().toUpperCase();
  if (normalized.length > 8) {
    throw new Error(`${fieldName} '${normalized}' is too long (max 8 chars).`);
  }

  return normalized;
}

function normalizeSearchParams(options = {}) {
  const {
    specificSeed,
    seeds,
    keyword,
    keywords,
    padding,
    randomSeeds,
    palindrome,
    ...rest
  } = options;

  const normalizedSeeds = [];
  if (specificSeed !== undefined) {
    normalizedSeeds.push(normalizeSeed(specificSeed, 'specificSeed'));
  }
  if (seeds !== undefined) {
    if (!Array.isArray(seeds) || seeds.length === 0) {
      throw new Error('seeds must contain at least one seed.');
    }
    for (let i = 0; i < seeds.length; i += 1) {
      normalizedSeeds.push(normalizeSeed(seeds[i], `seeds[${i}]`));
    }
  }

  const normalizedKeywords = [];
  if (keyword !== undefined) {
    normalizedKeywords.push(normalizeKeyword(keyword, 'keyword'));
  }
  if (keywords !== undefined) {
    if (!Array.isArray(keywords) || keywords.length === 0) {
      throw new Error('keywords must contain at least one keyword.');
    }
    for (let i = 0; i < keywords.length; i += 1) {
      normalizedKeywords.push(normalizeKeyword(keywords[i], `keywords[${i}]`));
    }
  }

  let normalizedPadding;
  if (padding !== undefined && padding !== null) {
    if (typeof padding !== 'string' || padding.trim() === '') {
      throw new Error('padding must be a non-empty string.');
    }
    if (normalizedKeywords.length === 0) {
      throw new Error('padding requires keyword search.');
    }

    normalizedPadding = padding.trim().toUpperCase();
    for (const ch of normalizedPadding) {
      if (!SEED_DIGITS.has(ch)) {
        throw new Error(`padding contains invalid character '${ch}'.`);
      }
    }
  }

  if (randomSeeds !== undefined && (!Number.isInteger(randomSeeds) || randomSeeds < 1)) {
    throw new Error('randomSeeds must be an integer >= 1.');
  }

  if (palindrome !== undefined && typeof palindrome !== 'boolean') {
    throw new Error('palindrome must be a boolean when provided.');
  }

  let explicitModeCount = 0;
  if (normalizedSeeds.length > 0) explicitModeCount += 1;
  if (normalizedKeywords.length > 0) explicitModeCount += 1;
  if (typeof randomSeeds === 'number') explicitModeCount += 1;
  if (palindrome === true) explicitModeCount += 1;

  if (explicitModeCount > 1) {
    throw new Error('Choose only one search mode: seeds, keywords, randomSeeds, or palindrome.');
  }

  return {
    ...rest,
    seeds: normalizedSeeds.length > 0 ? normalizedSeeds : undefined,
    keywords: normalizedKeywords.length > 0 ? normalizedKeywords : undefined,
    padding: normalizedPadding,
    randomSeeds,
    palindrome: palindrome === true,
  };
}

function buildApi(raw) {
  let disposed = false;

  return {
    async getCapabilities() {
      const json = await raw.GetCapabilitiesAsync();
      return parseJson(json);
    },

    async analyzeSeed(seed, deck, stake) {
      const json = await raw.AnalyzeSeedAsync(seed, deck, stake);
      return parseJson(json);
    },

    async validateJaml(jamlContent) {
      const json = await raw.ValidateJamlAsync(jamlContent);
      return parseJson(json);
    },

    async startJamlSearch(jamlContent, options = {}) {
      if (disposed) throw new Error('Motely instance has been disposed');
      const { onProgress, onResult, ...searchParams } = options;
      const results = [];
      const CHUNK = 500;
      const TOTAL_BLOCKS = 35 * 35 * 35;

      const runBlock = (dto) => {
        onProgress?.(dto.seedsSearched ?? 0, dto.seedsFound ?? 0, 0, dto.seeds?.length ?? 0);
        for (const s of dto.seeds ?? []) {
          results.push({ seed: s.seed, score: s.score ?? 0 });
          onResult?.(s.seed, s.score ?? 0);
        }
      };

      const opts = normalizeSearchParams({
        threadCount: Math.max(1, searchParams.threadCount ?? 1),
        batchCharCount: searchParams.batchCharCount ?? 4,
        ...searchParams,
      });
      const optionsJson = JSON.stringify(opts);

      if (opts.seeds?.length || opts.keywords?.length || typeof opts.randomSeeds === 'number' || opts.palindrome) {
        const json = await raw.RunSearchAsync(jamlContent, optionsJson);
        runBlock(parseJson(json));
        return results;
      }

      // Full sequential: run in chunks via RunSequentialRangeAsync
      let totalSearched = 0;
      const startBatch = Math.max(0, opts.startBatch ?? 0);
      const endBatch = opts.endBatch != null ? Math.min(TOTAL_BLOCKS, opts.endBatch) : TOTAL_BLOCKS;
      for (let start = startBatch; start < endBatch; start += CHUNK) {
        const end = Math.min(start + CHUNK, endBatch);
        const json = await raw.RunSequentialRangeAsync(jamlContent, start, end);
        const dto = parseJson(json);
        totalSearched += dto.seedsSearched ?? 0;
        runBlock(dto);
      }
      return results;
    },

    async processBlock(jamlContent, blockId) {
      if (disposed) throw new Error('Motely instance has been disposed');
      const json = await raw.ProcessBlockAsync(jamlContent, blockId);
      const dto = parseJson(json);
      return {
        blockId: dto.blockId,
        seedsSearched: dto.seedsSearched,
        seedsFound: dto.seedsFound,
        seeds: (dto.seeds ?? []).map((s) => ({ seed: s.seed, score: s.score ?? 0 })),
      };
    },

    dispose() {
      disposed = true;
    },
  };
}

function createDefaultApi() {
  let apiPromise = null;

  const getApi = () => {
    if (!apiPromise) {
      apiPromise = loadMotely();
    }
    return apiPromise;
  };

  return {
    async getCapabilities() {
      return (await getApi()).getCapabilities();
    },

    async analyzeSeed(seed, deck, stake) {
      return (await getApi()).analyzeSeed(seed, deck, stake);
    },

    async validateJaml(jamlContent) {
      return (await getApi()).validateJaml(jamlContent);
    },

    async startJamlSearch(jamlContent, options = {}) {
      return (await getApi()).startJamlSearch(jamlContent, options);
    },

    async processBlock(jamlContent, blockId) {
      return (await getApi()).processBlock(jamlContent, blockId);
    },

    dispose() {
      const current = apiPromise;
      apiPromise = null;
      void current?.then((api) => api.dispose());
    },
  };
}

export function loadMotely(options) {
  return Promise.resolve(buildApi(loadRawAddon(options)));
}

const api = createDefaultApi();

export default api;
