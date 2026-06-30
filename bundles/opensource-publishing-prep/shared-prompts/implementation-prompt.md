# Implementation Prompt

You are implementing one subbundle from `bundle://README.md` for `CanDoItAll.IPFS`.

Read the subbundle README completely before editing. Then read every source reference listed under `## Exact Source References`.

Constraints:

- Do not implement work from other subbundles unless the current subbundle explicitly requires it.
- Preserve the large-screen desktop UI target; do not spend implementation time on small or medium responsive tuning.
- Update `bundle://reviews/01-execution-report.md` with entry gate, closure gate, downstream dependency check, and proof notes.
- For critical subbundles, create `bundle://proof/SBxx/manifest.md` and include commands, artifacts, changed-file hashes, and portable `repo://` or `bundle://` references.
- Run focused tests for touched areas and explain any skipped validation.
- Treat pre-existing warnings as baseline unless your changes increase or transform them.

Subbundle-specific instruction:

- Follow the `## Implementation Steps`, `## Do Not Do`, `## Acceptance Checklist`, and `## Proof Required` sections exactly.
