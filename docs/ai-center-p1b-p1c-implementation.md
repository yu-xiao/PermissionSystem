# AI Center P1-B / P1-C Implementation

## Scope

- Versioned read-only agent: `permission-readonly-agent` version `1.0`.
- Conversation, message, run, model usage, tool invocation, and citation audit flows.
- OpenAI-compatible tool calling with explicit model-function-to-tool-code mapping.
- Six read-only permission tools; the report dataset tool remains disabled by default.
- Per-user SignalR run events through `/hubs/ai`.
- MCP exposure for the five default-safe read-only tools.
- Header AI chat dialog and AI Provider management page.

## Runtime limits

- User message: 4,000 characters.
- Model rounds: 6 per run.
- Tool calls: 10 per run.
- Total run time: 90 seconds.
- Provider response body: 1 MiB.
- Tool rows: 200 maximum.

## Access and safety gates

- `Ai:Enabled` must be enabled.
- The current tenant must be listed in `Ai:AllowedTenantIds`.
- The caller must have `ai:chat:use` or the relevant management permission.
- Every tool requires the AI tool permission and its original business permission.
- A default enabled Provider must have an explicit compliance confirmation before any model call.
- Provider endpoints retain HTTPS, host allowlist, DNS/IP, private-network, and response-size checks.
- Provider error bodies, API keys, raw sensitive logs, and personal contact fields are not returned.

## Retention

- After the configured conversation retention period (30 days by default), message content, content digest, token count, and derived conversation titles are irreversibly sanitized.
- After the configured audit retention period (180 days by default), terminal Run, Tool invocation, Usage, unreferenced Message, and empty Conversation rows are hard deleted in foreign-key-safe order.
- Cleanup runs once after startup delay and then every 24 hours.

## API

- `/api/ai/providers`
- `/api/ai/conversations`
- `/api/ai/conversations/{id}/messages`
- `/api/ai/runs/{id}`
- `/api/ai/runs/{id}/cancel`
- `/api/ai/runs/{id}/citations`

Feedback is not implemented in P1 because the confirmed six-table schema has no feedback persistence contract. Adding it requires a separate schema decision.

## Deployment

Apply both AI migrations before enabling AI:

- `20260828041655_AddAiCenterP1`
- `20260828050053_AddAiProviderComplianceGate`

Keep `Ai:Enabled=false` until the tenant allowlist, Provider network policy, encryption key, data residency, and compliance confirmation have been reviewed.
