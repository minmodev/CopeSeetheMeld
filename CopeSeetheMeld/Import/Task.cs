using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CopeSeetheMeld.Import;

public partial class Import(string input) : AutoTask
{
    private readonly HttpClient client = new();
    private readonly JsonSerializerOptions jop = new() { IncludeFields = true };

    [GeneratedRegex(@"https?:\/\/etro\.gg\/gearset\/([^/]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternEtro();

    // accepts page=sl| anywhere in the query string, or the api shortlink URL
    [GeneratedRegex(@"(?:https?:\/\/xivgear\.app\/\?(?:[^|#\s]*?&)?page=sl\||https?:\/\/api\.xivgear\.app\/shortlink\/)([a-zA-Z0-9-]+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternXIVG();

    [GeneratedRegex(@"[?&](?:selectedIndex|onlySetIndex)=(\d+)", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex PatternXIVGIndex();

    protected override async Task Execute()
    {
        using var _ = new OnDispose(Plugin.Config.Save);

        // teamcraft export is just markdown
        if (input.StartsWith("**"))
        {
            Status = "Importing from TC";
            ImportTeamcraft(input);
            return;
        }

        // raw xivgear JSON pasted directly (check before unescaping so we don't mangle it)
        if (input.TrimStart().StartsWith('{'))
        {
            Status = "Importing xivgear JSON";
            ImportXIVGJson(input, -1);
            return;
        }

        input = Uri.UnescapeDataString(input);

        var m1 = PatternEtro().Match(input);
        if (m1.Success)
        {
            Status = "Importing from Etro";
            await ImportEtro(m1.Groups[1].Value);
            return;
        }

        var m2 = PatternXIVG().Match(input);
        if (m2.Success)
        {
            Status = "Importing from xivgear";
            var idx = PatternXIVGIndex().Match(input);
            await ImportXIVG(m2.Groups[1].Value, idx.Success ? idx.Groups[1].Value : "");
            return;
        }

        Error("Unrecognized input");
    }
}
