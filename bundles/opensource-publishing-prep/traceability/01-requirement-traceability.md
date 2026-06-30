# Requirement Traceability

| Requirement | Source note | Owning subbundle | Evidence | Notes |
| --- | --- | --- | --- | --- |
| R001 Preparation-only bundle | "preparing bundle only" | SB01, SB09 | Prepared validator pass and final proof manifest | Source implementation began only after bundle preparation was complete. |
| R002 Open-source publishing prep | "Preparation for publishing" | SB01, SB02, SB09 | Updated docs/package/license/dependency checklist and `bundle://proof/SB09/manifest.md` | Includes inherited metadata review, packages, vulnerability scan, and final release proof. |
| R003 Review messy parts | "identify all messy parts" | SB01, SB03, SB05, SB06 | Hotspot inventory, workbook checklist, refactor proof | SB05 split `NodeOperatorService`; SB06 split large UI markup/code-behind responsibilities and tracks remaining `Files.razor.cs` state-helper extraction as a follow-up candidate. |
| R004 Isolate NodeControl responsibilities | NodeControl mixing services | SB03, SB05 | `bundle://proof/SB03/manifest.md` and `bundle://proof/SB05/manifest.md` | SB03 extracted contracts; SB05 split NodeOperator workflows behind a compatibility facade. |
| R005 Future non-UI/CLI feasibility | "node without UI" and "nonUI version with CLI" | SB03, SB05, SB09 | UI-independent interfaces and dependency graph proof | Workflow interfaces are UI-neutral; CLI implementation remains future work. |
| R006 Large desktop UI only | "desktop large screen only" | SB06, SB09 | `bundle://proof/SB09/browser-smoke-summary.json` | Final browser smoke uses `1920x1080` and `1600x900`; no small/medium viewport tuning was performed. |
| R007 .NET performance review | Requested performance skill | SB01, SB07, SB09 | `bundle://proof/SB07/performance-triage.md` and final tests | SB07 fixed selected HTTP lifetime, stream, disposal, async wait, and JSON-options allocation issues; broad scan leads remain documented deferrals. |
| R008 EF/query optimization review | Requested EF skill | SB01, SB08, SB09 | `bundle://proof/SB08/manifest.md` and final tests | EF absence reconfirmed; SB08 hardened Explorer SQLite indexes/parameters, target update normalization, and application log rotation cost. |
| R009 Docker compose with persisted data | "start node with db together" | SB04, SB08, SB09 | `bundle://proof/SB09/transcripts/docker-multinode-e2e.txt` | SB04 added compose files and proved durable data; SB09 proved multi-node pin/unpin plus persistence after restart/rebuild. |
| R010 Detailed xlsx checklist | "use xlsx" | SB01, all subbundles, SB09 | `bundle://inventories/publishing-prep-checklists.xlsx` | Workbook is a first-class bundle artifact and is regenerated at closure. |
| R011 Baseline validation | Publishing hardening | SB01, SB09 | `bundle://proof/SB09/manifest.md` | Final build, full tests, packages, vulnerability scan, docker e2e, and browser smoke passed. |
