# DevAcademy — AI Providers

Optional, bring-your-own-key. AI is off until the learner configures a provider. AI never marks an exercise as
solved, never runs commands and never writes files; deterministic evaluation is the only thing that passes a
lesson. Source: `src/DevAcademy.AI`, orchestration in `src/DevAcademy.Application/Tutor`.

**Verification status.** Everything here is tested against **fake HTTP handlers only** (`tests/DevAcademy.AI.Tests`).
No live provider API call has ever been made by the test suite, so wire formats follow the providers'
documentation (links are in the source headers) and real-world behaviour is unconfirmed. Model-list
pagination, error classification, SSE parsing and key scrubbing are covered by those fakes.

## 1. Providers

All providers implement `IModelProvider`: `GetAvailableModelsAsync` (follows pagination to the end, max 100 pages),
`ValidateCredentialsAsync` (invalid key → `Valid=false`; other failures throw), `TestModelAsync` (a real request of
~16 output tokens), `CreateCompletionAsync`, `StreamCompletionAsync` (SSE). Production `HttpClient`:
`AllowAutoRedirect=false` (credentials never follow a redirect), 60 s timeout, pooled connections.

| Provider id | Base URL | Auth header | Model list & pagination | Chat endpoint | Credential check |
|---|---|---|---|---|---|
| `openai` | `https://api.openai.com/v1/` | `Authorization: Bearer <key>` | `GET models` (no pagination; follows a `next` link only where a subclass defines one) | `POST chat/completions` (Chat Completions, chosen because the same wire format serves OpenRouter/Ollama/LM Studio); `max_completion_tokens`; `stream_options.include_usage` for usage | model list call |
| `anthropic` | `https://api.anthropic.com/v1/` | `x-api-key: <key>` + `anthropic-version: 2023-06-01` | `GET models?limit=1000[&after_id=<last_id>]` while `has_more` | `POST messages` (system text is lifted to the top-level `system` field; `max_tokens` always sent) | model list call |
| `gemini` | `https://generativelanguage.googleapis.com/v1beta/` | `x-goog-api-key: <key>` (never in the query string) | `GET models?pageSize=1000[&pageToken=…]` | `POST models/{id}:generateContent`, streaming `:streamGenerateContent?alt=sse` (`systemInstruction` for system text) | model list call |
| `openrouter` | `https://openrouter.ai/api/v1/` | `Authorization: Bearer <key>` | `GET models`; follows `links.next` only if it stays on the same scheme/host/port | OpenAI-compatible chat completions | `GET key` |
| `openai-compatible` | user-supplied base URL (e.g. `http://localhost:11434/v1` for Ollama) | `Bearer` if a key is configured; key optional (`RequiresApiKey=false` by default); `max_tokens` instead of `max_completion_tokens` | `GET models` | `POST chat/completions` | model list call |

Models are discovered dynamically; there is no built-in whitelist and no hard-coded model list. A **manual Model ID**
is a first-class input: any string is sent as-is, and `TestModelAsync` verifies it. A discovered `ModelInfo`
(`KnownModel`) is used only to avoid sending parameters the API explicitly says the model lacks (e.g.
`temperature`); OpenAI `o*` and `gpt-5*` ids additionally have `temperature` suppressed (documented behaviour).

## 2. Capabilities and pricing: never guessed

`ModelCapabilities` has six members (TextGeneration, Streaming, ToolCalling, StructuredOutput, MultimodalInput,
ReasoningControls), each `Supported | NotSupported | Unknown`. **Unknown is the default** and is used whenever the API
does not say. OpenAI and Anthropic model lists carry no capability data → all Unknown. Gemini reports
`supportedGenerationMethods` (streaming absent ⇒ Unknown, not NotSupported). OpenRouter reports
`supported_parameters`, `architecture.input/output_modalities`, `context_length`, `top_provider.max_completion_tokens`
and `pricing` (USD per token; `-1` for dynamically priced routers is treated as unknown).

`CostEstimator` multiplies tokens by published per-token prices and returns null when prices or token counts are
missing. Costs are labelled approximate in the UI; provider billing is authoritative.

## 3. Errors

`AiProviderException` with `AiErrorKind`: InvalidKey, PermissionDenied, ModelNotFound, RateLimited (429), QuotaExceeded
(OpenAI `insufficient_quota`, OpenRouter 402), ProviderUnavailable, BadRequest, Network, Timeout (408/504 or local
timeout). Error text is passed through `Scrub`, which removes the API key from messages. **There is no automatic
retry and no automatic fallback to another model or provider** — a failure is reported and the learner decides.

## 4. Secrets

`ISecretStore` (`IsAvailable`, `UnavailableReason`, `Get/Set/Delete(providerId)`), implemented in
`DevAcademy.Infrastructure/Secrets`:

| OS | Store | Status |
|---|---|---|
| macOS | Keychain generic password, service `DevAcademy`, account = provider id (Security.framework P/Invoke) | verified |
| Windows | Credential Manager generic credential, target `DevAcademy/{providerId}` (`CredReadW`/`CredWriteW`) | compiled, **unverified** |
| Linux | Secret Service via `libsecret` (`secret_password_*v_sync`) | compiled, **unverified**; needs libsecret and a keyring daemon |

If no store is available, keys cannot be saved — there is **no plaintext fallback**. Keys never go to SQLite, JSON
exports, logs or child-process environments, and are sent only to the provider's own base URL.

## 5. What leaves the machine (Tutor)

`TutorService` builds the exact `ChatMessage` list for a scenario (Explain, Debug, Hint, CodeReview,
AlgorithmExplanation, LanguageComparison, ProjectMentor, InterviewSimulator, LearningAdaptation) and exposes it as a
**payload preview** (`TutorPayload.Messages`, included sections, approximate input size) before sending — the preview
works even when AI is disabled. The learner controls which context is included (`IncludeLesson`, `IncludeCode`,
`IncludeResults`, `IncludePreviousAttempts`) and a global allow-code switch. Hidden tests are reduced to pass/fail
before they reach the prompt. Hints are available without AI from `hints:` in `lesson.yaml`.

**Request log** (`ai_requests` table): provider, model, scenario, start time, input/output tokens, approximate cost,
status — **metadata only, no prompt or response text**. "Delete AI history" removes all rows.

## 6. Planned

Tool calling / structured output use by the tutor (capabilities are discovered but not used), per-provider rate
limiting, ML-context tutoring (dataset metadata, metrics, training config — never a replacement for deterministic
checks), live-API contract tests (opt-in, with the learner's own key).
