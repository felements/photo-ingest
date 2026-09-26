namespace PhotoIngest;

public sealed class OptionsException : Exception
{
    public OptionsException(string message) : base(message) { }
}

public sealed class Options
{
    public string Command = "";
    public List<string> Positional = new();
    public string Archive = DefaultArchive;
    public string? Event;
    public bool DryRun;
    public bool Undated;
    public string? Source;
    public bool Help;

    public static readonly string DefaultArchive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures", "archive");

    public static Options Parse(string[] a)
    {
        var o = new Options { Command = a[0] };
        if (o.Command is "--help" or "-h") { o.Command = "help"; o.Help = true; }
        if (o.Command is "--version" or "-V") o.Command = "version";
        for (int i = 1; i < a.Length; i++)
        {
            string Value() => i + 1 < a.Length ? a[++i] : throw new OptionsException($"option {a[i]} needs a value");
            switch (a[i])
            {
                case "--archive": o.Archive = Value(); break;
                case "--event": o.Event = Value(); break;
                case "--source": o.Source = Value(); break;
                case "--dry-run": o.DryRun = true; break;
                case "--undated": o.Undated = true; break;
                case "--help": case "-h": o.Help = true; break;
                default:
                    if (a[i].StartsWith('-') && a[i].Length > 1) throw new OptionsException($"unknown option {a[i]}");
                    o.Positional.Add(a[i]); break;
            }
        }
        return o;
    }
}
