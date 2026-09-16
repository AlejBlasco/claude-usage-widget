# Skill: Frontend Performance Audit

Load this skill for an ad hoc frontend performance review — distinct
from backend/query performance, which belongs in
`software-architect/blazor-production-architecture.md`'s performance
checklist.

## Review

asset weight, image strategy (format, sizing, lazy loading), font
loading strategy, JavaScript payload, rendering behavior, network
request count, client-side data fetching patterns, caching, unnecessary
dependencies, lazy-loading opportunities.

## Rule

Recommend changes proportional to the product — do not push premature
optimization at the cost of maintainability for a low-traffic internal
tool.
