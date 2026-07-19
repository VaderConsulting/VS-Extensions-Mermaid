using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace MermaidDiagramExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(MermaidDiagramPackage.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(typeof(DiagramToolWindowPane), Style = VsDockStyle.Tabbed, Window = "3ae79031-e1bc-11d0-8f78-00a0c9110057")]
    public sealed class MermaidDiagramPackage : AsyncPackage
    {
        public const string PackageGuidString = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        private static readonly Guid PackageGuid = new Guid(PackageGuidString);

        private static MermaidDiagramPackage _Instance;

        public static IServiceProvider ServiceProvider => _Instance;

        protected override async Task InitializeAsync(CancellationToken CancellationToken, IProgress<ServiceProgressData> Progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken);
            _Instance = this;
            await MermaidDiagramCommands.InitializeAsync(this);
            MermaidRuntimeManager.StartBackgroundInit();
        }

        /// <summary>
        /// Returns the base path for the active Mermaid runtime (validated, in use).
        /// Prefers the managed cache (N); falls back to extension-bundled mermaid folder if no cache is ready yet.
        /// Never blocks; safe to call from UI thread during diagram generation.
        /// </summary>
        public static string GetMermaidBasePath()
        {
            string Active = MermaidRuntimeManager.GetActiveBasePath();
            if (!string.IsNullOrEmpty(Active) && System.IO.Directory.Exists(Active))
            {
                return Active;
            }

            string Legacy = GetLegacyExtensionMermaidPath();
            if (!string.IsNullOrEmpty(Legacy) && System.IO.File.Exists(System.IO.Path.Combine(Legacy, "viewer", "index.html")))
            {
                return Legacy;
            }

            return Active;
        }

        private static string GetLegacyExtensionMermaidPath()
        {
            string AssemblyPath = typeof(MermaidDiagramPackage).Assembly.Location;
            if (string.IsNullOrEmpty(AssemblyPath))
            {
                return null;
            }

            string Dir = System.IO.Path.GetDirectoryName(AssemblyPath);
            return System.IO.Path.Combine(Dir, "mermaid");
        }
    }
}
