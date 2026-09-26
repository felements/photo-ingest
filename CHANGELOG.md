# Changelog

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
