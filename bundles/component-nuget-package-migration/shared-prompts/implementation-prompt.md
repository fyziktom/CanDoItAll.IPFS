# Implementation Prompt

Use this prompt when executing subbundles:

```text
Implement the current subbundle from bundles/component-nuget-package-migration.
Read README.md, plan/01-phase-plan.md, the current subbundle README, traceability/01-requirement-traceability.md, and reviews/01-execution-report.md first.
For SB01, edit only package source/version/project-reference wiring unless compile proof requires a direct dependency.
For SB02, use Playwright MCP for browser proof, capture before/after screenshots, prove BaseLib output.css is served, and record screenshot-review decisions.
Make the smallest correct change set and stop if a progression gate cannot honestly pass.
```
