using PhotoIngest;

return Cli.Run(args, Console.Out, Console.Error);

namespace PhotoIngest
{
    public static class Cli
    {
        static readonly string[] Commands = { "ingest", "report", "verify", "probe", "help", "version" };

        public static string Version =>
            typeof(Cli).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0]
            ?? typeof(Cli).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        public static int Run(string[] args) => Run(args, Console.Out, Console.Error);

        public static int Run(string[] args, TextWriter out_, TextWriter err)
        {
            if (args.Length == 0) { Usage(err); return 2; }
            Options o;
            try { o = Options.Parse(args); }
            catch (OptionsException e) { err.WriteLine(e.Message); Usage(err); return 2; }

            if (o.Command == "version") { out_.WriteLine($"photo-ingest {Version}"); return 0; }
            if (o.Command == "help")
            {
                var topic = o.Positional.FirstOrDefault();
                if (topic is not null && !Commands.Contains(topic)) { err.WriteLine($"unknown command {topic}"); Usage(err); return 2; }
                out_.Write(HelpText(topic)); return 0;
            }
            if (!Commands.Contains(o.Command)) { err.WriteLine($"unknown command {o.Command}"); Usage(err); return 2; }
            if (o.Help) { out_.Write(HelpText(o.Command)); return 0; }

            var live = ReferenceEquals(err, Console.Error) && !Console.IsErrorRedirected;
            switch (o.Command)
            {
                case "ingest": using (var progress = Progress.Create(err, live)) return Ingest.Run(o, progress);
                case "report": return Report.Run(o);
                case "verify": using (var progress = Progress.Create(err, live)) return Verify.Run(o, progress);
                case "probe": return Probe.Run(o, out_, err);
                default: Usage(err); return 2;
            }
        }

        public static void Usage() => Usage(Console.Error);

        public static void Usage(TextWriter w) => w.WriteLine("""
            photo-ingest: sort device photo dumps into a date archive, skipping files already there.

            usage: photo-ingest <command> [options]
              ingest <dump-dir> [--archive DIR] [--event NAME] [--dry-run]
              report [--archive DIR] [--undated] [--source exif|video|sidecar|filename|folder|mtime|none]
              verify [--archive DIR]
              probe <file>...
              help [command]        (also: --help, -h)
              version               (also: --version)
            """);

        public static string HelpText(string? command) => command switch
        {
            "ingest" => IngestHelp,
            "report" => ReportHelp,
            "verify" => VerifyHelp,
            "probe" => ProbeHelp,
            "help" => GeneralHelp,
            _ => GeneralHelp,
        };

        static readonly string Archive = "--archive DIR   archive root (default: " + Options.DefaultArchive + ")";

        static readonly string GeneralHelp = $"""
            photo-ingest merges one device dump (phone, camera card, Google Takeout export)
            into a date-based photo archive. It only ever reads the dump; it writes only
            under the archive directory. It never deletes anything, anywhere.

            what it does, per dump
              1. Walks the dump and sorts every file into a bucket: media, screenshots,
                 WhatsApp, non-media (books, documents, audio, JSON) or junk to skip.
              2. Groups sidecars (DNG, PP3, THM, Motion Photo) with the photo they belong to.
              3. Works out when each photo was taken: EXIF, video header, Takeout JSON,
                 the filename, a dated folder name, then file mtime as the last resort.
              4. Hashes every file with SHA-256 and looks it up in the archive's catalog;
                 a file with the same bytes already archived is logged as a duplicate,
                 not copied. Successive dumps of the same phone mostly dedupe away.
              5. Copies the rest into YYYY/YYYY-MM/YYYYMMDD_HHMMSS_<name>, verifying each
                 copy's hash before recording it. Sidecars land next to their photo.
              6. Records every decision in _index.sqlite and a tab-separated run log, and
                 rewrites _manifest.sha256 so the archive can be checked with verify.
              Run it with --dry-run first to see what would happen without writing anything.

            usage: photo-ingest <command> [options]

            commands
              ingest <dump-dir>   copy new files from a dump into the archive (use --dry-run first)
              report              counts per month, bucket, dump and date source
              verify              re-hash every archived file against the catalog
              probe <file>...     show how single files would be classified and dated
              help [command]      this text, or details for one command
              version             print the version

            common option
              {Archive}

            archive layout
              YYYY/YYYY-MM/YYYYMMDD_HHMMSS_<original name>   photos and videos, sidecars next to their primary
              YYYY/YYYY-MM <event>/                          same, when ingest ran with --event
              _screenshots/YYYY-MM/                          screenshots and screen recordings
              _whatsapp/YYYY-MM/                             WhatsApp media
              _nonmedia/<dump>/<original path>               books, documents, audio, Takeout JSON
              _undated/<dump>/<original path>                media with no usable date at all
              _index.sqlite                                  catalog: every file, duplicate and run
              _manifest.sha256                               `sha256sum -c` list of all archived files
              _runs/<timestamp>_<dump>.log                   one tab-separated line per decision

            date sources, tried in order
              exif, video container (or the filename's time for videos), Google Takeout JSON,
              filename, dated folder name, file mtime (if after 2000). Files with none go to _undated.

            duplicates
              A file whose SHA-256 is already in the catalog is not copied; the log names the
              archived file it matches. Re-encoded or resized copies are different bytes and are kept.

            Run `photo-ingest help <command>` for details.

            """;

        static readonly string IngestHelp = $"""
            usage: photo-ingest ingest <dump-dir> [--archive DIR] [--event NAME] [--dry-run]

            Walks <dump-dir>, classifies every file, resolves a capture date, hashes it, and
            copies files not yet in the catalog into the archive. The dump folder's name is
            recorded as the dump name, so give it a meaningful name first (e.g. 2026.10-poco-x3).

            options
              {Archive}
              --event NAME    suffix for the month folder of every media file in this run,
                              e.g. --event georgia gives 2016/2016-06 georgia/. Use only for a
                              folder that is a single trip or event.
              --dry-run       do everything except copying and catalog writes; prints counts
                              per action and bucket and the first 50 planned destinations per
                              bucket. Writes only a _dry.log under _runs/.

            what happens to each file (first matching rule wins)
              skip        trash and thumbnail dirs, .nomedia, zero-byte and temp files
              nonmedia    books, documents, audio, archives, JSON, and anything under Books/,
                          Documents/, Download/, Android/
              screenshots Screenshots/ or ScreenRecorder/ dirs, names starting with Screenshot
              whatsapp    WhatsApp*/ dirs, IMG-YYYYMMDD-WA… names
              media       image, video and RAW extensions plus sidecars (dng, pp3, thm, xmp, mp)
              misc        anything else, parked under _nonmedia/<dump>/misc/ and worth a look

            Copies go through a .part file, are re-hashed, then moved into place and recorded.
            An interrupted run can simply be re-run; finished files are skipped.

            Progress bars for walking, dating, hashing and copying are shown on stderr when it
            is a terminal; when stderr is redirected, one summary line per phase is printed.

            exit codes: 0 ok, 1 aborted (see the log's last line), 2 usage error

            """;

        static readonly string ReportHelp = $"""
            usage: photo-ingest report [--archive DIR] [--undated] [--source SRC]

            Without flags: files per month and bucket, parked non-media totals, files per
            date source, and files plus duplicates per dump.

            options
              {Archive}
              --undated       list the files that landed in _undated/
              --source SRC    list files whose date came from SRC:
                              exif, video, sidecar, filename, folder, mtime, none.
                              folder and mtime are the weak ones worth reviewing.

            """;

        static readonly string VerifyHelp = $"""
            usage: photo-ingest verify [--archive DIR]

            Re-hashes every file the catalog knows and walks the archive for strays.
            Prints one line per problem, tagged missing, changed or untracked, then a summary.
            Shows a progress bar on a terminal; one line per phase when stderr is redirected.

            options
              {Archive}

            exit codes: 0 clean, 1 problems found, 2 no catalog at that path

            Note: do not move or rename files inside the archive by hand; verify will then
            report them as missing plus untracked. Promote into a curated gallery by copying.

            """;

        static readonly string ProbeHelp = """
            usage: photo-ingest probe <file>...

            For each file prints its bucket and the rule that chose it, the embedded metadata
            date, the filename date, the mtime, and which one ingest would use. Reads only.

            """;
    }
}
