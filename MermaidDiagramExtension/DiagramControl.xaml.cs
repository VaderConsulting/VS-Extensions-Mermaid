using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace MermaidDiagramExtension
{
    public partial class DiagramControl : UserControl
    {
        private string _CurrentMermaid;
        private bool _IsFlow;
        private bool _SourceVisible;

        public DiagramControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object Sender, RoutedEventArgs E)
        {
            try
            {
                string BasePath = MermaidDiagramPackage.GetMermaidBasePath();
                if (string.IsNullOrEmpty(BasePath))
                {
                    ErrorText.Text = "Mermaid runtime is being prepared. Please try again in a moment.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }

                string HtmlPath = Path.Combine(BasePath, "viewer", "index.html");
                if (!File.Exists(HtmlPath))
                {
                    ErrorText.Text = "Could not load Mermaid viewer. Ensure viewer exists for the active runtime.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }

                await WebView.EnsureCoreWebView2Async(null);
                WebView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                WebView.Source = new Uri(HtmlPath);
            }
            catch (Exception)
            {
                ErrorText.Text = "Could not load Mermaid viewer.";
                ErrorText.Visibility = Visibility.Visible;
            }
        }

        private async void OnNavigationCompleted(object Sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs E)
        {
            if (!E.IsSuccess || string.IsNullOrEmpty(_CurrentMermaid))
            {
                return;
            }

            try
            {
                string Script = string.Format("renderMermaid({0}, {1});",
                    EscapeJsString(_CurrentMermaid),
                    _IsFlow ? "\"flowchart\"" : "\"sequence\"");
                await WebView.ExecuteScriptAsync(Script);
            }
            catch (Exception)
            {
            }
        }

        public void SetDiagram(string Mermaid, bool IsFlow)
        {
            _CurrentMermaid = Mermaid ?? string.Empty;
            _IsFlow = IsFlow;
            ErrorText.Visibility = Visibility.Collapsed;
            SourceTextBox.Text = _CurrentMermaid;
            if (SourceRow.Height.GridUnitType == GridUnitType.Auto && _SourceVisible)
            {
                SourcePanel.Visibility = Visibility.Visible;
            }

            try
            {
                if (WebView.CoreWebView2 != null)
                {
                    string Script = string.Format("renderMermaid({0}, {1});",
                        EscapeJsString(_CurrentMermaid),
                        IsFlow ? "\"flowchart\"" : "\"sequence\"");
                    WebView.ExecuteScriptAsync(Script);
                }
            }
            catch (Exception)
            {
            }
        }

        public void ShowError(string Message)
        {
            _CurrentMermaid = null;
            ErrorText.Text = Message ?? "Unable to generate diagram from the selected code.";
            ErrorText.Visibility = Visibility.Visible;
            SourceTextBox.Text = string.Empty;
            SourcePanel.Visibility = Visibility.Collapsed;
        }

        private static string EscapeJsString(string Value)
        {
            if (Value == null)
            {
                return "\"\"";
            }

            return "\"" + Value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        private void OnRefresh(object Sender, RoutedEventArgs E)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            MermaidDiagramCommands.RegenerateDiagram(_IsFlow);
        }

        private void OnCopyMermaid(object Sender, RoutedEventArgs E)
        {
            try
            {
                Clipboard.SetText(_CurrentMermaid ?? string.Empty);
            }
            catch (Exception)
            {
            }
        }

        private void OnShowSource(object Sender, RoutedEventArgs E)
        {
            _SourceVisible = !_SourceVisible;
            SourcePanel.Visibility = _SourceVisible ? Visibility.Visible : Visibility.Collapsed;
            if (_SourceVisible)
            {
                SourceTextBox.Text = _CurrentMermaid ?? string.Empty;
            }
        }

        private void OnFitDiagram(object Sender, RoutedEventArgs E)
        {
            try
            {
                WebView.ExecuteScriptAsync("fitDiagram();");
            }
            catch (Exception)
            {
            }
        }

        private async void OnExportSvg(object Sender, RoutedEventArgs E)
        {
            try
            {
                string Svg = await WebView.ExecuteScriptAsync("(function(){ var s = getSvgData(); return s ? s : ''; })()");
                if (string.IsNullOrEmpty(Svg) || Svg == "null")
                {
                    return;
                }

                string Unwrapped = UnwrapJsonString(Svg);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                string Path = PromptSaveFile("SVG files (*.svg)|*.svg", "diagram.svg");
                if (Path != null)
                {
                    File.WriteAllText(Path, Unwrapped, Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
        }

        private async void OnExportPng(object Sender, RoutedEventArgs E)
        {
            try
            {
                string DataUrl = await WebView.ExecuteScriptAsync("(function(){ return getPngDataUrlAsync ? getPngDataUrlAsync() : Promise.resolve(''); })()");
                if (string.IsNullOrEmpty(DataUrl) || DataUrl == "null")
                {
                    return;
                }

                string Unwrapped = UnwrapJsonString(DataUrl);
                if (!Unwrapped.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                int Comma = Unwrapped.IndexOf(',');
                if (Comma < 0)
                {
                    return;
                }

                byte[] Bytes = Convert.FromBase64String(Unwrapped.Substring(Comma + 1));
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                string Path = PromptSaveFile("PNG files (*.png)|*.png", "diagram.png");
                if (Path != null)
                {
                    File.WriteAllBytes(Path, Bytes);
                }
            }
            catch (Exception)
            {
            }
        }

        private static string UnwrapJsonString(string Json)
        {
            if (string.IsNullOrEmpty(Json) || Json == "null")
            {
                return string.Empty;
            }

            string Trimmed = Json.Trim();
            if (Trimmed.Length >= 2 && Trimmed[0] == '"' && Trimmed[Trimmed.Length - 1] == '"')
            {
                return Trimmed.Substring(1, Trimmed.Length - 2).Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\\", "\\");
            }

            return Json;
        }

        private static string PromptSaveFile(string Filter, string DefaultName)
        {
            Microsoft.Win32.SaveFileDialog Dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = Filter,
                FileName = DefaultName
            };

            return Dialog.ShowDialog() == true ? Dialog.FileName : null;
        }
    }
}
