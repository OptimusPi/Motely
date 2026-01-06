I need to refactor the Column Architecture to support complex reporting scenarios beyond simple integer tallies.

Here is the RFC for the new architecture. Please analyze your current `IColumnDefinition` implementation and help me refactor it to support these new requirements, specifically the `MultiSource` strategy and the `InlineLabel` type.

# RFC: Advanced Column Architecture & Multi-Source Reporting

## 1. Problem Statement
Current implementation is limited to `ScoreTally` (simple integer counts/sums). We need rich reporting capabilities (Strings, Enums, Lists) and the ability to aggregate disparate "signals" (e.g., "Best score from Scorer A OR Scorer B") into a single, clean CSV column.

## 2. Proposed Column Types
Refactor the Column definition to support a `ColumnType` discriminator.

### `ColumnType.ScoreTally` (Legacy/Default)
*   **Behavior**: Arithmetic sum of a specific signal.
*   **Output**: Integer.
*   **Example**: `WeeJoker_Count: 2`

### `ColumnType.InlineLabel` (Feature Request)
*   **Behavior**: Outputs a static or dynamic string based on a condition, or joins multiple string matches.
*   **Output**: String (quoted CSV value).
*   **Attributes**:
    *   `DefaultValue`: String (e.g., "NULL", "None") - *User requested SPACE by default*.
    *   `Prefix/Suffix`: Optional decoration.
*   **Example**: `Wee_Edition: "Negative"` or `Deck_Tag: "Anaglyph"`

### `ColumnType.AnteDisplay`
*   **Behavior**: Reports the Ante number(s) where a specific event occurred.
*   **Output**: Integer or Array String `[2, 4]`.
*   **Formatting Strategy**:
    *   `FirstFound`: Returns first ante number (e.g., `2`).
    *   `BestFound`: Returns ante with highest associated "score" (requires complex scorer).
    *   `AllList`: Returns `2|5|8` (pipe delimited for single-column strictness) or JSON array.
*   **Example**: `Perkeo_Ante: 2`

### `ColumnType.ItemDisplay`
*   **Behavior**: Reports the specific names of items found (Jokers, Tarots, etc.) passing a filter.
*   **Output**: String.
*   **Example**: `Rare_Joker_Found: "Blueprint"` (where filter was `Rarity == Rare`).

---

## 3. The "Multi-Source" Aggregation Strategy
**User Problem**: "I want to link multiple filters to ONE column. Finding the best group of Oops! All 6s... using sums across antes, but reporting the MAX found in any single ante."

**Solution**: Decouple `Scorers` (Data Finders) from `Columns` (Data Reporters).

### Architecture
A **Column** now possesses a `Pipeline`:
1.  **Inputs**: List of `IScorer` or `IFilter` instances.
2.  **AggregationStrategy**: Logic to reduce Inputs to a single value.
3.  **Formatter**: Logic to turn that value into a CSV string.

### Aggregation Strategies

#### 1. `Strategy.MaxOf` (The "Best" Logic)
*   **Logic**: Run *all* attached scorers. Return the highest single value returned by any of them.
*   **Use Case**: "Score of 5 from Scorer A vs Score of 2 from Scorer B -> Result: 5".
*   **Advanced**: "Find the Ante with the *maximum* density of desired cards."

#### 2. `Strategy.SumAll`
*   **Logic**: Run all attached scorers. Sum their total results.
*   **Use Case**: "Total Value = (Value of Jokers) + (Value of Consumables)".

#### 3. `Strategy.FirstMatch` (The "Or" Logic)
*   **Logic**: Iterate through scorers in order. Return the first non-zero/non-null result.
*   **Use Case**: "Did we find a Perkeo? If yes, report 'Perkeo'. If no, did we find a Blueprint? Report 'Blueprint'. Else 'None'."

#### 4. `Strategy.Coalesce` (String Joining)
*   **Logic**: Run all scorers. Collect all non-null strings. Join them with a delimiter (e.g., `+` or `, `).
*   **Use Case**: `Editions_Found: "Holographic + Polychrome"`

---

## 4. Implementation Goal
Please create the C# interfaces and classes required to support:
1.  `ColumnType` Enum.
2.  `IReportingStrategy` interface (for Max/Sum/FirstMatch).
3.  A generic `ColumnDefinition` class that can accept multiple scorers and a strategy.