using System.IO;
using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using RebelShipBrowser.Services;

namespace RebelShipBrowser
{
    public partial class ScriptEditorDialog : Window
    {
        private readonly UserScript _script;
        private readonly UserScriptService _scriptService;

        /// <summary>
        /// Indicates if the script was saved during this session
        /// </summary>
        public bool ScriptSaved { get; private set; }

        public ScriptEditorDialog(UserScript script, UserScriptService scriptService)
        {
            ArgumentNullException.ThrowIfNull(script);
            ArgumentNullException.ThrowIfNull(scriptService);

            InitializeComponent();
            _script = script;
            _scriptService = scriptService;

            LoadScript();
            SetupSyntaxHighlighting();

            Title = $"Edit Script - {script.Name}";
        }

        private void LoadScript()
        {
            // Load full script code including metadata block
            CodeEditor.Text = _script.Code;
            StatusText.Text = $"Editing: {_script.FileName}";
        }

        private void SetupSyntaxHighlighting()
        {
            var jsHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");

            if (jsHighlighting != null)
            {
                CodeEditor.SyntaxHighlighting = jsHighlighting;
            }
            else
            {
                CodeEditor.SyntaxHighlighting = CreateJavaScriptHighlighting();
            }
        }

        private static IHighlightingDefinition CreateJavaScriptHighlighting()
        {
            var xshd = @"
<SyntaxDefinition name=""JavaScript"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
    <Color name=""Comment"" foreground=""#6A9955"" />
    <Color name=""String"" foreground=""#CE9178"" />
    <Color name=""Keyword"" foreground=""#569CD6"" fontWeight=""bold"" />
    <Color name=""Number"" foreground=""#B5CEA8"" />
    <Color name=""Punctuation"" foreground=""#D4D4D4"" />

    <RuleSet>
        <Span color=""Comment"" begin=""//"" />
        <Span color=""Comment"" multiline=""true"" begin=""/\*"" end=""\*/"" />

        <Span color=""String"" begin=""&quot;"" end=""&quot;"" escape=""\\"" />
        <Span color=""String"" begin=""'"" end=""'"" escape=""\\"" />
        <Span color=""String"" begin=""`"" end=""`"" escape=""\\"" />

        <Keywords color=""Keyword"">
            <Word>async</Word>
            <Word>await</Word>
            <Word>break</Word>
            <Word>case</Word>
            <Word>catch</Word>
            <Word>class</Word>
            <Word>const</Word>
            <Word>continue</Word>
            <Word>debugger</Word>
            <Word>default</Word>
            <Word>delete</Word>
            <Word>do</Word>
            <Word>else</Word>
            <Word>export</Word>
            <Word>extends</Word>
            <Word>false</Word>
            <Word>finally</Word>
            <Word>for</Word>
            <Word>function</Word>
            <Word>if</Word>
            <Word>import</Word>
            <Word>in</Word>
            <Word>instanceof</Word>
            <Word>let</Word>
            <Word>new</Word>
            <Word>null</Word>
            <Word>of</Word>
            <Word>return</Word>
            <Word>static</Word>
            <Word>super</Word>
            <Word>switch</Word>
            <Word>this</Word>
            <Word>throw</Word>
            <Word>true</Word>
            <Word>try</Word>
            <Word>typeof</Word>
            <Word>undefined</Word>
            <Word>var</Word>
            <Word>void</Word>
            <Word>while</Word>
            <Word>with</Word>
            <Word>yield</Word>
        </Keywords>

        <Rule color=""Number"">
            \b\d+(\.\d+)?\b
        </Rule>
    </RuleSet>
</SyntaxDefinition>";

            using var reader = new XmlTextReader(new StringReader(xshd));
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save the full code as-is - metadata is parsed from the code
            File.WriteAllText(_script.FilePath, CodeEditor.Text);

            // Reload to parse metadata from the saved code
            _scriptService.LoadAllScripts();

            ScriptSaved = true;
            StatusText.Text = "Saved!";
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
