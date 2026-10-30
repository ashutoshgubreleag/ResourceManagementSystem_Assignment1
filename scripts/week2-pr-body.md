## Summary

Refactors the Week 1 TechNova resource-allocation console app into the **ApexPost Counter Platform**,
keeping the layered Controller → Service → Repository architecture (ADR-W2-01).

* **Domain:** branches and counters, staff (4 roles), retail catalogue with per-branch stock,
  shipping with a pricing strategy and 10 zones, money orders, masked identity verification, and a
  mixed basket (Open/Paid/Voided/Refunded). All seven business rules are enforced in services, and
  the four reports are each also available as JSON.
* **C# 14:** value objects, pattern matching, extension members, the `field` keyword, constrained
  generics + specifications, delegates, events, custom attributes with a cached reflection
  validator, async/`CancellationToken` end to end, `Task.WhenAll`, `SemaphoreSlim`, `ValueTask`
  cache, timeout versus cancellation.
* **Pipeline:** Problem Details, correlation ID, request logging and timing, maintenance mode,
  security headers + HSTS, API key (endpoint metadata, constant time, per partner), IP allowlist,
  cookie auth with claims and policies, antiforgery, rate limiting (login + per partner), CORS,
  `/health`.
* **Filters:** audit (append-only JSONL, whitelisted and redacted), validation, performance and
  domain exceptions.
* **Data:** EF Core removed; seed data copied to a git-ignored `App_Data` at runtime.

## Testing

* `dotnet build` with 0 warnings and 0 errors (`TreatWarningsAsErrors`)
* `dotnet test`: 111 passing (unit + WebApplicationFactory integration)

## Docs

* `README.md`: run steps, demo accounts, user-secrets, features, screenshots, sample requests
* `DESIGN.md`: architecture and domain diagrams, middleware order, filters versus middleware,
  11 new ADRs, SOLID mapping, security and audit
* `AUDIT-SAMPLE.json`: real entries produced by the audit filter

🤖 Generated with [Claude Code](https://claude.com/claude-code)

https://claude.ai/code/session_0122KPm2RMziTSmn7xFdWppA
