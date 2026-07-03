# JAML Schema Reference

## Root keys

| Key | Purpose |
|-----|---------|
| `id` | Unique identifier for the filter. Auto-slugified from `name` if omitted. |
| `name` | Human-readable filter title. |
| `description` | Longer explanation of what the filter finds. |
| `author` | Filter author. |
| `dateCreated` | ISO date string. |
| `deck` | Balatro deck to search. (Red, Blue, Yellow, ...) |
| `stake` | Stake difficulty. (White, Red, Green, Black, Blue, Purple, Orange, Gold) |
| `seeds` | List of specific seeds to evaluate. |
| `must` | Clauses that must all match for a seed to be returned. |
| `should` | Clauses that increase a seed's score when matched. |
| `mustNot` | Clauses that disqualify a seed if matched. |

## Clause keys

| Key | Purpose |
|-----|---------|
| `joker` / `jokers` | Match a specific joker. |
| `edition` | Required item edition: Foil, Holographic, Polychrome, Negative. |
| `stickers` | Required stickers: Eternal, Perishable, Rental. |
| `shopItems` | Constraint on shop item availability. |
| `boosterPacks` | Constraint on booster pack contents. |
| `ante` | Single ante number to evaluate. |
| `antes` | List of ante numbers (joined with OR semantics). |
| `min` | Minimum number of times the clause must match. |
| `max` | Maximum number of times the clause may match. |
| `score` | Score contribution for `should` clauses. |
| `label` | Optional label for debugging. |
| `sources` | Where the item can come from (shop, packs, cards, tags, etc.). |
| `with` | Additional context such as luck or vouchers. |
| `luck` | Luck multiplier for events. |
| `vouchers` | Required vouchers for the clause. |

## Logic keys

| Key | Purpose |
|-----|---------|
| `and` | All nested clauses must match. |
| `or` | At least one nested clause must match. |
| `clauses` | List of nested clauses. |
