# Home vinyl tonearm redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the schematic home vinyl tonearm with a matte silver photorealistic transparent PNG and recalibrate pivot/rest angles.

**Architecture:** Keep `VinylRecordCoverView` structure; only swap `Assets/vinyl-tonearm.png` (and optional SVG archive note) and tune `TonearmPivotX/Y`, host Canvas placement, and ±5° angle constants if needed.

**Tech Stack:** Avalonia Image asset, PNG; optional AI image gen; `publish-with-api.ps1 -Restart`

**Spec:** `docs/superpowers/specs/2026-11-10-home-vinyl-tonearm-redesign.md`

**Commits:** Only if the user explicitly asks.

---

## File map

| File | Role |
|------|------|
| Replace `src/MusicVault.App/Assets/vinyl-tonearm.png` | New matte-silver transparent tonearm |
| Optional update `vinyl-tonearm.svg` comment / archive | Source note for attribution |
| Modify `VinylRecordCoverView.axaml` | TonearmHost size/position if needed |
| Modify `VinylRecordCoverView.axaml.cs` | Pivot ratios and angles |

---

### Task 1: Acquire asset (stock then AI)

- [ ] Search/download candidate commercial-OK transparent tonearm PNGs; reject if license unclear or not matte silver
- [ ] If none pass: GenerateImage — isolated turntable tonearm, matte silver, transparent background, pivot screw top-left, headshell bottom-right, portrait ~3:4
- [ ] Verify transparency / crop / soft edges; save as `vinyl-tonearm.png`

### Task 2: Wire + calibrate

- [ ] Drop PNG into Assets; update SVG header comment with source
- [ ] Measure pivot → set `TonearmPivotX/Y`; adjust Canvas Left/Top/Width/Height
- [ ] Smoke pause/play angles (−30° / 0° ±5°)

### Task 3: Publish

- [ ] `.\scripts\publish-with-api.ps1 -Restart`
- [ ] Visual check home vinyl arm
