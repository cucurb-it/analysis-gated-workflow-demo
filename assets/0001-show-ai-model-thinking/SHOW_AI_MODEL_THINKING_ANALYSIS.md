# Show AI Model Thinking Analysis

**Author:** *(Architect)*
**Date:** 2026-03-09
**Context:** TUI application that accepts user prompts and displays AI model responses. The application currently supports prompt entry and response rendering but does not surface the internal reasoning (thinking) output produced by thinking-capable models.

---

## Workflow State

| Field | Value |
|---|---|
| Current Phase | DEEP CODE ANALYSIS PHASE |
| Phase Status | IN PROGRESS |
| Last Updated | 2026-03-09 |
| Pending Architect Action | none |

---

## Executive Summary
*(populated at end of Phase 02)*

---

## Feature or Refactoring Description

### 3.1 — Request Summary

The feature requires the application to detect when a thinking-capable AI model (e.g. Qwen 3.5) produces a reasoning/thinking stream, stream that thinking output to the user in real time as it is generated, and then render the final model response separately — displayed after the thinking output has completed.

The thinking output is not the final answer; it is the model's intermediate reasoning process. Both the thinking stream and the final response must be visible to the user, in that order, in the UI.

### 3.2 — Domain Context

**Domain:** AI chat / streaming response rendering in a terminal UI (TUI).

**Capability served:** Progress feedback and transparency during AI model inference. Thinking-capable models (extended reasoning models) produce two distinct output streams: a reasoning/thinking stream and a final response stream. Currently only the final response is surfaced. This feature closes that gap.

**Problem solved:** Users experience a silent wait period while the model reasons. For long-running thinking passes, there is no indication of progress. Surfacing the thinking stream provides real-time feedback and transparency into the model's reasoning process.

### 3.3 — Scope

**In scope:**
- Streaming and rendering the thinking/reasoning output from thinking-capable models in real time
- Rendering the final response after the thinking output has completed
- Visual separation between the thinking output and the final response in the UI
- Handling the case where a model produces a thinking stream (thinking-capable) vs. a model that does not (standard model — no thinking section rendered)

**Out of scope:**
- Changes to how the user enters prompts
- Changes to model selection or configuration
- Persisting or exporting the thinking output
- Any backend or API changes beyond what is required to consume thinking stream data

**Unclear / requires clarification:**
- See Open Questions (§3.5)

### 3.4 — Key Concepts & Terminology

| Term | Definition (as used by the Architect) |
|---|---|
| **Thinking model** | An AI model capable of producing a separate reasoning/thinking output before its final response (e.g. Qwen 3.5) |
| **Thinking process** | The intermediate reasoning stream produced by a thinking model during inference — distinct from the final answer |
| **Streaming** | Real-time, incremental delivery of model output tokens to the UI as they are generated |
| **Final response** | The model's answer to the user's prompt — rendered after the thinking process has completed |
| **UI** | The terminal user interface (TUI) that accepts prompts and renders model output |
| **Rendered after** | The final response is displayed below / after the thinking text in the UI, not interleaved |

### 3.5 — Open Questions

1. **Visual treatment of thinking output** — Should the thinking text be visually distinguished from the final response (e.g. a different colour, a collapsible block, a label such as "Thinking…", a dimmed style)? Or is plain streaming text acceptable?
2. **Thinking output persistence** — Once the final response is rendered, should the thinking text remain visible, be collapsed, or be dismissed?
3. **Model detection** — Is thinking-capability determined statically (by model name/configuration) or dynamically (by the presence of a thinking stream in the response)?
4. **Streaming API** — Which API or SDK is used to communicate with the AI model? Does the thinking stream arrive as a distinct event type, a separate field, or interleaved with the response tokens?
5. **Concurrent thinking + response streaming** — Can thinking and final response tokens arrive concurrently, or is thinking always fully complete before final response tokens begin?

---

## Deep Code Analysis
*(populated in Phase 02)*

---

## Project Conventions Confirmation
*(populated in Phase 02 — mandatory)*

---

## Architecture Decision Records
*(populated by Architect — never by AI)*

---

## Implementation Plan
*(populated in Phase 04)*

---

## Implementation Summary
*(populated in Phase 06)*

---

## Prompt Log

| # | Date & Time | Phase | Prompt |
|---|-------------|-------|--------|
| 1 | 2026-03-09 | DEEP FEATURE ANALYSIS PHASE | Show AI model thinking,  /Users/peter/Projects/cucurb-it/analysis-gated-workflow-demo/assets/0001-show-ai-model-thinking |
| 2 | 2026-03-09 | DEEP FEATURE ANALYSIS PHASE | At this moment, the UI allow to enter a new prompt. However, when using for instance qwen 3.5, which is a thinking model, the thinking process should be streamed and shown so the user sees some progress while waiting for the answer generated by the AI model. The final response to the user's prompt should be rendered after the 'thinking' text. |
| 3 | 2026-03-09 | DEEP FEATURE ANALYSIS PHASE | Show AI model thinking,  /Users/peter/Projects/cucurb-it/analysis-gated-workflow-demo/assets/0001-show-ai-model-thinking |
| 4 | 2026-03-09 | DEEP FEATURE ANALYSIS PHASE | proceed to the deep code analysis phase |

---
