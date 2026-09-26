# photo-ingest

Merge phone, camera and cloud photo dumps into one date-based archive. Files
you already archived are recognised by content and skipped; everything else is
copied with hash verification, and every decision is logged.

```
$ photo-ingest ingest ~/dumps/2026.10-phone-dump --dry-run
walking   13,484                                   ━━━━━━━━━━━━━━━━━━━━ 100%
dating    9,812                                    ━━━━━━━━━━━━━━━━━━━━ 100%
hashing   95.4 GB                                  ━━━━━━━━━━━━━━━━━━━━ 100%
copying   95.4 GB   copy 3,823  duplicate 9,647  skip 14  error 0
```

[![CI](https://github.com/felements/photo-ingest/actions/workflows/ci.yml/badge.svg)](https://github.com/felements/photo-ingest/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/felements/photo-ingest)](https://github.com/felements/photo-ingest/releases)

## Why

Every few months a phone gets dumped to disk. Every dump repeats most of the
previous one, plus WhatsApp forwards, screenshots, audiobooks and app caches.
After a few years the "to sort" folder held 92,000 files and 716 GB, of which
a third were byte-identical repeats. Tools that sort photos into date folders
exist, but none of them remembers what it has already seen, so each dump had
to be deduplicated by hand before sorting.

photo-ingest keeps a small SQLite catalog inside the archive. A file whose
SHA-256 is already there is not copied again, so re-importing the same phone
costs a hash pass and nothing else. The archive itself stays a plain folder
tree that any backup tool, NAS or image viewer can use.

## Who it is for, and who it is not for

Good fit:

- you collect dumps from phones, cards and Google Takeout into a folder and
  want one clean archive out of them;
- you want a plain directory tree, not a database-backed photo manager;
- you want to be able to check the archive later (`verify`) and see why each
  file went where it went (`probe`, the run logs).

Look elsewhere if:

- you want a photo manager with faces, maps and a web UI: Immich, PhotoPrism
  or digiKam;
- you only need "put photos into date folders once": `phockup`, `elodie`,
  `exiftool` one-liners;
- you need near-duplicate detection (re-encoded or resized copies):
  `czkawka`, `rmlint`. photo-ingest deliberately treats only identical bytes
  as duplicates and keeps everything else.

## Install

Download the single-file binary for your OS from the
[releases page](https://github.com/felements/photo-ingest/releases), make it
executable and put it on your `PATH`. Nothing else is needed.

Or build from source with the .NET 10 SDK:

```bash
git clone https://github.com/felements/photo-ingest
cd photo-ingest
dotnet publish src/PhotoIngest -c Release -o bin --self-contained false
bin/photo-ingest help
```

## Quick start

```bash
# 1. see what would happen; writes only a log
photo-ingest ingest /path/to/2026.10-phone-dump --archive ~/Pictures/archive --dry-run

# 2. do it
photo-ingest ingest /path/to/2026.10-phone-dump --archive ~/Pictures/archive

# 3. look around
photo-ingest report --archive ~/Pictures/archive
photo-ingest verify --archive ~/Pictures/archive
```

`--archive` defaults to `~/Pictures/archive`. The dump folder's name becomes the
dump name in the catalog, so name it before ingesting (`2026.10-phone-dump`
beats `DCIM`).

For a folder that is a single trip or event, `--event NAME` suffixes the month
folders: `--event georgia` gives `2016/2016-06 georgia/`.

## What it does, per dump

1. Walks the dump and sorts every file into a bucket: media, screenshots,
   WhatsApp, non-media (books, documents, audio, JSON) or junk to skip.
2. Groups sidecars (DNG, PP3, THM, Motion Photo) with the photo they belong to.
3. Works out when each photo was taken: EXIF, video header, Google Takeout JSON,
   the filename, a dated folder name, then file mtime as the last resort.
4. Hashes every file with SHA-256 and looks it up in the catalog. Same bytes
   already archived means "duplicate", logged and not copied.
5. Copies the rest into `YYYY/YYYY-MM/YYYYMMDD_HHMMSS_<original name>` through a
   `.part` file, re-hashes the copy, then moves it into place and records it.
6. Rewrites `_manifest.sha256` and finishes the run log.

It only reads the dump. It writes only under the archive. It never deletes.

## Archive layout

```
~/Pictures/archive/
  2023/2023-11/20231104_183012_IMG_20231104_183012.jpg
  2023/2023-11/20231104_183012_IMG_20231104_183012.dng      sidecar, same prefix
  2016/2016-06 georgia/…                                    month folder with --event
  _screenshots/2023-11/…
  _whatsapp/2023-11/…
  _nonmedia/<dump>/<original path>                          books, docs, audio, Takeout JSON
  _nonmedia/<dump>/misc/…                                   extensions the rules don't know
  _undated/<dump>/<original path>                           media with no usable date
  _index.sqlite                                             catalog: files, duplicates, runs
  _manifest.sha256                                          `sha256sum -c` compatible
  _runs/<timestamp>_<dump>.log                              one tab-separated line per file
```

## Examples

Dry-run summary of a phone dump that had been dumped before:

```
actions:
     9647  duplicate
     9647  duplicate/Media
     3823  copy
     3610  copy/Media
      187  copy/NonMedia
       22  copy/Screenshots
        4  copy/WhatsApp
       14  skip
date sources (copied files):
     3388  copy/exif
      201  copy/filename
       21  copy/video
```

A run log line, tab-separated (action, bucket, date source, date, source, destination or matched file):

```
copy       Media   exif     2023-07-05 20:10:23  DCIM/Camera/IMG_20230705_201022.jpg  2023/2023-07/20230705_201023_IMG_20230705_201022.jpg
duplicate  Media   exif     2023-07-05 20:10:23  DCIM/Camera/copy/IMG_20230705_201022.jpg  2023/2023-07/20230705_201023_IMG_20230705_201022.jpg
skip       Skip                                  DCIM/.thumbnails/1483604902114.jpg   dir:.thumbnails
```

Why did this file go there?

```
$ photo-ingest probe VID_20201217_212129.mp4
  bucket=Media (ext:mp4) size=48213977
  metadata=2020-12-17 21:21:30 (Video)
  filename=2020-12-17 21:21:29
  mtime=2023-11-04 18:30:12
  chosen=2020-12-17 21:21:29 (Filename)
```

Weakly dated files worth a look:

```bash
photo-ingest report --source mtime
photo-ingest report --source folder
photo-ingest report --undated
```

## FAQ

**Why exact-bytes duplicates only?** Because it can never lose a distinct file.
Re-encoded copies (Google Photos, WhatsApp) get their own name with a `~1`
suffix; `report` and the catalog make them easy to find for a later pass.

**What if a run is interrupted?** Re-run it. Files already recorded are skipped
without hashing; a file copied but not yet recorded is recognised by content and
reused.

**Can I move files inside the archive?** Better not. `verify` will report them as
missing plus untracked, and the catalog keeps the old name. Promote into a
curated gallery by copying.

**Why .NET?** It was at hand on every machine involved, and single-file
self-contained binaries make installation a download.

**Does it change anything in the dump?** No. It opens source files read-only and
never deletes, moves or touches them. Removing a processed dump is up to you;
check the run log has no `error` lines and `verify` is clean first.

## Development

```bash
dotnet test tests/PhotoIngest.Tests          # 139 tests, xunit
dotnet run --project src/PhotoIngest -- help
```

Design notes, including the classification rules, date resolution order and
catalog schema, are in [docs/design.md](docs/design.md). See
[CONTRIBUTING.md](CONTRIBUTING.md) for how changes are made.

## License

[MIT](LICENSE)
