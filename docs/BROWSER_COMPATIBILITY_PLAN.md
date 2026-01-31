# Browser compatibility plan: unify search, no `#if BROWSER`

**Goal:** Make search and analysis work in browser builds by removing or abstracting desktop-only code. No preprocessor conditionals (`#if BROWSER`). Prefer interfaces so the same logic can run on desktop and browser with different implementations.

**Principle:** If something is not browser-compatible, either remove it or swap it for an interface that can be abstracted (desktop impl vs browser impl). Follow the plan step by step.

---

## Phase 1: Audit (no code changes)

**Objective:** List every API/type used in the search path that might be browser-incompatible or that differs between desktop and browser today. Categorize each item.

### 1.1 Scope

- **MotelySearch.cs** (desktop-only today; excluded from browser build)
- **MotelySearch.Browser.cs** (browser stub: interfaces + `MotelySearchSettings.Start()` throws)
- **Motely.Orchestration** (JsonSearchExecutor, MotelySearchOrchestrator) — used by both; calls `MotelySearchSettings.Start()`
- **MotelySeedAnalyzer** — calls `MotelySearchSettings.Start()`; used by WASM `AnalyzeSeed`

### 1.2 Audit table (to fill in Phase 1)

| Location | API / type | Purpose | Category |
|----------|------------|---------|----------|
| MotelySearch.cs | `Thread` | Worker threads, Join, Yield | **Abstract** |
| MotelySearch.cs | `Barrier` | Pause/unpause coordination | **Abstract** |
| MotelySearch.cs | `Marshal.AllocHGlobal` / `FreeHGlobal` | Unmanaged buffers (hashes, seed matrix) | **Abstract or replace** |
| MotelySearch.cs | `Console` / `FancyConsole` | Progress, CSV header, seed output | **Already abstracted** (FancyConsole has .Browser.cs) |
| MotelySearch.cs | `Environment.ProcessorCount` | Default thread count | **Keep** (works in browser) |
| MotelySearch.cs | `Interlocked`, `Stopwatch`, `CancellationToken` | Counters, timing, cancel | **Keep** (standard .NET) |
| MotelySearch.Browser.cs | `MotelySearchSettings.Start()` | Throws | **Replace** with real implementation |

**Categories:**

- **Keep:** Safe on both platforms; no change.
- **Already abstracted:** Handled by existing platform-specific code (e.g. FancyConsole).
- **Abstract:** Introduce an interface + desktop impl + browser impl; search code depends only on the interface.
- **Remove:** Not needed in browser; remove or make optional.
- **Replace:** Remove stub; provide real browser-capable implementation.

### 1.3 Deliverable for Phase 1

- Completed audit table (all usages in MotelySearch + thread classes listed and categorized).
- Decision per item: keep / already abstracted / abstract / remove / replace.
- No code changes yet.

---

## Phase 2: Choose strategy

**Objective:** Pick one approach and document it so implementation is consistent.

### Option A: Single implementation with injected abstractions

- **Idea:** Keep one `MotelySearch.cs` (and one `MotelySearchSettings`) compiled for **both** desktop and browser.
- **Mechanism:** Introduce interfaces for the incompatible bits (e.g. `ISearchConcurrency`, `IUnmanagedAllocator`). `MotelySearch` (or a factory) takes these via constructor or a small, settable service locator. Desktop app registers desktop implementations; browser app registers browser implementations.
- **Pros:** One code path; no duplicated search logic.
- **Cons:** Requires refactoring `MotelySearch` to use abstractions and wiring at app startup.

### Option B: Two implementations, one interface

- **Idea:** Desktop keeps current `MotelySearch` (threads, barriers, Marshal). Browser gets a **separate** class that implements `IMotelySearch` and runs the same logical search (same filters, same `MotelyVectorSearchContext` / `MotelySearchContextParams`) but single-threaded and with managed or abstracted allocation.
- **Mechanism:** `MotelySearchSettings.Start()` in the browser build returns an instance of this browser runner instead of throwing. No `#if`: browser build compiles a different **file** (e.g. `MotelySearch.Browser.cs`) that contains the real browser runner and a `MotelySearchSettings` whose `Start()` constructs and returns it.
- **Pros:** Minimal change to desktop; browser gets a tailored implementation (e.g. single-threaded, no Barrier).
- **Cons:** Duplicated “run batches and call filter” logic unless shared in a common helper used by both.

### Option C: Hybrid

- **Idea:** Abstract only the bare minimum (e.g. “run N units of work”) and “allocate unmanaged block”) so that one `MotelySearch`-like core compiles for both, but the “runner” (threads vs single-thread loop) and “allocator” (Marshal vs managed/pinned) are pluggable.
- **Mechanism:** Define e.g. `ISearchRunner` and `IUnmanagedAllocator`. Desktop runner = current thread + barrier implementation; browser runner = single-threaded loop. Same batch/filter logic in one place.

**Deliverable for Phase 2:** Written decision: Option A, B, or C, plus a short “how we will implement it” (which interfaces, which files, how `Start()` is wired).

---

## Phase 3: Interfaces and contracts

**Objective:** Define the abstractions (if Option A or C) or the browser runner contract (if Option B) so that desktop and browser can be implemented against the same contract.

### 3.1 If abstracting concurrency

- **Interface name and assembly:** e.g. `IMotelySearchConcurrency` in Motely.
- **Methods:** e.g. “CreateWorker(Action run)” returning something that can “Start” and “Join” (or “RunAsync”), and “CreateBarrier(int participantCount)” if barriers are still needed in a single implementation. Or a single “RunParallel(IReadOnlyList<Action> workItems, CancellationToken ct)” to hide threads vs single-thread.
- **Desktop impl:** Uses `Thread` and `Barrier`.
- **Browser impl:** Uses a single-threaded loop (or `Task.Run` / threading if WASM supports it and we decide to use it).

### 3.2 If abstracting allocation

- **Interface name:** e.g. `IMotelyUnmanagedAllocator`.
- **Methods:** Alloc(nbytes), Free(ptr). Return type can be `nint` or `void*` depending on coding style.
- **Desktop impl:** `Marshal.AllocHGlobal` / `FreeHGlobal`.
- **Browser impl:** Either same (if supported in WASM) or managed array + `GCHandle.Alloc(..., GCHandleType.Pinned)` and pass pointer; allocator tracks handles for free.

### 3.3 If implementing a full browser runner (Option B)

- **Contract:** Class that implements `IMotelySearch`, constructed from `MotelySearchSettings<T>` (or equivalent config). Implements `Start`, `AwaitCompletion`, `Pause`, `Cancel`, and reports progress/counts. Internally: single-threaded loop over seeds/batches, build `MotelySearchContextParams`, create `MotelyVectorSearchContext`, call base filter and additional filters, report results. No `Thread`, no `Barrier`; allocation either via Marshal (if it works) or managed + pin.

**Deliverable for Phase 3:** Interface signatures and where they live; or browser runner contract (public surface and behavior).

---

## Phase 4: Implementation (step-by-step)

**Objective:** Implement in small, verifiable steps. Each step should keep the solution building and tests passing where applicable.

### 4.1 Step 1: Introduce abstractions (if Option A/C)

- Add interface(s) to Motely (e.g. in a new file `MotelySearchAbstractions.cs` or next to existing interfaces).
- Add desktop implementation(s) in Motely (e.g. `DesktopSearchConcurrency`, `DesktopUnmanagedAllocator`).
- **Do not** change `MotelySearch` yet; optionally add a way to “set” or “inject” the default implementation (e.g. static property or small factory) so that existing desktop code keeps working with current behavior.
- Verify: desktop build and existing tests still pass.

### 4.2 Step 2: Refactor MotelySearch to use abstractions (Option A/C)

- Replace direct use of `Thread`, `Barrier`, and `Marshal` in `MotelySearch` (and thread classes) with the new interface(s). Prefer constructor or init-time injection so that tests and desktop app can pass the current desktop impl.
- Verify: desktop build and tests still pass.

### 4.3 Step 3: Browser implementation of abstractions (Option A/C)

- Add browser implementation(s) (e.g. single-thread runner, managed allocator) in a file that is compiled only for the browser target (e.g. in Motely, a file that is **not** excluded for `net10.0-browser`). Do **not** use `#if BROWSER`; use the existing .csproj rule: “this file is only included for browser” by not excluding it for browser and (if needed) excluding it for desktop, or by having a separate project that references Motely and provides the impl.
- In the browser host (e.g. Motely.WASM), at startup, set or register the browser implementation(s) so that when `MotelySearchSettings.Start()` is called, it uses the browser runner/allocator.
- **Unify build:** Remove the exclusion of `MotelySearch.cs` for browser so that the same `MotelySearch` (and `MotelySearchSettings`) is compiled for both. Remove or repurpose `MotelySearch.Browser.cs`: either delete it (if Option A/C and interfaces live in shared code) or replace its content with the browser implementations only (if Option B).
- Verify: browser build compiles; run a minimal test (e.g. analyze one seed, run one small search) in browser.

### 4.4 Step 4: Option B only — browser runner

- Implement the browser runner class that implements `IMotelySearch`, using the same filter/context APIs as the desktop thread logic (single-threaded loop, same `SearchSeeds`-style batch execution). Prefer extracting a shared “run one batch” helper from desktop so logic is not duplicated.
- In `MotelySearch.Browser.cs`, change `MotelySearchSettings.Start()` to instantiate this runner with the current settings and return it (no throw).
- Verify: browser build compiles; analyzer and orchestrator paths in browser succeed (analyze one seed, run one small search).

### 4.5 Step 5: Cleanup and docs

- Remove any redundant stub code or duplicate interface definitions.
- Update comments and (if any) high-level docs to state: “Search is unified; desktop uses X, browser uses Y” (or “same implementation with different injected services”).
- Optionally add a short “Browser compatibility” section in the main Motely README pointing to this plan and the chosen option.

---

## Phase 5: Verification

- **Desktop:** Full solution build; existing Motely tests pass; manual run of CLI/orchestrator search and analyze.
- **Browser:** Motely.WASM build and run in browser; `AnalyzeSeed` returns correct analysis; `SearchSeeds` (via orchestrator) runs and returns results.
- **No `#if BROWSER`** (or similar) in the codebase for this feature.

---

## Summary checklist

- [ ] Phase 1: Audit complete; table and decisions documented.
- [ ] Phase 2: Strategy chosen (A, B, or C) and documented.
- [ ] Phase 3: Interfaces/contracts defined.
- [ ] Phase 4: Implementation steps 1–5 done in order, with verification after each.
- [ ] Phase 5: Desktop and browser verified; no preprocessor conditionals.

You can stop after Phase 1 (and optionally Phase 2) and review before any code changes. Implementation (Phases 3–5) should follow this plan step by step so we don’t skip or assume anything.
