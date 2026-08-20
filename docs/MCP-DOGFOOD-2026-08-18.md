# seedfinder.app MCP — dogfood pass, 2026-08-18

Every tool on the `Balatro_Seed_Finder` MCP server was hit with real inputs (today's Cola Faucet filter and its 247 hits) plus edge inputs. Each item below has the exact call that reproduces it. Ordered by how much damage it does.

Server-side code lives in the seedfinder.app repo (`lib/party/coordinator.ts`, `app/api/*` per `Motely.DistributedWorker/README.md`), which is not in `D:\MotelyJAML`. Everything here is filed against observed behaviour; nothing was patched.

---

## S1 — crashes that eat every other diagnostic

### 1. Any loader failure comes back as `C# exception from NativeAOT`

`score_seeds`, `jamlyze_seeds` and `find_similar_seeds` return the same opaque string for every kind of user mistake. None of them run the validator that `start_seed_hunt` already has.

| Input | `score_seeds` | `start_seed_hunt` (same JAML) |
|---|---|---|
| unterminated `antes: [1, 2` | NativeAOT crash | `JAML parse error at line 6: Unterminated flow array (missing ']')` |
| unknown key `editon: Negative` | NativeAOT crash | `Unknown clause key: 'editon'.` |
| `joker: TotallyFakeJoker` | NativeAOT crash | (validator stops at first error; would need a second run) |
| `deck: Ghosst` | NativeAOT crash | — |
| `joker: []` / `voucher: []` / `tag: []` / `spectralCard: []` / `legendaryJoker: []` | NativeAOT crash | `'joker' clause requires a value.` |
| bare JUMMY line as the whole document | NativeAOT crash | — |

Repro (any of the above):

```yaml
name: bad
deck: Ghost
stake: White
must:
  - joker: DietCola
    antes: [1, 2, 3]
    editon: Negative
```
`score_seeds(jaml, ["DGFO7C11"])` → `C# exception from NativeAOT`.

Fix, server side, no WASM rebuild needed: run the `start_seed_hunt` validator at the top of `score_seeds`, `jamlyze_seeds`, `find_similar_seeds`, and return its message. Also expose it as its own tool — `validate_jaml(jaml)` — because right now an agent has no way to check JAML before spending a call. `Motely.Lsp.Core` already has `Diagnostics(text)` with spans and stable codes; the WASM package exports it under `MotelyLsp`; nothing on the MCP surface reaches it.

### 2. `[]` "category any" means three different things in three engines

`JAML.md` §"Category any" documents `joker: []` as valid grammar. Today:

- **CLI** (`dotnet run --project Motely.CLI`): accepts `legendaryJoker: []`, ran 7.8 B seeds, found 247 matches.
- **server hunt validator** (`start_seed_hunt`): rejects with `'legendaryJoker' clause requires a value.`
- **WASM** (`score_seeds` etc.): NativeAOT crash.

Same document, three verdicts. Needs one authoritative answer and a test in each head. If the answer is "valid", the hunt validator is wrong and WASM crashes on legal input; if "invalid", `JAML.md` lines 41–58 and 305 are wrong and the CLI is silently accepting a filter that the server will refuse.

### 3. `find_similar_seeds` crashes on two valid seeds

`find_similar_seeds(seeds=["MVR7ZU31","DGFO7C11"], query="perkeo")` → `C# exception from NativeAOT`. The tool scans the community library plus the built-in corpus; one library filter that trips #1 kills the whole call. Needs per-filter try/catch and a `skipped: [{slug, error}]` list in the result, not all-or-nothing.

---

## S2 — payloads and caps

### 4. `list_filters` returns 277 KB and has no parameters

No `limit`, `offset`, `query`, `fields`. From an agent it is unusable without spilling to disk (which is what happened). Minimum: `limit` (default 25), `query` substring on name/slug, and a `summary` mode returning `{slug, name, deck, stake}` only.

### 5. `jamlyze_seeds` returns ~147 KB per seed regardless of the JAML

Two seeds → 265,664 bytes. Per ante it dumps 11 pull streams (`judgementJokers`, `wraithJokers`, `emperorTarots`, `purpleSealTarots`, `sixthSenseSpectrals`, `seanceSpectrals`, `riffRaffJokers`, `rareTagJokers`, `uncommonTagJokers`, `legendaryJokers`, `voucherSequence`) and 7 shop streams, 20 items each, for all 8 antes — for a filter that asked about a tag and one joker. Needs `antes: [..]`, `fields: [...]` (or `verbosity: minimal|standard|full`), and a default that returns only what the JAML's clauses reference plus shop/packs/tags/boss/voucher.

### 6. Seed caps are inconsistent and unhelpful

`score_seeds` caps at 1000 seeds (schema `maxItems`), `find_similar_seeds` at 10000. Today's list was ~1800; the only signal was a schema rejection with no "send in batches of 1000" hint and no `offset`. Either raise `score_seeds` to match, or document batching in the error.

### 7. `plan_seed_search` returns the entire vocabulary

The response for "Perkeo early, Ankh in shop, Diet Cola before 8, Charm Tag ante 8, negative legendaries" contained all 150 jokers, all 32 vouchers, all tarots/spectrals/planets/bosses/tags — the description says "matched to the request"; it is not matched, it is everything (~4 KB of names). Corpus examples returned: Hallucination, Negative Tag, Diet Cola. Missed Perkeo, Ankh, Ghost, Charm Tag. This duplicates `learn_jaml topic=vocabulary` and adds little.

---

## S3 — semantics

### 8. `rejected` conflates four different things

All of these land in `rejected: [...]` with no distinction:

- failed a `must` gate (the intended meaning)
- malformed seed string: `"dgfo7c11"` (lowercase), `"DGFO7C1"` (7 chars), `"DGF07C11"` (zero is not in the alphabet)
- negative total score: `should: - joker: DietCola score: -50` → every seed rejected, including ones that hit
- empty filter (no `must`, no `should`) → every seed rejected; but should-only → every seed matches (score 0 ok)

Suggested shape: `{matches, rejected, invalid: [{seed, reason}]}`; normalize case on input; decide whether negative totals are "rejected" or "matched with negative score" and say so.

### 9. `mustNot` and `should` disagree on the same clause

```yaml
must:
  - tag: CharmTag
    antes: [8]
mustNot:
  - joker: DietCola
    antes: [1]
```
seeds `["DGFO7C11","MVR7ZU31","1NCEB6CB"]` → matches: DGFO7C11, MVR7ZU31; rejected: 1NCEB6CB.

But a per-ante `should` probe (`- joker: DietCola / antes: [1] / score: 1`, one clause per ante) tallies **ante 1 = 1 for both MVR7ZU31 and 1NCEB6CB**. Same clause, same default sources: `should` says MVR7ZU31 has a Cola in ante 1, `mustNot` says it doesn't. `mustNot` on `tag: CharmTag antes: [8]` correctly rejects everything, so the mechanism works; the joker/ante-1 case is inconsistent. Engine-side; needs a unit test with these three seeds.

### 10. Silent defaults

JAML with no `deck:`/`stake:` runs and returns matches without saying which deck/stake it assumed. Echo the effective deck/stake in every result.

---

## S4 — data and ops hygiene

### 11. `list_seeds` shows the same seed saved five times

`H95HQCVY` appears as ids 57, 147, 148, 149, 150 across Plasma/Ghost/Ghost/Red/Black, all `score: 1000`, all `filter_slug: null`, `tallies: null`, `found_by: null`. `save_seed` accepts empty provenance and does not dedupe on `(seed, deck, stake, filter_slug)`.

### 12. `show_seedfinder_app` is an echo

Returns `{"jaml": <input>}` and nothing else — no URL, no confirmation the app loaded it, and no validation (it happily echoed `deck: Ghosst` + unterminated array). `plan_seed_search`'s own description says "validate it by passing it to show_seedfinder_app". It does not validate. Return a URL and run the validator.

### 13. Unknown `runId` leaks infrastructure

`get_seed_hunt_status(runId="definitely-not-a-run")` → `GET /v2/runs/definitely-not-a-run?remoteRefBehavior=lazy -> HTTP 400: Bad Request (x-vercel-id=iad1:...)`. Should be `run not found`.

### 14. Seeds-per-batch is still an open contradiction

`Motely.DistributedWorker/README.md` already records it: engine says `35^batchChars`, seedfinder's hunt workflow says `35^(batchChars-1)`. `start_seed_hunt`'s description says `35^n` (5 → ≈52.5 M, which is 35^5). Unresolved. Engine should export alphabet size and seeds-per-batch so neither side hardcodes it.

### 15. The WASM the server runs is not the WASM in this repo

`Motely.Wasm/BOOTSHARP-0.9-ADVERSARIAL-REVIEW.md` (2026-08-10) already found this: repo source exports `runScoreSeeds`/`takeRun`; the published `motely-wasm@25.0.3` exports `MotelySearch.searchList` and `MotelyJamlyzer.analyzeSeeds` — which are exactly the names in the MCP tool descriptions. So every crash in S1 lives in an artifact this repo cannot currently rebuild (review also notes the `wasm-tools` workload was missing). Two paths: (a) fix and republish from whatever built 25.0.3; (b) migrate the server to the repo's `run/take` contract — and if so, move `JamlConfigLoader.TryLoad` **inside** the `try` in `RunScoreSeeds`/`RunFindSeeds`, because today it sits outside and a loader throw under AOT is exactly the unhandled path that produces the opaque message.

---

## What worked

`get_filter`, `learn_jaml` (all topics), `estimate_keyword_search`, JUMMY one-liners under `must:` (`- Negative Perkeo / ante: 1`, `- Blueprint in antes 1 or 2`), `start_seed_hunt`'s validator, and the scoring math itself: duplicate source indices dedupe (a clause caps at one hit), a `should` clause with no `score:` is worth 1, tallies are positional and correct. `mustNot` works for tags. Named legendaries with `edition:` and `sources:` work.

## Suggested order

1. Reuse the hunt validator in `score_seeds` / `jamlyze_seeds` / `find_similar_seeds`; add `validate_jaml`. (Removes the entire S1 wall from the user's side in one change.)
2. Decide `[]` once; fix the two heads that disagree; add a test in CLI, hunt validator, WASM.
3. `limit`/`query` on `list_filters`; `antes`/`fields` on `jamlyze_seeds`.
4. Split `rejected` into `rejected` + `invalid`; normalize seed case.
5. Per-filter isolation in `find_similar_seeds`.
6. `mustNot`/`should` ante-1 discrepancy — engine unit test with `MVR7ZU31`, `1NCEB6CB`.
7. Dedupe `save_seed`; echo deck/stake; real URL from `show_seedfinder_app`; `run not found`.
