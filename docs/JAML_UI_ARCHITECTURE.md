# JAML UI Architecture

## 1. JAML Schema Data Model

The core domain: what a `.jaml` filter file contains and how it flows through the system.

```mermaid
classDiagram
    class JamlFilter {
        +string name
        +string description
        +string author
        +string dateCreated
        +Deck deck
        +Stake stake
        +Defaults defaults
        +Clause[] must
        +Clause[] should
        +Clause[] mustNot
    }

    class Defaults {
        +int[] antes
        +int[] boosterPacks
        +int[] shopItems
        +int score
    }

    class Clause {
        +ClauseType type
        +string value
        +string label
        +int[] antes
        +int score
        +Edition edition
        +Seal seal
        +Enhancement enhancement
        +string rank
        +string suit
        +Sources sources
        +Clause[] and
        +Clause[] or
    }

    class Sources {
        +int[] shopItems
        +int[] boosterPacks
        +int minShopSlot
        +int maxShopSlot
        +int minPackSlot
        +int maxPackSlot
        +bool tags
        +bool requireMega
        +int[] judgement
        +int[] rareTag
        +int[] uncommonTag
        +int[] riffRaff
        +int[] purpleSealOrEightBall
        +int[] emperor
        +int[] sixthSense
        +int[] seance
        +int[] uncommonShopJokers
        +int[] rareShopJokers
        +int[] commonShopJokers
    }

    JamlFilter "1" --> "0..1" Defaults
    JamlFilter "1" --> "*" Clause : must
    JamlFilter "1" --> "*" Clause : should
    JamlFilter "1" --> "*" Clause : mustNot
    Clause "1" --> "0..1" Sources
    Clause "1" --> "*" Clause : and/or (nested)
```

## 2. JAML Clause Type Taxonomy

Every item type JAML can search for, grouped by category.

```mermaid
mindmap
  root((JAML Clause Types))
    Jokers
      Joker
      SoulJoker
    Cards
      TarotCard
      Planet / PlanetCard
      Spectral / SpectralCard
      StandardCard
    Economy
      Voucher
    Blinds
      Boss / BossBlind
      SmallBlindTag
      BigBlindTag
      Tag
    Events
      LuckyMoney
      LuckyMult
      MisprintMult
      WheelOfFortune
      CavendishExtinct
      GrosMichelExtinct
    Erratic
      ErraticRank
      ErraticSuit
    Logic
      And
      Or
```

## 3. End-to-End Data Flow

How JAML goes from the user's brain to search results on screen.

```mermaid
flowchart TB
    subgraph UI["Vue 3 UI (vue-jaml-ui)"]
        direction TB
        JB[JamlBuilder<br/>Visual form editor]
        SVE[SoftVisualEditor<br/>Bubble drag UI]
        ME[Monaco Editor<br/>Raw YAML editing]
        
        JB -->|"update:jaml"| EP[EditorPanel<br/>Mode switcher]
        SVE -->|"update:jaml"| EP
        ME -->|"update:jaml"| EP

        EP -->|jamlContent| JamlUI[JamlUI.vue<br/>Main orchestrator]
    end

    subgraph API["Motely.API (.NET)"]
        direction TB
        REST[REST Endpoints<br/>POST /api/search/start<br/>GET /api/filters]
        HUB[SignalR Hub<br/>searchHub]
        ENG[Search Engine<br/>MotelySearch.cs]
        
        REST --> ENG
        HUB --> ENG
    end

    subgraph WASM["Motely.BrowserWasm"]
        direction TB
        WEXP[WasmExports<br/>startSearch/getResults]
        WSRCH[MotelySearchSync<br/>In-browser search]
        
        WEXP --> WSRCH
    end

    subgraph Results["Results Display"]
        direction TB
        RP[ResultsPanel<br/>Tabulator table]
        BP[BlueprintPanel<br/>Seed analyzer iframe]
        ASP[ActiveSearchesPanel<br/>Progress tracking]
    end

    JamlUI -->|"HTTP POST (jaml yaml)"| REST
    JamlUI -->|"SignalR connect"| HUB
    JamlUI -->|"JS interop (future)"| WEXP

    HUB -->|"onResult"| RP
    HUB -->|"onProgress"| ASP
    REST -->|"search results"| RP
    
    RP -->|"selected seed"| BP
```

## 4. UI Component Hierarchy

The actual Vue component tree and how panels compose.

```mermaid
flowchart TB
    App[App.vue] --> Router{Vue Router}
    Router -->|"/"| Home[Home.vue<br/>Landing page]
    Router -->|"/jaml"| JamlUI[JamlUI.vue<br/>Main workspace]
    Router -->|"/genie"| Genie[JamlGenie.vue]

    JamlUI --> PM[PanelManager.vue]
    JamlUI --> SM[SettingsModal.vue]
    JamlUI --> EM[ErrorModal.vue]

    PM --> SP[SplitPane.vue<br/>Left/Right split]

    SP --> PS1[PanelSection.vue<br/>Left panels]
    SP --> PS2[PanelSection.vue<br/>Right panels]

    PS1 --> EP[EditorPanel.vue]
    PS1 --> JGP[JamlGeniePanel.vue]
    PS2 --> RP[ResultsPanel.vue]
    PS2 --> CP[ChatPanel.vue]
    PS2 --> ASP[ActiveSearchesPanel.vue]
    PS2 --> BPP[BlueprintPanel.vue]
    PS2 --> REQ[RequestsPanel.vue]

    EP --> ME[Monaco Editor<br/>useMonaco.js]
    EP --> SVE[SoftVisualEditor.vue]
    EP --> JB[JamlBuilder.vue]
    
    JB --> CB[ClauseBucket.vue]
    CB --> CC[ClauseChip.vue]
```

## 5. JAML Filter Lifecycle

A filter goes through these states from creation to search results.

```mermaid
stateDiagram-v2
    [*] --> Drafting: User opens editor

    Drafting --> Editing: Type YAML or use builder
    Editing --> Validating: Schema validation (Monaco)
    Validating --> Editing: Errors found
    Validating --> Ready: Valid JAML

    Ready --> Saving: Ctrl+S or Save button
    Saving --> Ready: Saved to API

    Ready --> Searching: Start Search
    Searching --> Progress: SignalR onProgress
    Progress --> Progress: Batch updates
    Progress --> Complete: All seeds checked
    Progress --> Stopped: User stops

    Complete --> Results: Seeds displayed
    Stopped --> Results: Partial results

    Results --> Analyzing: Click seed row
    Analyzing --> Blueprint: Open in Blueprint
    
    Results --> Drafting: Edit filter
```

## 6. JAML Source Resolution

How the `sources` object in a clause determines where to look for items.

```mermaid
flowchart LR
    subgraph Clause["Clause: joker: Blueprint"]
        S[sources]
    end

    S --> Shop["Shop Slots<br/>shopItems: [0,1,2]<br/>minShopSlot/maxShopSlot"]
    S --> Pack["Pack Slots<br/>boosterPacks: [0,1,2,3]<br/>minPackSlot/maxPackSlot<br/>requireMega"]
    S --> Tag["Tags<br/>rareTag: [0]<br/>uncommonTag: [0]<br/>tags: true"]
    S --> Special["Special Sources<br/>judgement: [0,1]<br/>riffRaff: [0,1]<br/>emperor: [0,1]<br/>sixthSense: [0]<br/>seance: [0,1]<br/>purpleSealOrEightBall: [0]"]
    S --> Stream["Stream Pre-filters<br/>uncommonShopJokers: [0,1,2]<br/>rareShopJokers: [0]<br/>commonShopJokers: [0,1]"]

    Shop --> Engine[Search Engine]
    Pack --> Engine
    Tag --> Engine
    Special --> Engine
    Stream --> Engine
```

## 7. Must / Should / MustNot Scoring Logic

How the three clause buckets combine to score a seed.

```mermaid
flowchart TB
    Seed[Seed: ABC123] --> Must{All must<br/>clauses pass?}
    Must -->|No| Reject[Seed REJECTED]
    Must -->|Yes| MustNot{Any mustNot<br/>clause matches?}
    MustNot -->|Yes| Reject
    MustNot -->|No| Score[Calculate Score]
    
    Score --> S1[should clause 1<br/>score: 67]
    Score --> S2[should clause 2<br/>score: 38]
    Score --> S3[should clause 3<br/>score: 25]
    
    S1 -->|matched| Add1[+67]
    S2 -->|matched| Add2[+38]
    S3 -->|not matched| Add3[+0]
    
    Add1 --> Total[Total: 105]
    Add2 --> Total
    Add3 --> Total

    Total --> Rank[Seed passes with score 105<br/>Higher = better seed]
```

## 8. Composable Dependency Graph

How the Vue composables wire together.

```mermaid
flowchart TB
    JamlUI[JamlUI.vue] --> usePanelState
    JamlUI --> useFilters
    JamlUI --> useSearch
    JamlUI --> useSignalR
    JamlUI --> useGlobalError
    JamlUI --> useLayout
    JamlUI --> useResize
    JamlUI --> useToasts
    JamlUI --> useSound

    useFilters --> useApi
    useSearch --> useApi
    useApi --> useRequests

    useSignalR --> |"onResult"| JamlUI
    useSignalR --> |"onProgress"| JamlUI

    EP[EditorPanel] --> useMonaco
    useMonaco --> |"fetch schema"| Schema[jaml.schema.json]

    CP[ChatPanel] --> useChat
    useChat --> useSignalR
```

## 9. Integration Points: API vs WASM vs Standalone

Three deployment modes and what each supports.

```mermaid
flowchart TB
    subgraph Full["Full Stack (Motely.API)"]
        direction LR
        UI1[Vue UI] <-->|REST + SignalR| API[.NET API Server]
        API --> DB[(Filter Storage)]
        API --> SE1[Search Engine<br/>SIMD optimized]
    end

    subgraph Browser["Browser WASM (Motely.BrowserWasm)"]
        direction LR
        UI2[Vue UI] <-->|JS Interop| WASM[.NET WASM Module]
        WASM --> SE2[MotelySearchSync<br/>In-browser search]
    end

    subgraph Static["Static / Standalone"]
        direction LR
        UI3[Vue UI] --> LS[(localStorage)]
        UI3 --> |"No search"| Manual[Manual seed entry]
        UI3 --> |"iframe"| BP[Blueprint analyzer]
    end
```

## 10. What's Working vs What Needs Work

Current state assessment.

```mermaid
quadrantChart
    title Feature Completeness vs Priority
    x-axis Low Priority --> High Priority
    y-axis Incomplete --> Complete
    
    Monaco Editor: [0.8, 0.9]
    JamlBuilder: [0.7, 0.8]
    SoftVisualEditor: [0.4, 0.6]
    Panel System: [0.5, 0.85]
    Blueprint Embed: [0.3, 0.7]
    SignalR Connection: [0.9, 0.65]
    Search Start/Stop: [0.95, 0.7]
    Results Table: [0.85, 0.75]
    JAML Validation: [0.8, 0.5]
    WASM Integration: [0.9, 0.2]
    Chat System: [0.2, 0.6]
    Sound Effects: [0.1, 0.9]
```

### Known Bugs to Fix

| File | Issue | Severity |
|------|-------|----------|
| `useApi.js` | `updateRequest(0, ...)` hardcodes index 0 instead of using `requestIndex` | Medium |
| `usePanelState.ts` | `isBasePanel` always returns false (ID format mismatch) | Medium |
| `useSignalR.js` | Hardcoded dev IP `192.168.0.171:3141` | High |
| `useSignalR.js` | `disconnect()` is a no-op | Low |
| `useRequests.js` | `updateRequest(index)` receives wrong type from `addRequest` | Low |

### Missing for Standalone JAML UI

1. **WASM bridge**: `Motely.BrowserWasm` exists but isn't wired to the Vue UI yet
2. **Offline filter storage**: Currently requires API; needs localStorage fallback
3. **Schema validation in builder**: JamlBuilder doesn't validate against schema in real-time
4. **Export/share**: No way to share a `.jaml` file URL or download
