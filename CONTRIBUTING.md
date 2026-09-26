# Contributing

Small tool, small process.

- **Bugs and ideas**: open an issue. For a misplaced or misdated file, include
  the output of `photo-ingest probe <file>` and the matching line from the run
  log; that is usually enough to reproduce.
- **Changes**: every behaviour change comes with a test in
  `tests/PhotoIngest.Tests` that fails before and passes after. The suite runs
  in a few seconds with `dotnet test tests/PhotoIngest.Tests`. CI runs it on
  every push and pull request.
- **Rules**: classification lives in `Rules.cs`, date parsing in `Dates.cs`,
  sidecar grouping in `Sidecars.cs`. New extensions or filename patterns go
  there, with a `[InlineData]` case each.
- **Safety first**: nothing may write outside the archive directory, and nothing
  may delete. A change that needs either is a design discussion, not a PR.
- **Style**: one static class per concern, no abstractions ahead of need.
  `.editorconfig` covers formatting.

Releases are cut by tagging `vX.Y.Z`; the release workflow builds single-file
binaries for Linux, Windows and macOS and attaches them to the GitHub release.
Add a line to `CHANGELOG.md` with the change.
