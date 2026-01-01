using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace RebelShipBrowser.Services
{
    /// <summary>
    /// Represents a userscript with Tampermonkey-compatible metadata
    /// </summary>
    public partial class UserScript
    {
        private readonly List<string> _match = new();
        private readonly List<string> _include = new();
        private readonly List<string> _exclude = new();

        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public bool IsBundled { get; set; }
        public string Name { get; set; } = "Unnamed Script";
        public string? Description { get; set; }
        public string? Version { get; set; }
        public string? Author { get; set; }
        public bool HasUpdate { get; set; }
        public string? RemoteVersion { get; set; }
        /// <summary>
        /// Display and load order (1-999, default 500)
        /// </summary>
        public int Order { get; set; } = 500;
        public Collection<string> Match => new(_match);
        public Collection<string> Include => new(_include);
        public Collection<string> Exclude => new(_exclude);
        public RunAt RunAt { get; set; } = RunAt.DocumentEnd;
        public bool Enabled { get; set; } = true;
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Sets the match patterns
        /// </summary>
        internal void SetMatch(IEnumerable<string> patterns)
        {
            _match.Clear();
            _match.AddRange(patterns);
        }

        /// <summary>
        /// Sets the include patterns
        /// </summary>
        internal void SetInclude(IEnumerable<string> patterns)
        {
            _include.Clear();
            _include.AddRange(patterns);
        }

        /// <summary>
        /// Sets the exclude patterns
        /// </summary>
        internal void SetExclude(IEnumerable<string> patterns)
        {
            _exclude.Clear();
            _exclude.AddRange(patterns);
        }

        /// <summary>
        /// Parses a userscript file and extracts metadata
        /// </summary>
        public static UserScript Parse(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            var script = new UserScript { FilePath = filePath };
            var content = File.ReadAllText(filePath);
            script.Code = content;

            // Extract metadata block
            var metaMatch = MetadataBlockRegex().Match(content);
            if (!metaMatch.Success)
            {
                script.Name = Path.GetFileNameWithoutExtension(filePath);
                return script;
            }

            var metaBlock = metaMatch.Groups[1].Value;

            // Parse individual metadata fields
            script.Name = ExtractMetaValue(metaBlock, "name") ?? Path.GetFileNameWithoutExtension(filePath);
            script.Description = ExtractMetaValue(metaBlock, "description");
            script.Version = ExtractMetaValue(metaBlock, "version");
            script.Author = ExtractMetaValue(metaBlock, "author");

            // Parse @match entries
            script.SetMatch(ExtractMetaValues(metaBlock, "match"));

            // Parse @include entries
            script.SetInclude(ExtractMetaValues(metaBlock, "include"));

            // Parse @exclude entries
            script.SetExclude(ExtractMetaValues(metaBlock, "exclude"));

            // Parse @run-at
            var runAt = ExtractMetaValue(metaBlock, "run-at");
            script.RunAt = runAt switch
            {
                not null when runAt.Equals("document-start", StringComparison.OrdinalIgnoreCase) => RunAt.DocumentStart,
                not null when runAt.Equals("document-idle", StringComparison.OrdinalIgnoreCase) => RunAt.DocumentIdle,
                _ => RunAt.DocumentEnd
            };

            // Parse @enabled (custom extension)
            var enabled = ExtractMetaValue(metaBlock, "enabled");
            script.Enabled = !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);

            // Parse @order (custom extension, 1-999, default 500)
            var orderStr = ExtractMetaValue(metaBlock, "order");
            if (int.TryParse(orderStr, out var order) && order >= 1 && order <= 999)
            {
                script.Order = order;
            }

            return script;
        }

        /// <summary>
        /// Generates the metadata block for saving
        /// </summary>
        public string GenerateMetadataBlock()
        {
            var lines = new List<string>
            {
                "// ==UserScript==",
                $"// @name        {Name}"
            };

            if (!string.IsNullOrEmpty(Description))
            {
                lines.Add($"// @description {Description}");
            }

            if (!string.IsNullOrEmpty(Version))
            {
                lines.Add($"// @version     {Version}");
            }

            if (!string.IsNullOrEmpty(Author))
            {
                lines.Add($"// @author      {Author}");
            }

            foreach (var match in _match)
            {
                lines.Add($"// @match       {match}");
            }

            foreach (var include in _include)
            {
                lines.Add($"// @include     {include}");
            }

            foreach (var exclude in _exclude)
            {
                lines.Add($"// @exclude     {exclude}");
            }

            var runAtValue = RunAt switch
            {
                RunAt.DocumentStart => "document-start",
                RunAt.DocumentIdle => "document-idle",
                _ => "document-end"
            };
            lines.Add($"// @run-at      {runAtValue}");

            if (!Enabled)
            {
                lines.Add("// @enabled     false");
            }

            if (Order != 500)
            {
                lines.Add($"// @order       {Order}");
            }

            lines.Add("// ==/UserScript==");

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Gets the script code without the metadata block
        /// </summary>
        public string GetCodeWithoutMetadata()
        {
            var match = MetadataBlockRegex().Match(Code);
            if (!match.Success)
            {
                return Code;
            }

            return Code.Substring(match.Index + match.Length).TrimStart('\r', '\n');
        }

        /// <summary>
        /// Checks if this script should run on the given URL
        /// </summary>
        public bool ShouldRunOnUrl(Uri url)
        {
            ArgumentNullException.ThrowIfNull(url);
            return ShouldRunOnUrlString(url.ToString());
        }

        /// <summary>
        /// Checks if this script should run on the given URL string
        /// </summary>
        internal bool ShouldRunOnUrlString(string url)
        {
            DebugLogger.Log($"[UserScript] Checking '{Name}' against URL: {url}");
            DebugLogger.Log($"[UserScript] Match patterns: {string.Join(", ", _match)}");

            // Check excludes first
            foreach (var pattern in _exclude)
            {
                if (MatchesPattern(url, pattern))
                {
                    DebugLogger.Log($"[UserScript] '{Name}' excluded by pattern: {pattern}");
                    return false;
                }
            }

            // Check matches
            foreach (var pattern in _match)
            {
                var matches = MatchesPattern(url, pattern);
                DebugLogger.Log($"[UserScript] Pattern '{pattern}' matches: {matches}");
                if (matches)
                {
                    return true;
                }
            }

            // Check includes
            foreach (var pattern in _include)
            {
                if (MatchesPattern(url, pattern))
                {
                    return true;
                }
            }

            // If no match/include patterns, don't run (unless explicitly for all)
            return _match.Count == 0 && _include.Count == 0;
        }

        private static bool MatchesPattern(string url, string pattern)
        {
            // Handle special patterns
            if (pattern == "*" || pattern == "*://*/*")
            {
                return true;
            }

            // Convert Tampermonkey pattern to regex
            // *://*.example.com/* -> matches http/https on any subdomain
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*", StringComparison.Ordinal)
                .Replace("\\?", ".", StringComparison.Ordinal)
                + "$";

            try
            {
                return Regex.IsMatch(url, regexPattern, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string? ExtractMetaValue(string metaBlock, string key)
        {
            var match = Regex.Match(metaBlock, $@"//\s*@{key}\s+(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private static List<string> ExtractMetaValues(string metaBlock, string key)
        {
            var values = new List<string>();
            var matches = Regex.Matches(metaBlock, $@"//\s*@{key}\s+(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                values.Add(match.Groups[1].Value.Trim());
            }
            return values;
        }

        [GeneratedRegex(@"//\s*==UserScript==(.*?)//\s*==/UserScript==", RegexOptions.Singleline)]
        private static partial Regex MetadataBlockRegex();
    }

    public enum RunAt
    {
        DocumentStart,
        DocumentEnd,
        DocumentIdle
    }
}
