# Show AI Model Thinking Analysis

**Author:** *(Architect)*
**Date:** 2026-03-09
**Context:** TUI application that accepts user prompts and displays AI model responses. The application currently supports prompt entry and response rendering but does not surface the internal reasoning (thinking) output produced by thinking-capable models.

---

## Workflow State

| Field | Value |
|---|---|
| Current Phase | COMPLIANCE & REVIEW PHASE |
| Phase Status | AWAITING ARCHITECT REVIEW |
| Last Updated | 2026-03-09 |
| Pending Architect Action | 5 placeholder ADRs require Architect decisions before advancing to Phase 04 |

---

## Executive Summary

The application is a minimal, single-component .NET 10 TUI chat interface built on RazorConsole and backed by the Microsoft.Extensions.AI (MEAI) abstraction layer. It currently communicates with Ollama (model: `qwen3.5:9b`) using a **single blocking `CompleteAsync()` call** — no streaming is implemented anywhere in the stack.

The dominant issue is a **two-layer interface contract constraint**:

1. `IChatService.SendMessageAsync()` returns `Task<string>` — a fully resolved string. This interface must change to support streaming or structured thinking output.
2. `App.razor` receives and renders a single string with no knowledge of whether that string contains thinking content, final response content, or both.

The root cause of the current gap is that the application was scaffolded with non-streaming, non-thinking-aware primitives. The MEAI `IChatClient` abstraction does expose `CompleteStreamingAsync()` returning `IAsyncEnumerable<StreamingChatCompletionUpdate>`, but this has not been used. Whether the MEAI Ollama provider surfaces thinking tokens as distinct `StreamingChatCompletionUpdate` events — or merges them into the standard text stream — was under active investigation by the Architect (evidenced by package decompilation artefacts in `.claude/settings.local.json`).

The key insight shaping implementation: **the service interface is the architectural seam**. Any streaming or thinking-aware implementation must pass through a redesigned `IChatService` contract, and the UI component must be extended to hold and render two distinct content streams (thinking + response) with visual separation.

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

### 3.1 — Architecture Overview

**Position in the system:**

The application is a single-layer, single-project .NET 10 console application. There are no domain layers, no data layers, and no microservices. The architecture is flat and direct:

```
Program.cs
  └── Host (Microsoft.Extensions.Hosting)
        └── RazorConsole TUI runtime
              └── App.razor  (sole UI component)
                    └── IChatService / ChatService  (sole service)
                          └── IChatClient  (MEAI abstraction)
                                ├── OllamaChatClient → http://localhost:11434  (default)
                                └── OpenAIClient → gpt-4o-mini  (if OPENAI_API_KEY set)
```

**Project:** `LLMAgentTUI` (.NET 10.0, SDK: `Microsoft.NET.Sdk.Razor`)
**Solution file:** `src/OllamaTui.sln`
**Main project folder:** `src/LLMAgentTUI./` *(note: trailing period in folder name — intentional)*

**Dependencies (NuGet):**

| Package | Version | Role |
|---|---|---|
| `Microsoft.Extensions.AI` | 9.1.0-preview.1.25064.3 | AI client abstraction (MEAI) |
| `Microsoft.Extensions.AI.Ollama` | 9.1.0-preview.1.25064.3 | Ollama provider for MEAI |
| `Microsoft.Extensions.AI.OpenAI` | 9.1.0-preview.1.25064.3 | OpenAI provider for MEAI |
| `Microsoft.Extensions.Hosting` | 8.0.0 | Generic Host / DI / lifecycle |
| `RazorConsole.Core` | 0.3.0 | Razor-component-based TUI framework |
| `Spectre.Console` | 0.54.0 | Terminal styling, Markup, Spinner, layout |

**Dependents:** None — this is a leaf application, not a library.

**Lifecycle:**
1. `Program.cs` configures and builds the host
2. `host.RunAsync()` starts the RazorConsole runtime
3. `App.razor` is the root component — instantiated by the runtime
4. `IChatService`/`ChatService` is a singleton, injected into `App.razor`
5. Application runs until `Ctrl+C` is received

---

### 3.2 — Behavioural Analysis

**App.razor — Primary execution flow:**

```
User types text into <TextInput>
  └── TextInput binds to _currentInput (two-way)
        └── User presses Enter → OnSubmit fires → SendMessage() called

SendMessage():
  1. Guard: if _currentInput is whitespace → return
  2. Capture userMessage = _currentInput.Trim()
  3. Clear _currentInput = string.Empty
  4. Append ChatMessage { Content = userMessage, IsUser = true } to _messages
  5. StateHasChanged()           ← renders user message immediately
  6. _isProcessing = true
  7. StateHasChanged()           ← renders spinner ("AI is thinking...")
  8. BLOCKING: await ChatService.SendMessageAsync(userMessage)
  9. On success: append ChatMessage { Content = response, IsUser = false } to _messages
  10. On exception: append ChatMessage { Content = "[red]Error: ...[/]", IsUser = false }
  11. _isProcessing = false
  12. StateHasChanged()           ← renders AI response, removes spinner
```

**ChatService.SendMessageAsync() — Execution flow:**

```
1. Append ChatMessage(ChatRole.User, message) to _conversationHistory
2. BLOCKING: await _chatClient.CompleteAsync(_conversationHistory)
   └── Single blocking call — waits for full model response
3. Extract response.Message.Text ?? "No response from the AI."
4. Append ChatMessage(ChatRole.Assistant, assistantMessage) to _conversationHistory
5. Return assistantMessage (plain string)
```

**Rendering logic (App.razor template):**

```
Figlet "ChatBot"
  └── if _messages.Count == 0:
        Markup "No messages yet..."
      else:
        foreach message in _messages:
          Padder
            Markup "{You|Bot}" (green if user, blue if bot)
            Markup " "
            Markdown @message.Content   ← markdown-rendered content
  └── if _isProcessing:
        Columns
          Spinner (Dots)
          Markup "AI is thinking..." (grey, italic)

TextInput bound to _currentInput, OnSubmit=SendMessage
Align/Markup (footer instructions)
```

**State that is read vs. mutated:**

| Variable | Type | Read | Mutated | Where |
|---|---|---|---|---|
| `_messages` | `List<ChatMessage>` | Render loop | Append only | `SendMessage()` |
| `_currentInput` | `string` | TextInput bind, SendMessage | Clear after send | `SendMessage()` |
| `_isProcessing` | `bool` | Spinner condition | Toggle around API call | `SendMessage()` |
| `_conversationHistory` | `List<Microsoft.Extensions.AI.ChatMessage>` | `CompleteAsync` input | Append only | `ChatService` |

**Side effects:**
- Terminal output via RazorConsole/Spectre.Console
- HTTP request to Ollama at `http://localhost:11434` (or OpenAI API)
- No file I/O, no database, no logging

---

### 3.3 — Data Model

**App.razor inner class `ChatMessage`:**

```csharp
public class ChatMessage
{
    public required string Content { get; set; }
    public bool IsUser { get; set; }
}
```

| Property | Type | Meaning |
|---|---|---|
| `Content` | `string` | Full message text (plain text or Spectre markup) |
| `IsUser` | `bool` | `true` = user message; `false` = AI message |

*Note: This `ChatMessage` is the UI model — distinct from `Microsoft.Extensions.AI.ChatMessage` used in `ChatService`.*

**Microsoft.Extensions.AI.ChatMessage (service-level):**

| Property | Type | Meaning |
|---|---|---|
| `ChatRole` | `ChatRole` | `User` or `Assistant` |
| `Text` | `string?` | Text content of the message |

**Response cardinalities:**
- `_messages`: grows unbounded per session (one entry per user turn + one per AI turn)
- `_conversationHistory`: mirrors _messages at service level, grows unbounded
- Typical session: 2–20 message pairs

**Key type gap — no thinking model:**
There is no data structure representing a thinking/reasoning block. The `ChatMessage` (UI model) has a single `Content` field, which merges all text into one string. There is no `ThinkingContent` or `IsThinking` property.

---

### 3.4 — Coding Patterns & Style

**Patterns observed:**

| Pattern | Where | Notes |
|---|---|---|
| Dependency Injection | `Program.cs`, `App.razor` | Standard .NET DI via `Microsoft.Extensions.Hosting`; `IChatClient` registered as singleton |
| Repository / Service layer | `ChatService` / `IChatService` | Thin service wrapping MEAI; single method |
| MVVM-adjacent | `App.razor` | State (`_messages`, `_isProcessing`) drives rendering; `StateHasChanged()` manually triggered |
| Provider strategy | `Program.cs` | `useOllama` flag switches between `OllamaChatClient` and `OpenAIClient` at startup |
| Null coalescing fallback | `ChatService.cs:17` | `response.Message.Text ?? "No response from the AI."` |

**Naming conventions:**
- Private fields: `_camelCase` (e.g. `_messages`, `_isProcessing`, `_chatClient`)
- Public properties: `PascalCase` (e.g. `Content`, `IsUser`)
- Methods: `PascalCase` async (e.g. `SendMessageAsync`, `SendMessage`)
- Interfaces: `I` prefix (e.g. `IChatService`, `IChatClient`)
- No abbreviations — names are descriptive and full

**Error handling:**
- `SendMessage()` wraps the API call in `try/catch(Exception ex)` — catches all exceptions
- Error rendered as Spectre red markup: `[red]Error: {ex.Message}[/]`
- `finally` block ensures `_isProcessing` is always reset to `false`
- No retry, no circuit breaker, no structured logging

**Logging / instrumentation:**
- None. No `ILogger`, no structured logging, no metrics.

**Async pattern:**
- `async Task` throughout — no `.Result` or `.Wait()` anti-patterns
- `ConfigureAwait(false)` used in `ChatService.SendMessageAsync` — correct for library/service code
- `StateHasChanged()` called manually after state mutations

**Investigative artefact in `.claude/settings.local.json`:**
The `src/.claude/settings.local.json` contains permissions for inspecting the compiled MEAI Ollama package:
```
"Bash(cp ~/.nuget/packages/microsoft.extensions.ai.ollama/9.1.0-preview.1.25064.3/...)"
"Bash(ilspycmd:*)"
"Bash(unzip:*)"
```
This indicates the Architect previously decompiled the `Microsoft.Extensions.AI.Ollama` package to inspect its internals — specifically to understand how (or whether) the Ollama provider surfaces thinking tokens through the MEAI abstraction. This is a critical signal: the thinking token surface in MEAI is non-obvious and was under investigation.

---

### 3.5 — Performance Characteristics

**Blocking API call:**
`ChatService.SendMessageAsync` calls `_chatClient.CompleteAsync()` which blocks until the entire model response is received. For thinking models like `qwen3.5:9b`, this blocking period includes the full thinking pass before any output is returned. During this wait, the only UI feedback is the static spinner + "AI is thinking..." text — no incremental progress.

**Complexity:**
- `_messages` render loop: O(n) where n = number of messages in session — acceptable for typical chat sessions
- `CompleteAsync`: O(1) call from the application's perspective; actual cost is model inference time (external)
- No O(n²) or worse operations in the application code

**Memory:**
- Conversation history grows unbounded in `_conversationHistory` (full messages retained forever per session)
- No truncation, no windowing, no pruning — long sessions will grow the context sent to the model

**No existing performance commentary in the code.** No TODOs, no FIXMEs, no inline notes about performance.

---

### 3.6 — Relationship to Similar Components

**Single-component architecture** — there are no peer components to compare. `App.razor` is the only UI component and `ChatService` is the only service. There is no equivalent or alternative implementation in the codebase.

**MEAI provider implementations** (Ollama vs. OpenAI) are selected at startup and interchangeable via the `IChatClient` abstraction. Both providers currently receive identical treatment — `CompleteAsync()` with no provider-specific code paths.

**No shared utilities** beyond the NuGet packages.

---

### 3.7 — Template Structure

Not applicable. This application does not operate on document templates.

---

### 3.8 — Known Issues & Code Commentary

No `TODO`, `FIXME`, `HACK`, or `NOTE` comments exist anywhere in the source code.

The copyright header `// Copyright (c) RazorConsole. All rights reserved.` appears on `Program.cs`, `IChatService.cs`, and `ChatService.cs` — this appears to be copied from the RazorConsole sample template and may not reflect actual ownership.

**Critical implementation gap identified through analysis (not a code comment):**
`IChatService` returns `Task<string>` — a single string. This interface contract prevents streaming or structured thinking output from reaching the UI layer without an interface change. The return type is the primary constraint on the feature.

---

## Project Conventions Confirmation

#### `.claude/settings.json` (root)
**Read:** ✓
**CRITICAL/MANDATORY Rules:** N/A — plugin configuration only
**Architecture Boundaries:** N/A
**Technology Constraints:** N/A
**Naming Conventions:** N/A
**Critical Workflows:** N/A
**Hard Constraints:** N/A

#### `.claude/settings.local.json` (root)
**Read:** ✓
**CRITICAL/MANDATORY Rules:** N/A — git permission allowlist only
**Architecture Boundaries:** N/A
**Technology Constraints:** N/A
**Naming Conventions:** N/A
**Critical Workflows:** N/A
**Hard Constraints:** N/A

#### `src/.claude/settings.json`
**Read:** ✓
**CRITICAL/MANDATORY Rules:** N/A — plugin configuration only
**Architecture Boundaries:** N/A
**Technology Constraints:** N/A
**Naming Conventions:** N/A
**Critical Workflows:** N/A
**Hard Constraints:** N/A

#### `src/.claude/settings.local.json`
**Read:** ✓
**CRITICAL/MANDATORY Rules:** N/A — bash permission allowlist; no coding rules
**Architecture Boundaries:** N/A
**Technology Constraints:** Permissions imply `.NET 10 / dotnet CLI` toolchain; `ilspycmd` decompiler; NuGet package inspection
**Naming Conventions:** N/A
**Critical Workflows:** N/A
**Hard Constraints:** N/A

#### `.github/` (root) — No files found
**No `.md` files present in the root `.github/` directory.**

#### `src/.github/` — No files found
**No `.md` files present in the `src/.github/` directory.**

#### `src/LLMAgentTUI./DESIGN.md`
**Read:** ✓
**CRITICAL/MANDATORY Rules:** None marked as critical. This is a design/layout reference document, not a coding standards document.
**Architecture Boundaries:** N/A
**Technology Constraints:** Lists RazorConsole components in use: `Figlet`, `Align`, `Panel`, `Rows`, `Columns`, `Padder`, `Border`, `Markup`, `TextInput`, `TextButton`, `Spinner`. Note: `Panel`, `Border`, `TextButton` appear in DESIGN.md but are **not present** in the current `App.razor` — the implementation diverges from the design document.
**Naming Conventions:** N/A
**Critical Workflows:** N/A
**Hard Constraints:** N/A
**Additional observations:** DESIGN.md describes a two-panel layout (Chat Panel + Input Panel) with distinct panel borders. The actual implementation uses `Padder`/`Rows` without `Panel` components. The design document describes the "AI is thinking…" spinner — this is implemented. The design document does not describe a thinking stream section, confirming this feature is new and undesigned.

**Summary:** No project-specific coding conventions, architectural mandates, or hard constraints exist in any convention file. The project has no CLAUDE.md, no GitHub instructions, and no coding standards documents. The only authoritative design reference is `DESIGN.md`, which is a layout guide and does not constitute a coding standard.

---

## Architecture Decision Records

> **ADR 2026-03-09:**
> Visual treatment of the thinking output — Should the thinking text be visually distinguished from the final response (e.g. a different colour, a collapsible block, a label such as "Thinking…", a dimmed/italic style)? Or is plain streaming text acceptable?
>
> **Status:** PENDING ARCHITECT INPUT
>
> **Context:**
> The UI currently renders all AI output as standard `<Markdown>` content. To distinguish thinking text from the final response, the UI component must apply a different visual style to the thinking block. Without a decision here, the implementation plan cannot specify the rendering approach for `App.razor`.
>
> **Decision:**
> TBD based on Architect input.
>
> **Consequences:**
> - DO: Apply the chosen visual treatment to the thinking block in `App.razor`
> - NOT: Intermix thinking and response text without visual differentiation

---

> **ADR 2026-03-09:**
> Thinking output persistence — Once the final response has been rendered, should the thinking text remain fully visible, be collapsed/hidden, or be dismissed entirely?
>
> **Status:** PENDING ARCHITECT INPUT
>
> **Context:**
> The thinking stream may be lengthy. After the final response is rendered, retaining the full thinking block may crowd the chat history. The `_messages` list in `App.razor` and the `ChatMessage` model both need to know whether to retain a separate thinking block or discard it post-render. This decision affects the data model and the rendering loop.
>
> **Decision:**
> TBD based on Architect input. Likely options: always visible, collapsed by default, or discarded after response is rendered.
>
> **Consequences:**
> - DO: Store thinking content in a dedicated field on the chat message model if it is to be retained
> - NOT: Store thinking content in the same `Content` field as the final response

---

> **ADR 2026-03-09:**
> Model detection — Is thinking-capability determined statically (by model name/configuration at startup in `Program.cs`) or dynamically (by detecting the presence of thinking tokens in the streaming response at runtime)?
>
> **Status:** PENDING ARCHITECT INPUT
>
> **Context:**
> The current model is hardcoded as `qwen3.5:9b` in `Program.cs`. Static detection would gate thinking-stream rendering based on the configured model name. Dynamic detection would activate thinking-stream rendering only when the stream actually contains thinking tokens — regardless of which model is configured. Dynamic detection is more robust and does not require maintaining a list of known thinking models.
>
> **Decision:**
> TBD based on Architect input.
>
> **Consequences:**
> - DO: If dynamic — detect thinking tokens in the stream and activate thinking UI accordingly
> - DO: If static — maintain a known-thinking-model list and check at render time
> - NOT: Mix approaches without a clear decision

---

> **ADR 2026-03-09:**
> MEAI thinking token surface — Does the `Microsoft.Extensions.AI` Ollama provider (`OllamaChatClient`, v9.1.0-preview.1.25064.3) surface thinking tokens as distinct `StreamingChatCompletionUpdate` events when calling `CompleteStreamingAsync()`? If so, what is the event type, property name, or flag that identifies a thinking token vs. a response token?
>
> **Status:** PENDING ARCHITECT INPUT
>
> **Context:**
> This is the most critical open question. Phase 02 analysis identified artefacts in `src/.claude/settings.local.json` (NuGet package decompilation via `ilspycmd`) indicating the Architect was actively investigating this question. Without knowing how — or whether — MEAI surfaces Ollama thinking tokens distinctly, the implementation plan for `ChatService` cannot be written. If MEAI does not surface thinking tokens distinctly (i.e., they arrive merged into the standard text stream), an alternative extraction strategy (e.g. parsing `<think>` tags from the raw stream) must be planned instead.
>
> **Decision:**
> TBD based on Architect's decompilation findings or Ollama API documentation.
>
> **Consequences:**
> - DO: If MEAI surfaces thinking tokens as distinct events — use the MEAI streaming API directly
> - DO: If MEAI merges thinking into the text stream — implement tag-based extraction from the raw content (e.g. strip `<think>…</think>` blocks)
> - NOT: Assume the MEAI abstraction surfaces thinking tokens without verification

---

> **ADR 2026-03-09:**
> Concurrent thinking + response streaming — Can thinking tokens and final response tokens arrive concurrently in the stream, or is the thinking phase always fully complete before final response tokens begin?
>
> **Status:** PENDING ARCHITECT INPUT
>
> **Context:**
> The streaming architecture in `ChatService` and the rendering logic in `App.razor` must handle the sequencing of thinking vs. response tokens. If thinking is always complete before the response begins, the implementation can use a simple two-phase streaming loop (drain thinking stream, then drain response stream). If they can overlap, the implementation requires concurrent stream handling or interleaved token routing — significantly more complex.
>
> **Decision:**
> TBD based on Architect input or Ollama API documentation. For Ollama's implementation of extended reasoning models, thinking is typically completed before the response begins.
>
> **Consequences:**
> - DO: If sequential — implement a two-phase loop: accumulate/render thinking tokens until thinking ends, then accumulate/render response tokens
> - DO: If concurrent — implement concurrent token routing with separate buffers
> - NOT: Design for sequential and discover concurrent at runtime

---

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
| 5 | 2026-03-09 | DEEP CODE ANALYSIS PHASE | proceed to Compliance & Review Phase |

---
