# JAML-LANG plan — adversarial review verdict

Reviewed against the real code (`Motely.Wasm/Program.cs`, `JamlConfigLoader.cs`,
`Events/LuckyMoneyFilterDesc.cs`, `AnteFeatures/VoucherFilterDesc.cs`) before any TS exists.

## Verdict: kill the central `Vocab()`. Reflect the clause types instead.

The plan's `Vocab()` hand-maps discriminator → enum (`tag→MotelyTag`, …) and re-lists keys.
That is a **second copy** of knowledge that already lives, correctly, in each FilterDesc/clause —
the *same* drift-by-construction the plan claims to kill, just moved from TS into C#. The plan
even mistyped its own example (`boss → "MotelyBoss"`; the real enum is `MotelyBossBlind`,
`JamlConfigLoader.cs:278`) — proof that any hand-authored list rots.

**The working version:** the clause classes ARE the schema. `VoucherClause.Vouchers` is
`MotelyVoucher[]`; `TagClause.Tags` is `MotelyTag[]`. Reflect the property type of the clause the
`switch` already constructs → value-enum-per-clause for free, zero hand table. The "vocab" is
derived from the place that filters, not re-listed centrally.

## Verified findings (stand behind these)

- **Event clauses crash in Release on a bare clause.** `Rolls = node.GetIntArray(disc) ?? []`
  (`JamlConfigLoader.cs:352`) defaults to empty — unlike `tag` (`?? [0,1]`, :288) / `voucher`
  (`?? [0]`, :241). The only guard is a `Debug.Assert` (`LuckyMoneyFilterDesc.cs:24`), stripped in
  Release; next line indexes `sorted[^1]` on the empty array (:41) → `IndexOutOfRangeException`.
  So `luckyMoney:` with just a `min:` **loads clean through `TryLoad`, then crashes at filter
  creation** — violates AGENTS.md "fail loudly with the exact load/build error."
  **Fix (narrow):** `?? [0]` parity with `voucher`, across the event clauses.
  **Note (per author):** JAML previously had a defaults mechanism that was removed; `tag`/`voucher`
  still carry *inline* loader defaults (`:288`/`:241`) but events don't. So the crash is the
  symptom of an inconsistent leftover, not a fresh bug — the narrow `?? [0]` patches parity with
  what survived; reviving a unified defaults layer is the real wish and a separate decision.
- **`Diagnose(text) -> JamlError[]` is aspirational.** `TryLoad` is fail-fast — first `throw` wins
  (`:81`). No accumulation. v1 is one squiggle at a time unless the loader is rewritten to collect.
- `ClauseKeys()` returns **inline literals** for ~15 discriminators (`:573-586`), not named
  `*Keys` arrays — so any reflection must call `ClauseKeys(d)` directly (currently `private`).

## The test that actually has teeth (the plan's three are tautologies)

The plan tests the generator against itself. Replace with round-trips through the real loader:

1. **Bare-clause-loads:** every discriminator, with only its discriminator key present, loads
   clean through `TryLoad`. *Fails today for events* — would have caught the crash above.
2. **Enum round-trip:** for every `(clause, enum-field)`, every real member loads clean and a
   post-normalization-bogus member fails (`ParseEnum` strips space/dash/underscore + case, :840).
3. **Negative completion:** a value position with no enum (event rolls) offers **zero** members —
   absence-of-enum must be an explicit "offer nothing," not a fallback to a merged list (that
   fallback IS the original Boss-in-Tags bug).

## Spike before writing engine code (UNVERIFIED — do not trust without checking)

- Does `SharpYaml.Model`'s DOM expose usable line/col marks? `NodeReader` throws the mapping away
  (`:913-923`) — "thread marks through the loader" may mean re-architecting the core reader.
- Can Bootsharp marshal a nested manifest, or must `Vocab()`/`Diagnose()` return a JSON string?
  Everything in `Program.cs` returns flat arrays / strings / concrete records.
- `voucher`/other clauses with empty `antes` — behavior NOT verified here; check the FilterDesc
  before assuming, same defaults-gap pattern as event rolls may apply.
