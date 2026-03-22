# Plan iteration: TS-first `.jummy`, separate file, `filter:` key

Supersedes / extends the jummy + aesthetics plan with **authoring vs execution** split and **TypeScript-first** options.

## 1. TS-only path (still useful for a “full” impl later)

**Idea:** Implement **`.jummy` ↔ `.jaml`** in **TypeScript only** first (Zod + YAML/JSON parse, string templates or structured emit).

**Why it stays valuable later:**

- **RAG / JAMMY** already wants Zod + few-shot output validation in JS; you do not block on C# ship cycles.
- The **same YAML shape** can become the contract: later add a **C# reader** (YamlDotNet DTO) or **compile step** in CLI that calls into existing `JamlConfig` load — the IR you emit is still **valid JAML**.
- **Bidirectional** is easier to prototype in TS (`jaml` → AST-ish → jummy) for “edit in chat, round-trip” on a **lossy subset** (document what is not representable in jummy v0).

**Risk to manage:** Drift between TS emitter and real JAML semantics — mitigate with **golden `.jaml` files** checked into `Motely.Tests` or a small `node test` that round-trips through **Motely.CLI validate** if you add it.

---

## 2. Keep `.jummy` a **totally separate** file

**Good call.**

- **`.jummy`** = human / LLM authoring format (WHAT + WHERE, macros, loose keys).
- **`.jaml`** = execution IR consumed by Motely today (must / should / mustNot, full clause set).
- Pipeline: `jummy-tool convert foo.jummy > foo.jaml` (or in-memory in Vercel), then existing search stack unchanged.

You can defer **embedding** `jummy:` inside JAML until you know the merge rules (precedence vs `must:`).

---

## 3. Your sketch: `filter:`, `aesthetic`, `must:`, `score:`

Example (conceptual):

```yaml
filter:
  - PerkeoObservatory
aesthetic:
  - palindrome
must:
  - legendaryJoker: Perkeo
    edition: Negative
score:
  - planetCard: TheMoon
    score: 10
  - joker: Any
    edition: Negative
```

**Thoughts:**

| Key | Role | Notes |
|-----|------|--------|
| **`filter:`** | **Named preset / macro** — expands to one or more native filter descriptors or JAML clause subtrees | `PerkeoObservatory` maps to [`PerkeoObservatoryFilterDesc`](Motely/filters/Native/PerkeoObservatoryDesc.cs) today, but **JAML has no top-level `filter:` key yet**. TS layer can hold a **registry** `Record<string, JamlFragment>` (or must-blocks) and expand before load. Later: optional C# registry for parity. |
| **`aesthetic` vs `aesthetics`** | Today JAML + schema use **`aesthetics`** (plural) ([`jaml.schema.json`](../jaml.schema.json), [`MergeJamlAesthetics`](Motely.Orchestration/MotelySearchOrchestrator.cs)) | For author UX, accept **`aesthetic:`** as **YAML alias** in a jummy loader and normalize to `aesthetics` on emit — avoid breaking existing files. |
| **`must:`** | Already the spirit of JAML **`must:`** clauses | Your shorthand aligns with existing clause keys like **`legendaryJoker`** ([`JamlConfig`](Motely/filters/Jaml/JamlConfig.cs)). |
| **`score:`** | Parallel to JAML **`should:`** scoring | Motely uses **should** + scoring descriptors ([`JamlScoring`](Motely/filters/Jaml/JamlScoring.cs), [`JamlShouldScoreDesc`](Motely/filters/Jaml/JamlShouldScoreDesc.cs)). In jummy you can use **`score:`** as sugar that **lowers to `should:`** blocks with `score: N` on emit — document the mapping. |

---

## 4. Outside the box

1. **JAML as IR, jummy as source** — same relationship as TypeScript → JavaScript: optimize for **authoring** in jummy; never require humans to hand-edit full IR for simple seeds.
2. **`filter:` as expansion table** — ship `filters.json` (name → snippet) in **motely** or **Jammy** repo; community PRs add rows without touching C#. Full impl later: validate names against a generated list from reflection (optional).
3. **Lossy round-trip** — `jaml → jummy` only for clauses in the **jummy subset**; everything else becomes a literal `rawJaml: |` block in jummy so nothing is dropped (escape hatch).
4. **Single aesthetic key** — if you want `aesthetic: palindrome` (scalar), jummy normalizes to `aesthetics: [palindrome]` for output JAML.

---

## 5. Suggested phased order (revised)

1. **TS:** Zod schema for `.jummy` v0 + `convert` to `.jaml` (string emit or YAML AST).
2. **TS:** `filter:` registry with at least `PerkeoObservatory` → emitted `must` / native equivalent fragment you already support in JAML.
3. **Normalize** `aesthetic` → `aesthetics` on emit; document.
4. **Map** `score:` → `should:` + scoring fields matching [`JamlConfig`](Motely/filters/Jaml/JamlConfig.cs).
5. **Later C#:** optional load path or CLI `motely compile-jummy` that shells to Node or ports the emitter.

---

## 6. Relation to aesthetics verification

Unchanged: **`aesthetics`** is already in JAML with **`palindrome`** wired through orchestrator. New **`aesthetic:`** is a **jummy-side alias**, not a second C# key, unless you explicitly add Yaml alias on `JamlConfig` (possible but not required if jummy always emits `aesthetics`).
