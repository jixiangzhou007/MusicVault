# Production Search Phase A Implementation Plan

> **For agentic workers:** Implement task-by-task. Steps use checkbox syntax.

**Goal:** Harden multi-source search with per-source timeouts, structured partial-failure results, UI warning, and correct health marking.

**Architecture:** Extract `SearchSourceExecution.RunSafeAsync` used by `MusicSearchAggregator`; extend `SearchAggregationResult`; wire Discover banner + health in `SearchPageViewModel`.

**Tech Stack:** .NET 8, existing ServiceMessageCodec / Avalonia alerts

---

### Task 1: SearchSourceExecution + result model
### Task 2: Wire aggregator (both search methods), remove duplicate Ensure
### Task 3: Discover UI banner + health logic
### Task 4: Unit tests + publish
