"use strict";

const { platform } = require("node:os");
const { join } = require("node:path");

const SEED_DIGITS = new Set("123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".split(""));

function getRid() {
  const pl = platform();
  if (pl !== "linux") {
    throw new Error(
      `motely-node only ships a linux-x64 binary (for Vercel). Current platform: ${pl}`
    );
  }
  return "linux-x64";
}

function resolveAddonPath() {
  return join(__dirname, "bin", getRid(), "Motely.NodeAddon.node");
}

function loadRawAddon() {
  const addonPath = resolveAddonPath();
  const addon = require(addonPath);
  const raw = addon?.MotelyNodeExports;
  if (!raw || typeof raw !== "object") {
    throw new Error(
      `Motely native addon at '${addonPath}' did not expose MotelyNodeExports.`
    );
  }
  return raw;
}

function getRawMethod(raw, methodName) {
  const method = raw?.[methodName];
  if (typeof method !== "function") {
    const available =
      raw && typeof raw === "object"
        ? Object.keys(raw).sort().join(", ")
        : "<none>";
    throw new Error(
      `Motely native addon is missing '${methodName}'. Available exports: ${available}`
    );
  }
  return method.bind(raw);
}

// ── Input normalization ───────────────────────────────────────────────────────

function normalizeSeed(value, fieldName) {
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`${fieldName} must be a non-empty string.`);
  }
  return value.trim().toUpperCase();
}

function normalizeKeyword(value, fieldName) {
  if (typeof value !== "string" || value.trim() === "") {
    throw new Error(`${fieldName} must be a non-empty string.`);
  }
  const normalized = value.trim().toUpperCase();
  if (normalized.length > 8) {
    throw new Error(`${fieldName} '${normalized}' is too long (max 8 chars).`);
  }
  return normalized;
}

function normalizeSearchParams(options = {}) {
  const { specificSeed, seeds, keyword, keywords, padding, randomSeeds, palindrome, ...rest } =
    options;

  const normalizedSeeds = [];
  if (specificSeed !== undefined) {
    normalizedSeeds.push(normalizeSeed(specificSeed, "specificSeed"));
  }
  if (seeds !== undefined) {
    if (!Array.isArray(seeds) || seeds.length === 0) {
      throw new Error("seeds must contain at least one seed.");
    }
    for (let i = 0; i < seeds.length; i += 1) {
      normalizedSeeds.push(normalizeSeed(seeds[i], `seeds[${i}]`));
    }
  }

  const normalizedKeywords = [];
  if (keyword !== undefined) {
    normalizedKeywords.push(normalizeKeyword(keyword, "keyword"));
  }
  if (keywords !== undefined) {
    if (!Array.isArray(keywords) || keywords.length === 0) {
      throw new Error("keywords must contain at least one keyword.");
    }
    for (let i = 0; i < keywords.length; i += 1) {
      normalizedKeywords.push(normalizeKeyword(keywords[i], `keywords[${i}]`));
    }
  }

  let normalizedPadding;
  if (padding !== undefined && padding !== null) {
    if (typeof padding !== "string" || padding.trim() === "") {
      throw new Error("padding must be a non-empty string.");
    }
    if (normalizedKeywords.length === 0) {
      throw new Error("padding requires keyword search.");
    }
    normalizedPadding = padding.trim().toUpperCase();
    for (const ch of normalizedPadding) {
      if (!SEED_DIGITS.has(ch)) {
        throw new Error(`padding contains invalid character '${ch}'.`);
      }
    }
  }

  if (randomSeeds !== undefined && (!Number.isInteger(randomSeeds) || randomSeeds < 1)) {
    throw new Error("randomSeeds must be an integer >= 1.");
  }

  if (palindrome !== undefined && typeof palindrome !== "boolean") {
    throw new Error("palindrome must be a boolean when provided.");
  }

  let explicitModeCount = 0;
  if (normalizedSeeds.length > 0) explicitModeCount += 1;
  if (normalizedKeywords.length > 0) explicitModeCount += 1;
  if (typeof randomSeeds === "number") explicitModeCount += 1;
  if (palindrome === true) explicitModeCount += 1;
  if (explicitModeCount > 1) {
    throw new Error("Choose only one search mode: seeds, keywords, randomSeeds, or palindrome.");
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

// ── Search result helpers ─────────────────────────────────────────────────────

function collectResults(dto, results, onResult) {
  // seeds is now string[] — no per-seed score, highestScore on the block.
  for (const seed of dto.seeds ?? []) {
    const result = { seed, score: dto.highestScore ?? 0 };
    results.push(result);
    onResult?.(seed, result.score);
  }
}

async function runSearchWithRaw(raw, jamlContent, options = {}) {
  const { onProgress, onResult, ...searchParams } = options;
  const results = [];
  const CHUNK = 500;
  // 35^(8-5) = 42,875 blocks — mirrors ProcessBlockRunner.TotalBlocks
  const TOTAL_BLOCKS = Math.pow(35, 8 - 5);

  const onBlock = (dto) => {
    onProgress?.(dto.seedsFound ?? 0, dto.highestScore ?? 0, 0, dto.seeds?.length ?? 0);
    collectResults(dto, results, onResult);
  };

  const opts = normalizeSearchParams({
    threadCount: Math.max(1, searchParams.threadCount ?? 1),
    batchCharCount: searchParams.batchCharCount ?? 4,
    ...searchParams,
  });

  // ── Specific seed list ──────────────────────────────────────────────────
  if (opts.seeds?.length) {
    onBlock(await getRawMethod(raw, "runListSearchAsync")(jamlContent, opts.seeds));
    return results;
  }

  // ── Keyword(s) ──────────────────────────────────────────────────────────
  if (opts.keywords?.length) {
    onBlock(
      await getRawMethod(raw, "runKeywordsSearchAsync")(
        jamlContent,
        opts.keywords,
        opts.padding ?? null
      )
    );
    return results;
  }

  // ── Random seeds ────────────────────────────────────────────────────────
  if (typeof opts.randomSeeds === "number") {
    onBlock(await getRawMethod(raw, "runRandomSearchAsync")(jamlContent, opts.randomSeeds));
    return results;
  }

  // ── Palindrome ──────────────────────────────────────────────────────────
  if (opts.palindrome === true) {
    onBlock(await getRawMethod(raw, "runPalindromeSearchAsync")(jamlContent));
    return results;
  }

  // ── Sequential range (default) ──────────────────────────────────────────
  const startBatch = Math.max(0, opts.startBatch ?? 0);
  const endBatch = opts.endBatch != null ? Math.min(TOTAL_BLOCKS, opts.endBatch) : TOTAL_BLOCKS;
  for (let start = startBatch; start < endBatch; start += CHUNK) {
    const end = Math.min(start + CHUNK, endBatch);
    onBlock(await getRawMethod(raw, "runSequentialRangeAsync")(jamlContent, start, end));
  }

  return results;
}

// ── API surface ───────────────────────────────────────────────────────────────

function buildApi(raw) {
  let disposed = false;

  return {
    /** Sync — returns capabilities immediately (no async work needed). */
    getCapabilities() {
      return getRawMethod(raw, "getCapabilities")();
    },

    /** Sync — full seed analysis. Fast enough for direct call (< 1ms per seed). */
    analyzeSeed(seed, deck, stake) {
      return getRawMethod(raw, "analyzeSeed")(seed, deck, stake);
    },

    /** Sync — validates JAML and returns {valid, name, deck, stake}. */
    validateJaml(jamlContent) {
      return getRawMethod(raw, "validateJaml")(jamlContent);
    },

    async startJamlSearch(jamlContent, options = {}) {
      if (disposed) throw new Error("Motely instance has been disposed");
      return runSearchWithRaw(raw, jamlContent, options);
    },

    async processBlock(jamlContent, blockId) {
      if (disposed) throw new Error("Motely instance has been disposed");
      return getRawMethod(raw, "processBlockAsync")(jamlContent, blockId);
    },

    dispose() {
      disposed = true;
    },
  };
}

// ── Default lazy-loaded singleton ─────────────────────────────────────────────

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

function loadMotely() {
  return Promise.resolve(buildApi(loadRawAddon()));
}

const api = createDefaultApi();

module.exports = {
  loadMotely,
  default: api,
};
