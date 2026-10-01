using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RoguePirates.V4.Presentation.Hud;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// Owner rule (Rogue Pirates UI text): no em dashes in in-game text; a colon or a comma instead. String literals in
    /// the game's code (not comments, tooltips, inspector headers or placeholder-art notes) and the text fields of the
    /// data are clean, and the toast and narrative displays clean whatever text reaches them.
    /// </summary>
    public sealed class V5UiTextTests
    {
        const char Dash = '\u2014';
        static readonly Regex Literal = new Regex("\\$?\"(?:[^\"\\\\]|\\\\.)*\"");

        [Test]
        public void GameCodeStrings_HaveNoEmDashes()
        {
            var root = Path.Combine(Application.dataPath, "_Game", "V4");
            var skipFolders = new[] { "DevTools", "Editor" };
            var found = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !skipFolders.Any(s => f.Replace('\\', '/').Contains("/" + s + "/")))
                .Where(f => !f.EndsWith("PlaceholderFactory.cs") && !f.EndsWith("DemoMode.cs"))
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(x => x.line.IndexOf(Dash) >= 0)
                .Where(x =>
                {
                    var t = x.line.TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("[Header")) return false;
                    var code = Regex.Replace(x.line, "\\[Tooltip\\(\"(?:[^\"\\\\]|\\\\.)*\"\\)\\]", "");
                    return Literal.Matches(code).Cast<Match>().Any(m => m.Value.IndexOf(Dash) >= 0);
                })
                .Select(x => Path.GetFileName(x.f) + ":" + (x.i + 1) + ": " + x.line.Trim()).ToList();
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void TheShownDataTexts_HaveNoEmDashes()
        {
            var data = Path.Combine(Application.dataPath, "_Game", "V4", "Data");
            var fields = new Regex("^\\s*(prompt|toastText|startToast|edgeToast|edgeWallToast|warningToast|template|label|line|text):", RegexOptions.Multiline);
            var found = Directory.GetFiles(data, "*.asset", SearchOption.AllDirectories)
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(x => fields.IsMatch(x.line) && (x.line.Contains("\\u2014") || x.line.IndexOf(Dash) >= 0))
                .Select(x => Path.GetFileName(x.f) + ":" + (x.i + 1) + ": " + x.line.Trim()).ToList();
            CollectionAssert.IsEmpty(found);
        }

        [Test]
        public void TheToast_CleansWhatItShows()
        {
            var canvas = new GameObject("canvas", typeof(RectTransform));
            try
            {
                var toast = ToastPresenter.Create(canvas.transform);
                toast.Show("Barnacle King \u2014 charge incoming", 1f);
                Assert.AreEqual("Barnacle King: charge incoming", toast.Text);
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
