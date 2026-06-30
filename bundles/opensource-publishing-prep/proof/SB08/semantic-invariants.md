# SB08 Semantic Invariants

## Storage Invariants

- EF Core remains absent; storage hardening applies to raw SQLite, JSON files, and log files.
- Explorer index schema changes must be additive and safe for existing databases.
- SQLite command parameters should be explicitly typed on the Explorer index hot paths.
- Generated `IN` and `NOT IN` target lists should use normalized, deduplicated values.
- Application log rotation should not count the full active log file before every write.
- Existing JSON store formats, backups, and quarantine behavior must remain compatible.

## Behavioral Invariants Proven

- Existing and legacy Explorer index databases contain the new pinned-root list index after store initialization.
- Pinned target updates handle duplicate and whitespace-padded inputs correctly.
- Application log rotation honors the active file count after a store reload.
- Remote pin request and server settings JSON stores still migrate legacy files, create backups, and quarantine corrupt documents.
- Configured container/local persistence paths still bind through composition.

## Measured Invariants

- EF marker scan count is zero for `EntityFrameworkCore`, `DbContext`, `DbSet`, `UseSqlite`, and `UseSqlServer`.
- `ExplorerIndexStore` has zero `AddWithValue` occurrences after SB08.
- Focused storage test result: 20 passed, 0 failed, 0 skipped.
- Full solution build passed after storage hardening.
