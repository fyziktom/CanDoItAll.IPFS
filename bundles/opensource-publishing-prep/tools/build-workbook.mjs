import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const artifactToolPath = process.env.ARTIFACT_TOOL_PATH
  ?? "C:/Users/dell/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs";

const { SpreadsheetFile, Workbook } = await import(pathToFileURL(artifactToolPath).href);

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const bundleRoot = path.resolve(scriptDir, "..");
const outputDir = path.join(bundleRoot, "inventories");
const workbookPath = path.join(outputDir, "publishing-prep-checklists.xlsx");
const previewPath = path.join(outputDir, "publishing-prep-checklists-preview.png");

await fs.mkdir(outputDir, { recursive: true });

const workbook = Workbook.create();
const statusValues = ["Planned", "Ready", "In Progress", "Blocked", "Done", "Deferred"];
const priorityValues = ["Critical", "High", "Medium", "Low"];

function colName(index) {
  let n = index + 1;
  let name = "";
  while (n > 0) {
    const rem = (n - 1) % 26;
    name = String.fromCharCode(65 + rem) + name;
    n = Math.floor((n - 1) / 26);
  }
  return name;
}

function rangeAddress(startRow, startCol, rowCount, colCount) {
  const start = `${colName(startCol)}${startRow + 1}`;
  const end = `${colName(startCol + colCount - 1)}${startRow + rowCount}`;
  return `${start}:${end}`;
}

function styleSheet(sheet, title, subtitle, headers, rows, tableName) {
  sheet.showGridLines = false;
  sheet.getRange("A1:H1").merge();
  sheet.getRange("A1").values = [[title]];
  sheet.getRange("A1").format = {
    fill: "#17324D",
    font: { bold: true, color: "#FFFFFF", size: 16 },
    wrapText: true,
  };
  sheet.getRange("A2:H2").merge();
  sheet.getRange("A2").values = [[subtitle]];
  sheet.getRange("A2").format = {
    fill: "#E8EEF5",
    font: { color: "#17324D", size: 10 },
    wrapText: true,
  };

  const headerRow = 3;
  const values = [headers, ...rows];
  const address = rangeAddress(headerRow, 0, values.length, headers.length);
  const dataRange = sheet.getRange(address);
  dataRange.values = values;
  sheet.tables.add(address, true, tableName);

  const headerAddress = rangeAddress(headerRow, 0, 1, headers.length);
  sheet.getRange(headerAddress).format = {
    fill: "#244B73",
    font: { bold: true, color: "#FFFFFF" },
    wrapText: true,
  };

  const bodyAddress = rangeAddress(headerRow + 1, 0, Math.max(rows.length, 1), headers.length);
  sheet.getRange(bodyAddress).format = {
    wrapText: true,
    verticalAlignment: "top",
    borders: { preset: "inside", style: "thin", color: "#D7DEE8" },
  };
  sheet.getRange(address).format.borders = { preset: "outside", style: "medium", color: "#A8B4C2" };
  sheet.freezePanes.freezeRows(4);

  if (headers.includes("Status")) {
    const statusColumn = headers.indexOf("Status");
    const statusRange = rangeAddress(headerRow + 1, statusColumn, Math.max(rows.length, 1), 1);
    sheet.getRange(statusRange).dataValidation = { rule: { type: "list", values: statusValues } };
  }
  if (headers.includes("Priority")) {
    const priorityColumn = headers.indexOf("Priority");
    const priorityRange = rangeAddress(headerRow + 1, priorityColumn, Math.max(rows.length, 1), 1);
    sheet.getRange(priorityRange).dataValidation = { rule: { type: "list", values: priorityValues } };
  }

  for (let i = 0; i < headers.length; i++) {
    const width = headers[i].includes("Reference") || headers[i].includes("Evidence")
      ? 34
      : headers[i].includes("Notes") || headers[i].includes("Concern")
        ? 42
        : 18;
    sheet.getRange(rangeAddress(0, i, values.length + 3, 1)).format.columnWidth = width;
  }
}

const allChecklistRows = [
  ["SB01", "Refresh baseline build and warning inventory", "Critical", "SB01", "Done", "bundle://proof/SB01/transcripts/build-no-restore.txt", "repo://CanDoItAll.IPFS.slnx", "Build currently succeeds with 15 advisory warnings in the refreshed incremental baseline."],
  ["SB01", "Refresh EF absence scan", "High", "SB01", "Done", "bundle://proof/SB01/transcripts/ef-core-marker-scan.txt", "bundle://analysis/01-current-state.md", "No EF Core markers found in inspected source/tests."],
  ["SB01", "Refresh large-file hotspot inventory", "High", "SB01", "Done", "bundle://proof/SB01/transcripts/large-source-file-scan.txt", "bundle://inventories/01-scope-inventory.md", "Used to prioritize refactor work."],
  ["SB02", "Correct stale package metadata and URLs", "High", "SB02", "Done", "bundle://proof/SB02/transcripts/pack-engine-release.txt and bundle://proof/SB02/transcripts/pack-client-release.txt", "repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj", "Engine and Client now point at the fyziktom/CanDoItAll.IPFS repository."],
  ["SB02", "Resolve or document dependency advisories", "Critical", "SB02", "Done", "bundle://proof/SB02/transcripts/package-vulnerable-after-updates.txt", "repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj", "OpenTelemetry and SQLitePCLRaw advisories are clear after package updates and explicit SQLitePCLRaw bundle pin."],
  ["SB02", "Prepare open-source README and license posture", "High", "SB02", "Done", "repo://README.md and bundle://proof/SB02/transcripts/package-content-check.txt", "repo://README.md", "README documents license lineage and the component-package publication caveat without claiming docker proof."],
  ["SB03", "Define NodeControl abstractions", "Critical", "SB03", "Done", "bundle://proof/SB03/transcripts/project-reference-graph.txt", "repo://src/CanDoItAll.IPFS.NodeControl.Abstractions/Abstractions/INodeOperator.cs", "Contracts project created for future CLI and SB05."],
  ["SB03", "Extract reusable workflow/persistence boundaries", "Critical", "SB03", "Done", "bundle://proof/SB03/transcripts/focused-nodecontrol-layering-tests.txt", "bundle://architecture/01-target-solution.md", "Reusable contracts and models no longer depend on Blazor."],
  ["SB04", "Add root docker compose runtime", "Critical", "SB04", "Done", "bundle://proof/SB04/transcripts/docker-compose-up.txt", "repo://docker-compose.yml", "Compose starts Engine API and NodeControl UI with explicit container configuration."],
  ["SB04", "Preserve IPFS repo and app data volumes", "Critical", "SB04", "Done", "bundle://proof/SB04/transcripts/docker-compose-restart-and-verify.txt and bundle://proof/SB04/transcripts/docker-compose-rebuild-and-verify.txt", "repo://src/CanDoItAll.IPFS.NodeControl/appsettings.Container.json", "Pinned CID, peer identity, and remote pin request survived restart and rebuild."],
  ["SB05", "Split file/content/network/repo workflows", "High", "SB05", "Done", "bundle://proof/SB05/transcripts/focused-nodeoperator-decomposition-tests.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs", "Facade retained for compatibility; workflows now live behind file, explorer, content, network, and maintenance boundaries."],
  ["SB06", "Decompose Files page and related explorer UI", "High", "SB06", "Done", "bundle://proof/SB06/browser-smoke-summary.json and bundle://proof/SB06/transcripts/ui-line-counts-after-codebehind-split.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs", "Files route uses existing child components and narrower workflows; the route-state code-behind remains a documented future helper-extraction candidate."],
  ["SB06", "Decompose Content, Network, Settings, and RemotePin modal", "High", "SB06", "Done", "bundle://proof/SB06/transcripts/browser-smoke-playwright-passing-filtered.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor", "Markup/code-behind split preserved busy/error/modal behavior at large desktop sizes."],
  ["SB07", "Triage blocking waits and async lifecycle", "High", "SB07", "Done", "bundle://proof/SB07/transcripts/performance-scan-after-sb07.txt and bundle://proof/SB07/performance-triage.md", "bundle://analysis/01-current-state.md", "NodeControl workflow sync wait removed; remaining test and inherited DNS/MDNS waits are documented deferrals."],
  ["SB07", "Triage allocation/string/LINQ hot paths", "Medium", "SB07", "Done", "bundle://proof/SB07/performance-triage.md", "repo://src/CanDoItAll.IPFS.Engine/Base/net-udns/DohClient.cs", "Fixed HTTP lifetime/stream/disposal and JSON options allocation; broad LINQ/string/allocation leads deferred pending profiling."],
  ["SB08", "Harden ExplorerIndexStore SQLite behavior", "Critical", "SB08", "Done", "bundle://proof/SB08/transcripts/sqlite-storage-source-proof.txt and bundle://proof/SB08/transcripts/focused-storage-tests.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs", "EF Core is absent; ExplorerIndexStore now has a pinned-root list index, typed parameters, and normalized target updates."],
  ["SB08", "Harden JSON stores and log rotation", "High", "SB08", "Done", "bundle://proof/SB08/transcripts/focused-storage-tests.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs", "JSON store atomic/quarantine behavior preserved; log rotation avoids repeated full active-file counts."],
  ["SB09", "Run final build/test/package validation", "Critical", "SB09", "Done", "bundle://proof/SB09/transcripts/test-final-full-after-progress-fix.txt and bundle://proof/SB09/transcripts/build-pack-client-final-after-fixes.txt", "repo://CanDoItAll.IPFS.slnx", "Full solution tests and package validation passed after final pin/progress fixes."],
  ["SB09", "Run final docker and browser validation", "Critical", "SB09", "Done", "bundle://proof/SB09/transcripts/docker-multinode-e2e.txt and bundle://proof/SB09/browser-smoke-summary.json", "bundle://reviews/01-execution-report.md", "Docker multi-node pin/unpin proof and 1920x1080/1600x900 browser smoke passed."],
  ["SB09", "Close raw notes and workbook statuses", "Critical", "SB09", "Done", "bundle://proof/SB09/manifest.md", "bundle://traceability/01-requirement-traceability.md", "Raw notes are closed with artifact-backed proof; legacy marker follow-ups are explicitly deferred."],
];

const overviewSheet = workbook.worksheets.add("Overview");
const checklistSheet = workbook.worksheets.add("All Checklist");
const hotspotSheet = workbook.worksheets.add("Architecture Hotspots");
const publishingSheet = workbook.worksheets.add("Publishing");
const dockerSheet = workbook.worksheets.add("Docker Persistence");
const uiSheet = workbook.worksheets.add("UI Decomposition");
const perfSheet = workbook.worksheets.add("Performance");
const storageSheet = workbook.worksheets.add("Storage Query");
const validationSheet = workbook.worksheets.add("Validation Evidence");
const traceSheet = workbook.worksheets.add("Traceability");

styleSheet(
  checklistSheet,
  "Open Source Publishing Preparation - Master Checklist",
  "Editable execution checklist. Update Status and Evidence Required as each subbundle runs.",
  ["Area", "Item", "Priority", "Owner", "Status", "Evidence Required", "Source Reference", "Notes"],
  allChecklistRows,
  "AllChecklistTable",
);

styleSheet(
  hotspotSheet,
  "Architecture Hotspots",
  "Large or mixed-responsibility files that drive the refactor plan.",
  ["File", "Approx Lines", "Concern", "Owner", "Status", "Proposed Isolation"],
  [
    ["repo://src/CanDoItAll.IPFS.NodeControl/wwwroot/app.css", 1845, "Global CSS surface is broad, but SB06 screenshot proof did not require CSS surgery.", "SB06", "Deferred", "Revisit only with a concrete maintainability driver; no mobile redesign."],
    ["repo://src/CanDoItAll.IPFS.NodeControl/Services/NodeOperatorService.cs", 134, "Compatibility facade over decomposed workflow services.", "SB03/SB05", "Done", "SB05 split file, explorer, content, network, and maintenance workflows; SB06 can migrate pages to narrower dependencies."],
    ["repo://src/CanDoItAll.IPFS.Engine/Base/peer-talk/Swarm.cs", 971, "Long runtime/network file with async/lifecycle performance risk.", "SB07", "Planned", "Review lifecycle, cancellation, allocation, and error paths."],
    ["repo://tests/CanDoItAll.IPFS.Tests/CoreApi/FileSystemApiTest.cs", 920, "Large test file may hide coverage gaps and brittle setup.", "SB01/SB09", "Planned", "Use as regression baseline; refactor only if needed for proof."],
    ["repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs", 848, "Page state, upload flows, pinned cache, explorer navigation, and refresh remain coupled after dependency narrowing.", "SB06/SB09", "In Progress", "Existing child components and narrower workflows are in place; future state-helper extraction remains the clear next split."],
    ["repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor", 401, "Markup is now split from 397 lines of code-behind handlers.", "SB06", "Done", "Code-behind split and workflow dependency migration completed with browser proof."],
    ["repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor", 377, "Markup is now split from 423 lines of code-behind handlers.", "SB06", "Done", "Network workflow dependency migration completed; live PubSub keeps direct client access for now."],
    ["repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/RemotePinShareModal.razor", 246, "Modal markup is now split from 370 lines of code-behind handlers.", "SB06", "Done", "Target/probe/send/export behavior preserved with modal browser proof."],
  ],
  "HotspotTable",
);

styleSheet(
  publishingSheet,
  "Open Source Publishing Checks",
  "Metadata, docs, package, license, and dependency readiness before public release.",
  ["Check", "Priority", "Owner", "Status", "Evidence Required", "Source Reference", "Notes"],
  [
    ["Replace stale upstream package URLs or document lineage", "High", "SB02", "Done", "Project file diff and pack transcripts", "repo://src/CanDoItAll.IPFS.Engine/CanDoItAll.IPFS.Engine.csproj", "Engine and Client now use the current repository URL while retaining lineage notes."],
    ["Replace deprecated PackageIconUrl usage", "Medium", "SB02", "Done", "Pack validation and package content check", "repo://src/CanDoItAll.IPFS.Client/CanDoItAll.IPFS.Client.csproj", "Packages include README.md and package-icon.png."],
    ["Review inherited LICENSE and copyright posture", "Critical", "SB02", "Done", "README lineage note", "repo://LICENSE", "License history retained; package metadata adds CanDoItAll contributors."],
    ["Update README from local-run notes to open-source onboarding", "High", "SB02/SB04/SB09", "Done", "README diff and docker persistence proof", "repo://README.md", "Docker instructions now reflect the SB04 compose runtime proof."],
    ["Resolve or accept vulnerability advisories", "Critical", "SB02", "Done", "bundle://proof/SB02/transcripts/package-vulnerable-after-updates.txt", "repo://src/CanDoItAll.IPFS.NodeControl/CanDoItAll.IPFS.NodeControl.csproj", "No vulnerable packages reported after update."],
  ],
  "PublishingTable",
);

styleSheet(
  dockerSheet,
  "Docker Persistence Plan",
  "Compose implementation and proof checklist. SB04 completed the first release-critical docker proof.",
  ["Check", "Priority", "Owner", "Status", "Evidence Required", "Source Reference", "Notes"],
  [
    ["Compose starts node/API and required NodeControl runtime", "Critical", "SB04", "Done", "bundle://proof/SB04/transcripts/docker-compose-up.txt", "repo://docker-compose.yml", "Topology is Engine API plus NodeControl UI."],
    ["IPFS repo data is in a durable volume", "Critical", "SB04", "Done", "bundle://proof/SB04/transcripts/docker-compose-restart-and-verify.txt", "repo://docker-compose.yml", "IPFS_PATH is /data/ipfs with named volume ipfs-node-data."],
    ["Explorer SQLite database is in a durable volume", "Critical", "SB04/SB08", "Done", "bundle://proof/SB04/transcripts/volume-files-before-restart.txt", "repo://src/CanDoItAll.IPFS.NodeControl/appsettings.Container.json", "Explorer DB is configured under /data/node-control/explorer-index."],
    ["Settings and remote pin JSON stores are durable", "High", "SB04/SB08", "Done", "bundle://proof/SB04/transcripts/docker-compose-rebuild-and-verify.txt", "repo://src/CanDoItAll.IPFS.NodeControl/appsettings.Container.json", "Remote pin request JSON survived restart and rebuild."],
    ["Application logs are durable and bounded", "Medium", "SB04/SB08", "Done", "bundle://proof/SB04/transcripts/volume-files-before-restart.txt and bundle://proof/SB08/transcripts/focused-storage-tests.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs", "Durable log path is configured and rotation accounting is covered by SB08 tests."],
    ["Secrets are not committed", "Critical", "SB04", "Done", "repo://docker-compose.yml", "repo://docker-compose.yml", "IPFS_PASS is required from the caller environment."],
  ],
  "DockerTable",
);

styleSheet(
  uiSheet,
  "Large Screen UI Decomposition",
  "Desktop-only UI maintainability checklist. Do not tune small or medium viewports.",
  ["UI Area", "Priority", "Owner", "Status", "Evidence Required", "Source Reference", "Notes"],
  [
    ["Files route shell and explorer state", "High", "SB06", "Done", "bundle://proof/SB06/screenshots/sb06-files-1920x1080.png and bundle://proof/SB06/screenshots/sb06-files-1600x900.png", "repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Files.razor.cs", "Uploads, pinned items, refresh, and errors remain covered; future state-helper extraction is documented."],
    ["Content command panels", "High", "SB06", "Done", "bundle://proof/SB06/screenshots/sb06-content-1920x1080.png and focused component tests", "repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Content.razor", "Block/object/DAG/name/key panels split into markup plus code-behind."],
    ["Network panels", "High", "SB06", "Done", "bundle://proof/SB06/screenshots/sb06-network-1920x1080.png and console review summary", "repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Network.razor", "Peer, bootstrap, filters, DHT, and PubSub behavior preserved."],
    ["Remote pin share modal", "High", "SB06", "Done", "bundle://proof/SB06/screenshots/sb06-remote-pin-share-modal-1920x1080.png and modal open proof", "repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/RemotePinShareModal.razor", "Modal markup and code-behind split; opened successfully at both desktop viewports."],
    ["Settings route", "Medium", "SB06", "Done", "bundle://proof/SB06/screenshots/sb06-settings-1920x1080.png", "repo://src/CanDoItAll.IPFS.NodeControl/Components/Pages/Settings.razor", "Configuration workflows preserved with maintenance workflow dependency."],
    ["Global CSS organization", "Medium", "SB06", "Deferred", "bundle://proof/SB06/browser-smoke-summary.json", "repo://src/CanDoItAll.IPFS.NodeControl/wwwroot/app.css", "No CSS refactor was needed for the behavior-preserving split."],
  ],
  "UITable",
);

styleSheet(
  perfSheet,
  "Performance Triage",
  "Static scan results from the .NET performance lens. Counts are leads, not automatic refactors.",
  ["Finding", "Count", "Priority", "Owner", "Status", "Evidence Required", "Notes"],
  [
    ["Blocking waits", 81, "High", "SB07", "Done", "bundle://proof/SB07/transcripts/performance-scan-after-sb07.txt", "NodeControl workflow candidate fixed; remaining hits are tests and inherited DNS/MDNS sync surfaces documented in triage."],
    ["async void", 18, "High", "SB07", "Deferred", "bundle://proof/SB07/performance-triage.md", "No non-event high-risk fix was selected for SB07."],
    ["Manual HttpClient construction", 3, "High", "SB07", "Done", "bundle://proof/SB07/transcripts/production-targeted-scan-after-sb07.txt", "Production count is zero; remaining broad-scan hits are test harness clients."],
    ["Substring allocations", 19, "Medium", "SB07", "Deferred", "bundle://proof/SB07/performance-triage.md", "Mostly parser/protocol/test paths; span rewrites deferred until profiling proves hot-path value."],
    ["StartsWith/EndsWith/Contains", 286, "Medium", "SB07", "Planned", "triage notes", "Check StringComparison on correctness-sensitive paths."],
    ["LINQ Select/Where/OrderBy/GroupBy", 345, "Medium", "SB07", "Deferred", "bundle://proof/SB07/performance-triage.md", "No blanket rewrite; optimize only with focused profiling."],
    ["LINQ All/Any", 156, "Low", "SB07", "Deferred", "bundle://proof/SB07/performance-triage.md", "No char LINQ anti-patterns found; broad All/Any review deferred."],
    ["new Dictionary/List allocations", 177, "Medium", "SB07", "Deferred", "bundle://proof/SB07/performance-triage.md", "Broad allocation signal; not a safe mechanical rewrite."],
    ["TODO/FIXME/HACK", 81, "Medium", "SB01/SB09", "Deferred", "bundle://proof/SB09/transcripts/release-risk-marker-scan.txt", "Remaining markers are inherited protocol/backlog notes; no new SB09 blocker was introduced."],
    ["NotImplemented", 27, "High", "SB09", "Deferred", "bundle://proof/SB09/transcripts/release-risk-marker-scan.txt", "Remaining unsupported paths are explicit capability limits or stream contract members; release docs avoid claiming unsupported coverage."],
  ],
  "PerformanceTable",
);

styleSheet(
  storageSheet,
  "Storage And Query Hardening",
  "EF Core is absent; apply query optimization intent to raw SQLite, JSON stores, and logs.",
  ["Store", "Priority", "Owner", "Status", "Evidence Required", "Source Reference", "Notes"],
  [
    ["EF Core marker scan", "High", "SB01/SB08", "Done", "bundle://proof/SB08/transcripts/ef-core-marker-scan-after-sb08-start.txt", "bundle://analysis/01-current-state.md", "No DbContext/DbSet/EntityFrameworkCore markers found."],
    ["ExplorerIndexStore SQLite schema and indexes", "Critical", "SB08", "Done", "bundle://proof/SB08/transcripts/sqlite-storage-source-proof.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/ExplorerIndexStore.cs", "Added pinned-root list index, typed SQLite parameters, target normalization, and runtime index tests."],
    ["ApplicationLogStore rotation", "High", "SB08", "Done", "bundle://proof/SB08/transcripts/focused-storage-tests.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/ApplicationLogStore.cs", "Rotation now maintains active-file entry count after initial read/reload."],
    ["RemotePinRequestStore JSON durability", "High", "SB08", "Done", "bundle://proof/SB08/transcripts/focused-storage-tests.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/RemotePinRequestStore.cs", "Existing atomic migration, backup, and quarantine tests passed."],
    ["ServerNodeSettingsStore JSON durability", "High", "SB08", "Done", "bundle://proof/SB08/transcripts/focused-storage-tests.txt", "repo://src/CanDoItAll.IPFS.NodeControl/Services/ServerNodeSettingsStore.cs", "Existing atomic migration, backup, quarantine, and configured path tests passed."],
    ["Docker/local path configuration", "Critical", "SB04/SB08", "Done", "bundle://proof/SB04/transcripts/docker-compose-rebuild-and-verify.txt", "repo://src/CanDoItAll.IPFS.NodeControl/appsettings.Container.json", "Container config no longer relies on LocalApplicationData for durable stores."],
  ],
  "StorageTable",
);

styleSheet(
  validationSheet,
  "Validation Evidence Plan",
  "Commands, browser proof, docker proof, and validator gates required before publication.",
  ["Subbundle", "Required Proof", "Priority", "Owner", "Status", "Evidence Location", "Notes"],
  [
    ["SB01", "Baseline build, warnings, scans", "Critical", "SB01", "Ready", "bundle://reviews/01-execution-report.md", "Refresh before implementation."],
    ["SB02", "Build, package/advisory, metadata diff", "High", "SB02", "Done", "bundle://proof/SB02/transcripts", "No unproven docker docs; package/advisory proof captured."],
    ["SB03", "Build, service composition tests, dependency graph, proof manifest", "Critical", "SB03", "Done", "bundle://proof/SB03/manifest.md", "Architecture-critical boundary complete; SB05 decomposition remains."],
    ["SB04", "docker compose up/restart/rebuild persistence proof", "Critical", "SB04", "Done", "bundle://proof/SB04/manifest.md", "Release-critical proof captured; SB09 will rerun final validation."],
    ["SB05", "Focused workflow tests and route smoke", "High", "SB05", "Done", "bundle://proof/SB05/transcripts/focused-nodeoperator-decomposition-tests.txt and bundle://proof/SB05/transcripts/nodeoperator-page-smoke-tests-passing.txt", "Behavior-preserving refactor passed."],
    ["SB06", "Playwright screenshots at 1920x1080 and 1600x900", "High", "SB06", "Done", "bundle://proof/SB06/browser-smoke-summary.json", "Desktop UI only; no console errors, page errors, or failed requests recorded."],
    ["SB07", "Performance scan before/after and focused tests", "High", "SB07", "Done", "bundle://proof/SB07/performance-triage.md", "Fixed selected hot/risky items; deferred broad scan findings with rationale."],
    ["SB08", "EF scan, storage tests, persistence proof manifest", "Critical", "SB08", "Done", "bundle://proof/SB08/manifest.md", "Data-critical storage hardening completed; docker final rerun remains SB09."],
    ["SB09", "Full build/test/package/docker/browser/validator proof", "Critical", "SB09", "Done", "bundle://proof/SB09/manifest.md", "Closure proof captured for tests, packages, docker multi-node e2e, browser smoke, vulnerability scan, and bundle validator."],
  ],
  "ValidationTable",
);

styleSheet(
  traceSheet,
  "Requirement Traceability",
  "Raw request coverage mapped to owning subbundles and planned proof.",
  ["Requirement", "Owner", "Status", "Evidence Required", "Notes"],
  [
    ["R001 Preparation-only bundle", "SB01/SB09", "Done", "prepared and completed validators plus git diff review", "Preparation-only constraint was honored during bundle prep; implementation now has completed proof."],
    ["R002 Publishing readiness", "SB01/SB02/SB09", "Done", "docs/package/dependency/final validation", "Package metadata, README, dependency scan, packages, and final validation are complete."],
    ["R003 Messy parts", "SB01/SB03/SB05/SB06", "Done", "hotspot inventory and refactor proof", "NodeOperator hotspot is decomposed; large UI markup/code-behind split is complete; Files route state remains a future extraction candidate."],
    ["R004 NodeControl isolation", "SB03/SB05", "Done", "bundle://proof/SB05/transcripts/focused-nodeoperator-decomposition-tests.txt", "SB03 extracted UI-free contracts; SB05 split concrete workflows."],
    ["R005 Future CLI feasibility", "SB03/SB05/SB09", "Done", "interfaces and graph proof", "Reusable workflow interfaces exist; CLI implementation remains out of scope."],
    ["R006 Large desktop UI only", "SB06/SB09", "Done", "bundle://proof/SB06/browser-smoke-summary.json and SB09 final rerun", "SB06 captured 1920x1080 and 1600x900 proof with no small/medium tuning."],
    ["R007 .NET performance", "SB01/SB07/SB09", "Done", "bundle://proof/SB07/performance-triage.md", "Performance skill used as lens; selected fixes are proven and deferrals documented."],
    ["R008 EF/query optimization", "SB01/SB08/SB09", "Done", "bundle://proof/SB08/transcripts/focused-storage-tests.txt", "EF Core absent; raw SQLite and file stores were hardened."],
    ["R009 Docker compose persisted data", "SB04/SB08/SB09", "Done", "bundle://proof/SB09/transcripts/docker-multinode-e2e.txt", "Compose added, persistence proof captured, and final multi-node pin/unpin e2e passed."],
    ["R010 XLSX checklist", "SB01/all/SB09", "Done", "this workbook", "Status updated through final implementation."],
    ["R011 Final validation", "SB09", "Done", "bundle://proof/SB09/manifest.md", "Final closure gate completed."],
  ],
  "TraceabilityTable",
);

overviewSheet.showGridLines = false;
overviewSheet.getRange("A1:H1").merge();
overviewSheet.getRange("A1").values = [["CanDoItAll.IPFS Open Source Publishing Preparation"]];
overviewSheet.getRange("A1").format = {
  fill: "#17324D",
  font: { bold: true, color: "#FFFFFF", size: 18 },
};
overviewSheet.getRange("A2:H2").merge();
overviewSheet.getRange("A2").values = [["Completed bundle workbook with implementation statuses, evidence locations, and final release-validation notes."]];
overviewSheet.getRange("A2").format = {
  fill: "#E8EEF5",
  font: { color: "#17324D", size: 10 },
  wrapText: true,
};
overviewSheet.getRange("A4:B10").values = [
  ["Metric", "Value"],
  ["Bundle", "bundle://opensource-publishing-prep"],
  ["Completion date", "2026-06-30"],
  ["Total checklist rows", allChecklistRows.length],
  ["Planned rows", null],
  ["Ready rows", null],
  ["Critical rows", null],
];
overviewSheet.getRange("B8").formulas = [[`=COUNTIF('All Checklist'!$E$5:$E$${allChecklistRows.length + 4},"Planned")`]];
overviewSheet.getRange("B9").formulas = [[`=COUNTIF('All Checklist'!$E$5:$E$${allChecklistRows.length + 4},"Ready")`]];
overviewSheet.getRange("B10").formulas = [[`=COUNTIF('All Checklist'!$C$5:$C$${allChecklistRows.length + 4},"Critical")`]];
overviewSheet.getRange("A4:B4").format = {
  fill: "#244B73",
  font: { bold: true, color: "#FFFFFF" },
};
overviewSheet.getRange("A5:B10").format = {
  borders: { preset: "inside", style: "thin", color: "#D7DEE8" },
};
overviewSheet.getRange("D4:H4").values = [["Subbundle", "Theme", "Critical", "Depends On", "Validation"]];
overviewSheet.getRange("D5:H13").values = [
  ["SB01", "Baseline And Risk Inventory", "Yes", "none", "Build/warning/checklist proof"],
  ["SB02", "Metadata And Dependencies", "No", "SB01", "Package/advisory/docs proof"],
  ["SB03", "Layering And Project Extraction", "Yes", "SB01", "Dependency graph and tests"],
  ["SB04", "Docker And Persistence", "Yes", "SB01", "Restart/rebuild persistence proof"],
  ["SB05", "NodeOperator Decomposition", "No", "SB03", "Workflow tests and route smoke"],
  ["SB06", "Large Screen UI Decomposition", "No", "SB05", "Playwright desktop proof"],
  ["SB07", "Performance Hardening", "No", "SB03", "Focused tests/perf evidence"],
  ["SB08", "Storage Query Hardening", "Yes", "SB04/SB05", "Storage tests and manifest"],
  ["SB09", "Release Validation", "Yes", "SB02/SB04/SB06/SB07/SB08", "Full closure proof"],
];
overviewSheet.getRange("D4:H4").format = {
  fill: "#244B73",
  font: { bold: true, color: "#FFFFFF" },
};
overviewSheet.getRange("D5:H13").format = {
  wrapText: true,
  borders: { preset: "inside", style: "thin", color: "#D7DEE8" },
};
overviewSheet.getRange("A4:B10").format.borders = { preset: "outside", style: "medium", color: "#A8B4C2" };
overviewSheet.getRange("D4:H13").format.borders = { preset: "outside", style: "medium", color: "#A8B4C2" };
overviewSheet.freezePanes.freezeRows(4);
for (let i = 0; i < 8; i++) {
  overviewSheet.getRange(rangeAddress(0, i, 14, 1)).format.columnWidth = i === 4 ? 34 : 22;
}

if (process.env.RENDER_PREVIEW === "1") {
  const preview = await workbook.render({
    sheetName: "Overview",
    autoCrop: "all",
    scale: 1,
    format: "png",
  });
  await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
  console.log(`Wrote ${previewPath}`);
}

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(workbookPath);
console.log(`Wrote ${workbookPath}`);
