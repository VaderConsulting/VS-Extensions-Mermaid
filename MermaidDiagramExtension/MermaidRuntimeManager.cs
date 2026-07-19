using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MermaidDiagramExtension
{
    /// <summary>
    /// Manages Mermaid runtime acquisition, updates, validation, and retention (N and N-1 only).
    /// All discovery, download, validation, and cleanup run on background threads; never blocks UI or startup.
    /// </summary>
    internal sealed class MermaidRuntimeManager
    {
        private const string GITHUB_RELEASES_LATEST = "https://api.github.com/repos/mermaid-js/mermaid/releases/latest";
        private const string CDN_TEMPLATE = "https://cdn.jsdelivr.net/npm/mermaid@{0}/dist/mermaid.min.js";
        private const string MERMAID_ROOT_FOLDER = "Mermaid";
        private const string EXTENSION_DATA_FOLDER = "MermaidDiagramExtension";
        private const int MIN_VALID_FILE_SIZE = 1000;
        private const string MERMAID_SIGNATURE = "mermaid";

        private static readonly object _Lock = new object();
        private static string _ActiveBasePath;
        private static bool _Initialized;
        private static Task _BackgroundInitTask;

        /// <summary>
        /// Gets the base path of the currently active (validated) Mermaid runtime, or null if none is ready.
        /// Callable from any thread; never blocks. Diagram generation uses this path.
        /// </summary>
        public static string GetActiveBasePath()
        {
            lock (_Lock)
            {
                return _ActiveBasePath;
            }
        }

        /// <summary>
        /// Ensures a Mermaid runtime is available and checks for updates. Must be run on a background thread.
        /// Does not block the UI or startup.
        /// </summary>
        public static async Task EnsureReadyAsync()
        {
            await Task.Run(async () =>
            {
                try
                {
                    string ExtensionViewerPath = GetExtensionViewerTemplatePath();
                    string MermaidRoot = GetMermaidCacheRoot();
                    if (string.IsNullOrEmpty(MermaidRoot) || string.IsNullOrEmpty(ExtensionViewerPath))
                    {
                        Log("MermaidRuntimeManager: Cannot determine cache or viewer path.");
                        return;
                    }

                    if (!Directory.Exists(MermaidRoot))
                    {
                        Directory.CreateDirectory(MermaidRoot);
                    }

                    string[] VersionDirs = GetVersionDirectories(MermaidRoot);
                    string CurrentActive = GetActiveBasePath();
                    bool HasValidLocal = VersionDirs.Length > 0 && !string.IsNullOrEmpty(CurrentActive) && Directory.Exists(CurrentActive);

                    if (!HasValidLocal)
                    {
                        Log("First run: no local Mermaid runtime. Downloading latest.");
                        string LatestVersion = await FetchLatestVersionAsync();
                        if (string.IsNullOrEmpty(LatestVersion))
                        {
                            Log("First run: could not fetch latest version. Diagram rendering unavailable.");
                            return;
                        }
                        string Installed = await DownloadValidateAndInstallAsync(LatestVersion, MermaidRoot, ExtensionViewerPath);
                        if (!string.IsNullOrEmpty(Installed))
                        {
                            SetActiveBasePath(Installed);
                            Log("First runtime download complete; activated version " + LatestVersion);
                        }
                        return;
                    }

                    Log("Startup update check.");
                    string RemoteVersion = await FetchLatestVersionAsync();
                    if (string.IsNullOrEmpty(RemoteVersion))
                    {
                        Log("Update check: could not reach official source. Continuing with cached version.");
                        return;
                    }

                    string LocalVersion = GetVersionFromPath(CurrentActive);
                    if (string.Equals(LocalVersion, RemoteVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        Log("Update check: local version equals N. No update required.");
                        PruneOldVersions(MermaidRoot);
                        return;
                    }

                    if (!IsNewerVersion(RemoteVersion, LocalVersion))
                    {
                        Log("Update check: local version is not older than remote. No update.");
                        PruneOldVersions(MermaidRoot);
                        return;
                    }

                    Log("New version available: " + RemoteVersion);
                    string NewPath = await DownloadValidateAndInstallAsync(RemoteVersion, MermaidRoot, ExtensionViewerPath);
                    if (string.IsNullOrEmpty(NewPath))
                    {
                        Log("New version download or validation failed. Keeping existing active version.");
                        return;
                    }

                    SetActiveBasePath(NewPath);
                    Log("Runtime activated: " + RemoteVersion);
                    PruneOldVersions(MermaidRoot);
                }
                catch (Exception Ex)
                {
                    Log("MermaidRuntimeManager error: " + Ex.Message);
                }
            });
        }

        /// <summary>
        /// Called from package startup without await. Schedules background ensure-ready and update check.
        /// </summary>
        public static void StartBackgroundInit()
        {
            if (_Initialized)
            {
                return;
            }

            lock (_Lock)
            {
                if (_Initialized)
                {
                    return;
                }

                _Initialized = true;
            }

            _BackgroundInitTask = Task.Run(async () =>
            {
                await Task.Delay(2000);
                await EnsureReadyAsync();
            });
        }

        private static string GetMermaidCacheRoot()
        {
            try
            {
                string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(LocalAppData))
                {
                    return null;
                }

                return Path.Combine(LocalAppData, EXTENSION_DATA_FOLDER, MERMAID_ROOT_FOLDER);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string GetExtensionViewerTemplatePath()
        {
            try
            {
                string AssemblyPath = typeof(MermaidRuntimeManager).Assembly.Location;
                if (string.IsNullOrEmpty(AssemblyPath))
                {
                    return null;
                }

                string Dir = Path.GetDirectoryName(AssemblyPath);
                return Path.Combine(Dir, "mermaid", "viewer");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<string> FetchLatestVersionAsync()
        {
            try
            {
                using (WebClient Client = new WebClient())
                {
                    Client.Headers.Add("User-Agent", "MermaidDiagramExtension/1.0");
                    string Json = await Client.DownloadStringTaskAsync(GITHUB_RELEASES_LATEST);
                    int TagStart = Json.IndexOf("\"tag_name\"", StringComparison.OrdinalIgnoreCase);
                    if (TagStart < 0)
                    {
                        return null;
                    }

                    int ValueStart = Json.IndexOf(':', TagStart) + 1;
                    int QuoteStart = Json.IndexOf('"', ValueStart) + 1;
                    int QuoteEnd = Json.IndexOf('"', QuoteStart);
                    if (QuoteEnd <= QuoteStart)
                    {
                        return null;
                    }

                    string Tag = Json.Substring(QuoteStart, QuoteEnd - QuoteStart).Trim();
                    if (Tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        Tag = Tag.Substring(1);
                    }

                    return Tag;
                }
            }
            catch (Exception Ex)
            {
                Log("Fetch latest version failed: " + Ex.Message);
                return null;
            }
        }

        private static async Task<string> DownloadValidateAndInstallAsync(string Version, string MermaidRoot, string ExtensionViewerPath)
        {
            string VersionDir = Path.Combine(MermaidRoot, Version);
            if (Directory.Exists(VersionDir))
            {
                if (ValidateVersionDirectory(VersionDir))
                {
                    return VersionDir;
                }

                try
                {
                    Directory.Delete(VersionDir, true);
                }
                catch (Exception)
                {
                }
            }

            string TempFile = Path.Combine(Path.GetTempPath(), "mermaid-" + Version + "-" + Guid.NewGuid().ToString("N") + ".js");
            try
            {
                string Url = string.Format(CDN_TEMPLATE, Version);
                using (WebClient Client = new WebClient())
                {
                    Client.Headers.Add("User-Agent", "MermaidDiagramExtension/1.0");
                    await Client.DownloadFileTaskAsync(Url, TempFile);
                }

                if (!File.Exists(TempFile) || new FileInfo(TempFile).Length < MIN_VALID_FILE_SIZE)
                {
                    Log("Validation failed: file too small or missing.");
                    return null;
                }

                string Content = File.ReadAllText(TempFile, Encoding.UTF8);
                if (Content.IndexOf(MERMAID_SIGNATURE, StringComparison.Ordinal) < 0)
                {
                    Log("Validation failed: file integrity check failed.");
                    return null;
                }

                Directory.CreateDirectory(VersionDir);
                string DestJs = Path.Combine(VersionDir, "mermaid.min.js");
                File.Copy(TempFile, DestJs, true);

                string ViewerDest = Path.Combine(VersionDir, "viewer");
                if (Directory.Exists(ExtensionViewerPath))
                {
                    CopyViewerTemplate(ExtensionViewerPath, ViewerDest);
                }

                WriteManifest(VersionDir, Version);
                Log("Runtime downloaded and validated: " + Version);
                return VersionDir;
            }
            catch (Exception Ex)
            {
                Log("Download or validation failed: " + Ex.Message);
                return null;
            }
            finally
            {
                try
                {
                    if (File.Exists(TempFile))
                    {
                        File.Delete(TempFile);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private static void CopyViewerTemplate(string SourceViewerDir, string DestViewerDir)
        {
            if (!Directory.Exists(DestViewerDir))
            {
                Directory.CreateDirectory(DestViewerDir);
            }

            foreach (string FilePath in Directory.GetFiles(SourceViewerDir))
            {
                string FileName = Path.GetFileName(FilePath);
                string DestPath = Path.Combine(DestViewerDir, FileName);
                File.Copy(FilePath, DestPath, true);
            }
        }

        private static void WriteManifest(string VersionDir, string Version)
        {
            string ManifestPath = Path.Combine(VersionDir, "manifest.json");
            string Json = "{\"version\":\"" + Version.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            File.WriteAllText(ManifestPath, Json, Encoding.UTF8);
        }

        private static bool ValidateVersionDirectory(string VersionDir)
        {
            string JsPath = Path.Combine(VersionDir, "mermaid.min.js");
            string ViewerPath = Path.Combine(VersionDir, "viewer", "index.html");
            if (!File.Exists(JsPath) || !File.Exists(ViewerPath))
            {
                return false;
            }

            if (new FileInfo(JsPath).Length < MIN_VALID_FILE_SIZE)
            {
                return false;
            }

            return true;
        }

        private static string GetVersionFromPath(string BasePath)
        {
            if (string.IsNullOrEmpty(BasePath))
            {
                return null;
            }

            string DirName = Path.GetFileName(BasePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrEmpty(DirName) ? null : DirName;
        }

        private static string[] GetVersionDirectories(string MermaidRoot)
        {
            if (!Directory.Exists(MermaidRoot))
            {
                return new string[0];
            }

            return Directory.GetDirectories(MermaidRoot)
                .Where(D => File.Exists(Path.Combine(D, "mermaid.min.js")))
                .OrderByDescending(D => Path.GetFileName(D), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void PruneOldVersions(string MermaidRoot)
        {
            string[] VersionDirs = GetVersionDirectories(MermaidRoot);
            if (VersionDirs.Length <= 2)
            {
                return;
            }

            foreach (string Dir in VersionDirs.Skip(2))
            {
                try
                {
                    Directory.Delete(Dir, true);
                    Log("Older version deleted: " + Path.GetFileName(Dir));
                }
                catch (Exception Ex)
                {
                    Log("Could not delete old version: " + Ex.Message);
                }
            }
        }

        private static bool IsNewerVersion(string Remote, string Local)
        {
            if (string.IsNullOrEmpty(Local))
            {
                return true;
            }

            if (string.IsNullOrEmpty(Remote))
            {
                return false;
            }

            int[] RemoteParts = ParseVersionParts(Remote);
            int[] LocalParts = ParseVersionParts(Local);
            int MaxLen = Math.Max(RemoteParts.Length, LocalParts.Length);
            for (int I = 0; I < MaxLen; I++)
            {
                int R = I < RemoteParts.Length ? RemoteParts[I] : 0;
                int L = I < LocalParts.Length ? LocalParts[I] : 0;
                if (R > L)
                {
                    return true;
                }

                if (R < L)
                {
                    return false;
                }
            }

            return false;
        }

        private static int[] ParseVersionParts(string Version)
        {
            List<int> Parts = new List<int>();
            foreach (string Part in Version.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string Digits = new string(Part.TakeWhile(C => char.IsDigit(C)).ToArray());
                if (Digits.Length > 0 && int.TryParse(Digits, out int N))
                {
                    Parts.Add(N);
                }
            }

            return Parts.ToArray();
        }

        private static void SetActiveBasePath(string Path)
        {
            lock (_Lock)
            {
                _ActiveBasePath = Path;
            }
        }

        private static void Log(string Message)
        {
            string Line = "[MermaidDiagram] " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " " + Message;
            System.Diagnostics.Debug.WriteLine(Line);
            try
            {
                string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), EXTENSION_DATA_FOLDER);
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }

                string LogFile = Path.Combine(LogDir, "mermaid.log");
                File.AppendAllText(LogFile, Line + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
            }
        }
    }
}
