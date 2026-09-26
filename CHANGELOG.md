# Changelog

## 0.2.0 (2026-09-26)

- `forget <path-or-glob>... [--delete]`: remove archived files on purpose. Dry
  listing by default; `--delete` deletes the files, moves their catalog rows to a
  new `dropped` table, prunes emptied folders and rewrites the manifest. `ingest`
  logs bytes that were dropped before as `dropped` and never copies them again;
  `report` shows the dropped count. Patterns support `*`, `**`, `?`, a directory
  meaning everything below it, and absolute paths inside the archive.
- `probe` applies directory rules to the full path, so it agrees with `ingest`
  about `@eaDir`, `Trash`, `Books/` and the like.
- Skip more NAS and macOS artefacts: `#recycle`, `@Recycle`,
  `.SynologyWorkingDirectory`, `@tmp`, `.@__thumb`, `.DS_Store`, `._*`.

## 0.1.1 (2026-09-26)

- `version` command and `--version` flag.
- `probe` reports a missing file on stderr and continues instead of crashing;
  exits 1 if any file was missing.

## 0.1.0 (2026-09-26)

First release.

- `ingest` with `--dry-run` and `--event`: classification into media,
  screenshots, WhatsApp, non-media and misc; sidecar grouping (DNG, PP3, THM,
  XMP, Motion Photo); date resolution from EXIF, video header, Google Takeout
  JSON, filename, folder name and mtime; SHA-256 catalog in SQLite with a fast
  path for re-runs; verified copies via `.part` files; tab-separated run logs;
  `_manifest.sha256`.
- `report`, `verify`, `probe`, `help`.
- Live progress bars on a terminal, plain per-phase lines when redirected.
- Proven on a 92k-file, 716 GB collection: 55k unique files archived, 35k
  duplicates skipped, verify clean.
