# photo-ingest: merging device dumps into a date archive

Design document, 2026-09-24. Implemented as written, with the amendments noted inline.

## Purpose

Phone, camera and cloud dumps accumulate in an "inprogress" folder (in the
author's case 92k files, 716 GB, roughly a quarter of them byte-identical repeats
across successive dumps of the same device). A separate curated gallery must
never be touched. We want a repeatable tool that takes one dump folder, skips
everything already ingested, and copies the rest into a predictable date-based
archive beside `gallery`, so that events can later be promoted into the curated gallery by
hand.

Success looks like: run the tool on the existing dumps once, then on each
future dump, and get an archive with one copy of each unique file, placed by
capture date, clutter parked aside, and a log that explains every decision.

## Non-goals

- No deletion anywhere. Source dumps are read-only input.
- No near-duplicate detection (re-encoded, resized, edited copies are kept).
- No writes to the curated gallery or to the dump folders.
- No GUI, no daemon. One command per dump.

## Decisions already taken

| Question | Decision |
|---|---|
| Target layout | Date archive `~/Pictures/archive/YYYY/YYYY-MM/`, sibling of the curated gallery |
| Duplicate definition | Exact content only (SHA-256) |
| Per-person split | No. Source dump name is recorded in the index, not the path |
| Language | C# on .NET 10, classic console project plus xunit test project (`dotnet run --project src/PhotoIngest`) |
| First run | Against a copy of the dump collection, dry-run first |

## Layout of the archive

```
~/Pictures/archive/
  2023/
    2023-11/
      20231104_183012_IMG_20231104_183012.jpg
      20231104_183012_IMG_20231104_183012.dng     # sidecar, same stem prefix
    2023-11 trip/                                  # optional --event suffix
  _screenshots/2023-11/…
  _whatsapp/2023-11/…
  _nonmedia/<dump-name>/<original relative path>
  _undated/<dump-name>/<original relative path>    # only when every date source failed
  _index.sqlite
  _manifest.sha256                                 # regenerated from the index after each run
  _runs/2026-09-24T14-03_2023.11-phone-dump.log
```

## Naming

`YYYYMMDD_HHMMSS_<original filename>`. The original name is kept so `PXL_`,
`IMG_`, `WP_` prefixes still identify the device. If the destination name already
exists with different content, a `~1`, `~2` suffix is inserted before the
extension.

Sidecars share the main file's stem in the source (`X.jpg` + `X.dng`, `X.MP`,
`X.pp3`, `X.thm`, `X.xmp`, `.RAW-02.ORIGINAL.dng` next to `.jpg`). They are
placed next to the main file with the same date prefix, using the main file's
date, regardless of their own metadata.

## Classification

Evaluated in order; the first matching rule wins.

1. Skip entirely: path contains `.stfolder`, `.thumbnails`, `@eaDir`,
   `.globalTrash`, `.deleteRecord`, `.trash`, `Trash/`, `System Volume
   Information`; filename is `.nomedia`, `Thumbs.db`, `desktop.ini`, `*.tmp`,
   `*~`. Skipped files are still listed in the run log with a count.
2. nonmedia: extension in the document/audio/archive set (`fb2 epub pdf doc docx
   xls rtf djvu mp3 opus zip unitypackage crypt12 json txt ini bin records nar
   tnl cover`) or path under `Books/`, `Documents/`, `Download/`, `Android/`.
   Google Takeout `*.json` sidecars are read for `photoTakenTime` before being
   parked, so they can still date their photo.
3. screenshots: path segment `Screenshots`, `ScreenRecorder`, `Screen
   recordings`, or filename starts with `Screenshot`.
4. whatsapp: path segment `WhatsApp*`, or filename matches `IMG-YYYYMMDD-WA…`,
   `VID-YYYYMMDD-WA…`, `PTT-…`.
5. media: everything else with an image or video extension (`jpg jpeg png gif
   heic heif webp bmp tif tiff dng orf nef arw cr2 raf mp4 mov 3gp mkv avi webm
   m4v mp xcf`).
6. Anything not matched by 1 to 5 goes to nonmedia under a `misc/` prefix and is
   flagged in the log so the rule set can be tuned.

## Date resolution

Tried in order; the source that succeeds is stored as `date_source` so
low-confidence placements can be reviewed with one query.

1. `exif` EXIF `DateTimeOriginal`, then `CreateDate` (JPEG, TIFF-based RAW, DNG,
   HEIC) via MetadataExtractor.
2. `video` QuickTime/MP4 movie header `Created` via MetadataExtractor. Values
   equal to the 1904 epoch are treated as missing. Container times are UTC by
   the MP4 spec while phone filenames carry local time, so when a video's
   filename has a full timestamp that wins (recorded as `filename`); otherwise
   the container time is converted to this machine's local time.
3. `sidecar` Google Takeout JSON `photoTakenTime.timestamp` for a file with a
   matching name.
4. `filename` regex over the name: `YYYYMMDD[_-]HHMMSS`, `YYYY-MM-DD`,
   `WP_YYYYMMDD_HH_MM_SS`, `IMG-YYYYMMDD-WA`, `PXL_YYYYMMDD_HHMMSS`,
   `VID_YYYYMMDD_HHMMSS`, `YYYYMMDD_HHMMSS`.
5. `folder` a `YYYY-MM-DD` or `YYYY.MM` date in any parent path segment inside
   the dump (Windows Phone and Google Takeout exports are organised this way).
6. `mtime` file modification time, only if it is after 2000-01-01.
7. Otherwise the file goes to `_undated/`.

Times are taken as local wall-clock as written by the device; no time zone
conversion. Files without a time component get `000000`.

## Duplicate handling

- SHA-256 of the full content, computed once per file per run.
- Fast path: if a row exists in the index with the same dump name, relative path
  and size, the file is assumed already ingested and is not re-hashed. This makes
  re-running the same dump after an interruption cheap.
- If the hash already exists in the index, the file is not copied. A row in
  `duplicates` records dump, relative path, and the id of the file it duplicates.
- Within one run, the first occurrence in walk order (sorted by path) wins.
- After copying, the destination is re-hashed and compared before the row is
  committed. A mismatch aborts the run with a clear error.

## Index schema

```
files      (id, sha256 UNIQUE, size, dest_rel, bucket, taken_at, date_source,
            dump, src_rel, ingested_at, run_id)
duplicates (id, sha256, dump, src_rel, size, of_file_id, run_id)
runs       (id, started_at, finished_at, dump, source_root, event, dry_run,
            counts_json)
```

`_manifest.sha256` is written from `files` after each successful run in
`sha256sum -c` format, for checking with standard tools.

## Command line

```
photo-ingest ingest <dump-dir> [--archive DIR] [--event NAME] [--dry-run]
photo-ingest report [--archive DIR] [--undated] [--source filename|folder|mtime|none]
photo-ingest verify [--archive DIR]
photo-ingest probe <file>...
```

(`photo-ingest` is `dotnet run --project src/PhotoIngest --` during
development, or the published binary in `~/photo-archive/bin/`.)

- `ingest` walks `<dump-dir>`, classifies, resolves dates, hashes, copies,
  records. The dump name is the folder's basename. `--dry-run` performs every
  step except copying and index writes, and prints per-bucket and per-date-source
  counts plus the first 50 planned destinations per bucket.
- `report` prints counts by year-month and bucket, and lists files whose
  `date_source` is weak (`folder`, `mtime`) or that landed in `_undated`.
- `verify` re-hashes the archive against the index and reports missing,
  changed or untracked files.

The archive path defaults to `~/Pictures/archive` and can be overridden for the
trial run into a sibling folder.

## Implementation notes

- Solution layout: `src/PhotoIngest/` (console app, `net10.0`, one file per
  class: Rules, Dates, Meta, Takeout, Resolve, Sidecars, Naming, Hashing,
  Catalog, Ingest, Report, Verify, Manifest, Probe, Options, Program) and
  `tests/PhotoIngest.Tests/` (xunit). Packages: `MetadataExtractor` 2.9.3,
  `Microsoft.Data.Sqlite` 10.0.12, and an explicit
  `SQLitePCLRaw.bundle_e_sqlite3` 2.1.13 to avoid the advisory that the
  default transitive 2.1.11 carries.
- Hashing and metadata reads run on a bounded parallel loop (degree = CPU count / 2) since the SSD is not the bottleneck. Copying
  stays sequential to keep the log ordered.
- Every decision (skip, bucket, date source, duplicate-of) is written to the
  per-run log as tab-separated lines so it can be grepped.
- Cyrillic and spaces in names are ordinary; no transliteration.
- Copies preserve mtime.

## Error handling

- Unreadable file: logged as `ERR`, counted, run continues.
- Metadata parse exception: treated as "no metadata", next date source tried.
- Copy or verify-after-copy failure: run aborts, index transaction rolled back,
  partially copied destination file removed. Re-running resumes via the fast
  path.
- Archive directory missing: created. Index missing: created with schema.

## Testing

- xunit tests in `tests/PhotoIngest.Tests`: filename date regexes,
  classification rules, sidecar grouping, destination naming with collisions,
  catalog queries against an in-memory SQLite, and one end-to-end ingest over a
  synthetic dump in a temp directory.
- Trial run: work on a copy of the dump collection, run `ingest --dry-run` for
  each dump folder, review counts and the `misc/` flags, tune rules, then run
  for real into a trial archive and `verify` it before adopting it.
