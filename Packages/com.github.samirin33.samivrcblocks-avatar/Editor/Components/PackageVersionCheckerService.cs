using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using Samirin33.NDMF.Components;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Samirin33.NDMF.Components.Editor
{
    /// <summary>
    /// PackageVersionChecker のバージョン照合・警告・自動修正。
    /// </summary>
    public static class PackageVersionCheckerService
    {
        private const string SessionWarnedPrefix = "Samirin33.PackageVersionChecker.Warned.";
        private const string VpmManifestRelativePath = "Packages/vpm-manifest.json";

        private static readonly HashSet<int> PendingInstanceIds = new HashSet<int>();
        private static readonly Dictionary<int, double> LastScheduledTimeByInstanceId = new Dictionary<int, double>();
        private const double CheckDebounceSeconds = 2.0;
        private static bool _scheduled;
        private static bool _pendingForceDialog;

        public sealed class Mismatch
        {
            public string PackageId;
            public string DisplayName;
            public string RequiredVersion;
            public string InstalledVersion;
            public bool IsMissing;
        }

        public static void ScheduleCheck(PackageVersionChecker checker, bool forceDialog = false)
        {
            if (checker == null)
                return;

            var id = checker.GetInstanceID();
            var now = EditorApplication.timeSinceStartup;
            if (LastScheduledTimeByInstanceId.TryGetValue(id, out var last)
                && now - last < CheckDebounceSeconds)
            {
                return;
            }

            LastScheduledTimeByInstanceId[id] = now;

            // 手動チェックは即時実行（delayCall だとダイアログが出ない／失われることがある）
            if (forceDialog && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                CheckAndWarn(checker, forceDialog: true, showSatisfiedDialog: false);
                return;
            }

            if (forceDialog)
                _pendingForceDialog = true;

            PendingInstanceIds.Add(id);
            if (_scheduled)
                return;

            _scheduled = true;
            EditorApplication.delayCall += FlushWhenReady;
        }

        /// <summary>
        /// 配置された GameObject 配下の PackageVersionChecker を照合する。
        /// </summary>
        public static void CheckPlacedHierarchy(GameObject root, bool forceDialog = true)
        {
            if (root == null)
                return;

            var checkers = root.GetComponentsInChildren<PackageVersionChecker>(true);
            if (checkers == null || checkers.Length == 0)
            {
                Debug.Log(
                    $"[PackageVersionChecker] 配置オブジェクト「{root.name}」配下に PackageVersionChecker がありません。");
                return;
            }

            foreach (var checker in checkers)
                ScheduleCheck(checker, forceDialog);
        }

        private static void FlushWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += FlushWhenReady;
                return;
            }

            var forceDialog = _pendingForceDialog;
            _pendingForceDialog = false;
            _scheduled = false;
            ScheduleFlush(forceDialog);
        }

        private static void ScheduleFlush(bool forceDialog)
        {
            var ids = PendingInstanceIds.ToArray();
            PendingInstanceIds.Clear();

            foreach (var id in ids)
            {
                var checker = EditorUtility.InstanceIDToObject(id) as PackageVersionChecker;
                if (checker == null)
                    continue;
                CheckAndWarn(checker, forceDialog);
            }
        }

        public static List<Mismatch> CollectMismatches(PackageVersionChecker checker)
        {
            var result = new List<Mismatch>();
            if (checker == null || checker.requirements == null)
                return result;

            foreach (var req in checker.requirements)
            {
                if (req == null || string.IsNullOrWhiteSpace(req.packageId) || string.IsNullOrWhiteSpace(req.minVersion))
                    continue;

                var packageId = req.packageId.Trim();
                var required = req.minVersion.Trim();
                var installed = GetInstalledVersion(packageId);
                var display = string.IsNullOrWhiteSpace(req.displayName) ? packageId : req.displayName.Trim();

                if (string.IsNullOrEmpty(installed))
                {
                    result.Add(new Mismatch
                    {
                        PackageId = packageId,
                        DisplayName = display,
                        RequiredVersion = required,
                        InstalledVersion = null,
                        IsMissing = true
                    });
                    continue;
                }

                if (IsVersionLower(installed, required))
                {
                    result.Add(new Mismatch
                    {
                        PackageId = packageId,
                        DisplayName = display,
                        RequiredVersion = required,
                        InstalledVersion = installed,
                        IsMissing = false
                    });
                }
            }

            return result;
        }

        public static void CheckAndWarn(
            PackageVersionChecker checker,
            bool forceDialog = false,
            bool showSatisfiedDialog = false)
        {
            if (checker == null)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // プレハブアセット上の手動チェックも許可。自動チェックのみシーン内を要求。
            if (!forceDialog && !checker.gameObject.scene.IsValid())
                return;

            var mismatches = CollectMismatches(checker);
            if (mismatches.Count == 0)
            {
                if (showSatisfiedDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Package Version Checker",
                        "要求バージョンを満たしています！",
                        "閉じる");
                }

                return;
            }

            var warnKey = SessionWarnedPrefix + BuildMismatchKey(mismatches);
            if (!forceDialog && SessionState.GetBool(warnKey, false))
                return;

            SessionState.SetBool(warnKey, true);

            var message = BuildWarningMessage(mismatches);
            var fix = EditorUtility.DisplayDialog(
                "Package Version Checker",
                message,
                "自動修正（バージョンを上げる）",
                "閉じる");

            if (fix)
                TryAutoFix(mismatches);
        }

        public static string BuildWarningMessage(IReadOnlyList<Mismatch> mismatches)
        {
            var sb = new StringBuilder();
            sb.AppendLine("お使いのプロジェクトのパッケージのバージョンが低いです！");
            sb.AppendLine();
            foreach (var m in mismatches)
            {
                var current = m.IsMissing ? "未インストール" : m.InstalledVersion;
                sb.AppendLine($"・{m.DisplayName}");
                sb.AppendLine($"現在: {current}");
                sb.AppendLine($"要求: {m.RequiredVersion}");
            }

            sb.AppendLine();
            sb.Append("「自動修正」でバージョンを上げられます。");
            return sb.ToString();
        }

        public static bool TryAutoFix(IReadOnlyList<Mismatch> mismatches)
        {
            if (mismatches == null || mismatches.Count == 0)
                return false;

            var manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VpmManifestRelativePath));
            if (!File.Exists(manifestPath))
            {
                EditorUtility.DisplayDialog(
                    "Package Version Checker",
                    $"自動修正に失敗しました。\n{VpmManifestRelativePath} が見つかりません。\nVRChat Creator Companion / VPM でプロジェクトを管理しているか確認してください。",
                    "閉じる");
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(manifestPath, Encoding.UTF8);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    "Package Version Checker",
                    $"vpm-manifest.json の読み込みに失敗しました。\n{e.Message}",
                    "閉じる");
                return false;
            }

            var updated = false;
            var report = new StringBuilder();
            var syncedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in mismatches)
            {
                if (TryUpsertVpmDependencyVersion(ref json, m.PackageId, m.RequiredVersion))
                {
                    updated = true;
                    syncedIds.Add(m.PackageId);
                    report.AppendLine($"・{m.DisplayName} → {m.RequiredVersion}");
                }
                else
                {
                    report.AppendLine($"・{m.DisplayName}: 更新できませんでした");
                }

                // VRChat SDK は avatars / base を同バージョンに揃える
                if (TryGetVrchatSdkPairId(m.PackageId, out var pairId) && !syncedIds.Contains(pairId))
                {
                    if (TryUpsertVpmDependencyVersion(ref json, pairId, m.RequiredVersion))
                    {
                        updated = true;
                        syncedIds.Add(pairId);
                        report.AppendLine($"・{pairId} → {m.RequiredVersion}（SDK 同期）");
                    }
                }
            }

            if (!updated)
            {
                EditorUtility.DisplayDialog(
                    "Package Version Checker",
                    "vpm-manifest.json を更新できませんでした。\n手動で依存バージョンを上げてください。\n\n" + report,
                    "閉じる");
                return false;
            }

            try
            {
                File.WriteAllText(manifestPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    "Package Version Checker",
                    $"vpm-manifest.json の書き込みに失敗しました。\n{e.Message}",
                    "閉じる");
                return false;
            }

            AssetDatabase.Refresh();

            var resolved = TryInvokeVpmResolve();
            // var resolveNote = resolved
            //     ? "VPM Resolve を実行しました。パッケージのダウンロードが完了するまでお待ちください。"
            //     : "vpm-manifest.json は更新しました。VRChat Package Resolver ウィンドウ、または Creator Companion から Resolve を実行してください。";

            // EditorUtility.DisplayDialog(
            //     "Package Version Checker",
            //     "依存バージョンを更新しました。\n\n" + report + "\n" + resolveNote,
            //     "閉じる");

            Debug.Log("依存バージョンを更新しました。\n\n" + report);

            Client.Resolve();
            return true;
        }

        private static bool TryGetVrchatSdkPairId(string packageId, out string pairId)
        {
            if (string.Equals(packageId, "com.vrchat.avatars", StringComparison.OrdinalIgnoreCase))
            {
                pairId = "com.vrchat.base";
                return true;
            }

            if (string.Equals(packageId, "com.vrchat.base", StringComparison.OrdinalIgnoreCase))
            {
                pairId = "com.vrchat.avatars";
                return true;
            }

            pairId = null;
            return false;
        }

        public sealed class PackageChoice
        {
            public string PackageId;
            public string DisplayName;
            public string InstalledVersion;
        }

        /// <summary>よく使うパッケージのプリセット（表示名, packageId）。</summary>
        public static readonly (string DisplayName, string PackageId)[] CommonPackagePresets =
        {
            ("VRChat SDK - Avatars", "com.vrchat.avatars"),
            ("VRChat SDK - Base", "com.vrchat.base"),
            ("Modular Avatar", "nadena.dev.modular-avatar"),
            ("NDMF", "nadena.dev.ndmf"),
            ("Avatar Optimizer", "com.anatawa12.avatar-optimizer"),
            ("lilToon", "jp.lilxyzw.liltoon"),
            ("VRCFury", "com.vrcfury.vrcfury"),
            ("SamiVRCBlocks-Avatar", "com.github.samirin33.samivrcblocks-avatar"),
            ("SamiVRCBlocks-AvatarEditor", "com.github.samirin33.samivrcblocks-avatar-editor"),
        };

        private static List<PackageChoice> _installedPackagesCache;
        private static double _installedPackagesCacheTime;
        private static readonly Dictionary<string, List<string>> VersionChoicesCache =
            new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private static readonly Dictionary<string, double> VersionChoicesCacheTime =
            new Dictionary<string, double>(StringComparer.Ordinal);

        public static IReadOnlyList<PackageChoice> GetInstalledPackageChoices(bool forceRefresh = false)
        {
            const double cacheSeconds = 5.0;
            if (!forceRefresh
                && _installedPackagesCache != null
                && EditorApplication.timeSinceStartup - _installedPackagesCacheTime < cacheSeconds)
            {
                return _installedPackagesCache;
            }

            var map = new Dictionary<string, PackageChoice>(StringComparer.Ordinal);

            foreach (var preset in CommonPackagePresets)
            {
                map[preset.PackageId] = new PackageChoice
                {
                    PackageId = preset.PackageId,
                    DisplayName = preset.DisplayName,
                    InstalledVersion = GetInstalledVersion(preset.PackageId)
                };
            }

            try
            {
                var registered = PackageInfo.GetAllRegisteredPackages();
                if (registered != null)
                {
                    foreach (var p in registered)
                    {
                        if (p == null || string.IsNullOrEmpty(p.name))
                            continue;
                        // 組み込み Unity モジュールは候補から除外
                        if (p.name.StartsWith("com.unity.modules.", StringComparison.Ordinal))
                            continue;

                        var display = string.IsNullOrEmpty(p.displayName) ? p.name : p.displayName;
                        if (map.TryGetValue(p.name, out var existing))
                        {
                            existing.InstalledVersion = p.version;
                            if (string.IsNullOrEmpty(existing.DisplayName))
                                existing.DisplayName = display;
                        }
                        else
                        {
                            map[p.name] = new PackageChoice
                            {
                                PackageId = p.name,
                                DisplayName = display,
                                InstalledVersion = p.version
                            };
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            // Packages フォルダ直下も補完
            var packagesRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages"));
            if (Directory.Exists(packagesRoot))
            {
                foreach (var dir in Directory.GetDirectories(packagesRoot))
                {
                    var packageJson = Path.Combine(dir, "package.json");
                    if (!File.Exists(packageJson))
                        continue;

                    var json = File.ReadAllText(packageJson);
                    var name = ReadJsonStringField(json, "name");
                    if (string.IsNullOrEmpty(name))
                        continue;

                    var display = ReadJsonStringField(json, "displayName") ?? name;
                    var version = ReadJsonStringField(json, "version");
                    if (map.TryGetValue(name, out var existing))
                    {
                        if (string.IsNullOrEmpty(existing.InstalledVersion))
                            existing.InstalledVersion = version;
                    }
                    else
                    {
                        map[name] = new PackageChoice
                        {
                            PackageId = name,
                            DisplayName = display,
                            InstalledVersion = version
                        };
                    }
                }
            }

            _installedPackagesCache = map.Values
                .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _installedPackagesCacheTime = EditorApplication.timeSinceStartup;
            return _installedPackagesCache;
        }

        /// <summary>
        /// バージョン候補（インストール済み・VPM 公開版・よく使う近傍）。
        /// </summary>
        public static IReadOnlyList<string> GetVersionChoices(string packageId, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return Array.Empty<string>();

            packageId = packageId.Trim();
            const double cacheSeconds = 30.0;
            if (!forceRefresh
                && VersionChoicesCache.TryGetValue(packageId, out var cached)
                && VersionChoicesCacheTime.TryGetValue(packageId, out var cachedAt)
                && EditorApplication.timeSinceStartup - cachedAt < cacheSeconds)
            {
                return cached;
            }

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var installed = GetInstalledVersion(packageId);
            if (!string.IsNullOrEmpty(installed))
                set.Add(installed);

            foreach (var v in TryGetVpmVersions(packageId))
                set.Add(v);

            // インストール済みからメジャー近傍の簡易候補も足す
            if (!string.IsNullOrEmpty(installed) && TryParseVersion(installed, out var ver))
            {
                var build = ver.Build < 0 ? 0 : ver.Build;
                set.Add($"{ver.Major}.{ver.Minor}.{build}");
                if (ver.Minor > 0)
                    set.Add($"{ver.Major}.{ver.Minor - 1}.0");
                set.Add($"{ver.Major}.{ver.Minor + 1}.0");
                set.Add($"{ver.Major + 1}.0.0");
            }

            var list = set
                .OrderByDescending(v => TryParseVersion(v, out var parsed) ? parsed : new Version(0, 0))
                .ThenByDescending(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();

            VersionChoicesCache[packageId] = list;
            VersionChoicesCacheTime[packageId] = EditorApplication.timeSinceStartup;
            return list;
        }

        public static string GetPresetDisplayName(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return null;

            packageId = packageId.Trim();
            foreach (var preset in CommonPackagePresets)
            {
                if (string.Equals(preset.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                    return preset.DisplayName;
            }

            var installed = GetInstalledPackageChoices();
            var match = installed.FirstOrDefault(p =>
                string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            return match?.DisplayName;
        }

        private static IReadOnlyList<string> TryGetVpmVersions(string packageId)
        {
            try
            {
                var resolverType = Type.GetType("VRC.PackageManagement.Resolver.Resolver, com.vrchat.core.vpm-resolver.Editor");
                if (resolverType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name != "com.vrchat.core.vpm-resolver.Editor")
                            continue;
                        resolverType = assembly.GetType("VRC.PackageManagement.Resolver.Resolver");
                        if (resolverType != null)
                            break;
                    }
                }

                if (resolverType == null)
                    return Array.Empty<string>();

                var method = resolverType.GetMethod("GetAllVersionsOf", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    return Array.Empty<string>();

                if (!(method.Invoke(null, new object[] { packageId }) is System.Collections.IEnumerable versions))
                    return Array.Empty<string>();

                var result = new List<string>();
                foreach (var item in versions)
                {
                    if (item is string s && !string.IsNullOrEmpty(s))
                        result.Add(s);
                }

                return result;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static string GetInstalledVersion(string packageId)
        {
            if (string.IsNullOrWhiteSpace(packageId))
                return null;

            packageId = packageId.Trim();

            try
            {
                var registered = PackageInfo.GetAllRegisteredPackages();
                var match = registered?.FirstOrDefault(p => p != null && p.name == packageId);
                if (match != null && !string.IsNullOrEmpty(match.version))
                    return match.version;
            }
            catch
            {
                // Package Manager 未準備時はファイルから読む
            }

            var packageJsonPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", packageId, "package.json"));
            if (File.Exists(packageJsonPath))
            {
                var version = ReadJsonStringField(File.ReadAllText(packageJsonPath), "version");
                if (!string.IsNullOrEmpty(version))
                    return version;
            }

            var locked = TryReadVpmLockedVersion(packageId);
            return string.IsNullOrEmpty(locked) ? null : locked;
        }

        public static bool IsVersionLower(string installed, string required)
        {
            if (!TryParseVersion(installed, out var a) || !TryParseVersion(required, out var b))
            {
                // パースできない場合は文字列比較にフォールバック（一致以外を要更新扱い）
                return !string.Equals(installed?.Trim(), required?.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            if (a != b)
                return a < b;

            // コアが同じ場合、installed のみプレリリースなら要求未満とみなす
            var installedPre = HasPrereleaseLabel(installed);
            var requiredPre = HasPrereleaseLabel(required);
            return installedPre && !requiredPre;
        }

        public static bool TryParseVersion(string input, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            var core = GetVersionCore(input);
            var parts = core.Split('.');
            if (parts.Length < 2)
                return false;

            // System.Version は最大4要素
            var normalized = string.Join(".", parts.Take(Math.Min(4, parts.Length)));
            if (parts.Length == 2)
                normalized += ".0";

            return Version.TryParse(normalized, out version);
        }

        private static string GetVersionCore(string input)
        {
            var core = input.Trim();
            var cut = core.IndexOfAny(new[] { '-', '+' });
            return cut >= 0 ? core.Substring(0, cut) : core;
        }

        private static bool HasPrereleaseLabel(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;
            var cut = input.Trim().IndexOf('-');
            return cut >= 0;
        }

        private static string BuildMismatchKey(IReadOnlyList<Mismatch> mismatches)
        {
            return string.Join("|", mismatches
                .OrderBy(m => m.PackageId, StringComparer.Ordinal)
                .Select(m => $"{m.PackageId}@{m.RequiredVersion}<{(m.InstalledVersion ?? "missing")}"));
        }

        private static string TryReadVpmLockedVersion(string packageId)
        {
            var manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VpmManifestRelativePath));
            if (!File.Exists(manifestPath))
                return null;

            var json = File.ReadAllText(manifestPath);
            var lockedMatch = Regex.Match(
                json,
                "\"locked\"\\s*:\\s*\\{(?<body>[\\s\\S]*?)\\n\\s*\\}",
                RegexOptions.CultureInvariant);

            var searchIn = lockedMatch.Success ? lockedMatch.Groups["body"].Value : json;
            var packageMatch = Regex.Match(
                searchIn,
                $"\"{Regex.Escape(packageId)}\"\\s*:\\s*\\{{[^}}]*?\"version\"\\s*:\\s*\"(?<ver>[^\"]+)\"",
                RegexOptions.CultureInvariant);

            return packageMatch.Success ? packageMatch.Groups["ver"].Value : null;
        }

        /// <summary>
        /// vpm-manifest.json の dependencies / locked 双方にある package の version を upsert する。
        /// VPM Resolve は locked を優先するため、dependencies だけの更新ではインストールが変わらない。
        /// </summary>
        internal static bool TryUpsertVpmDependencyVersion(ref string json, string packageId, string version)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(version))
                return false;

            var changed = false;

            // "package.id": { "version": "..." } 形式（dependencies / locked の両方）を更新。
            // nested dependencies の "id": ">=x" 文字列制約にはマッチしない。
            // MatchEvaluator 必須（"$1" + "3.x" だと $13 グループ参照になる）
            var entryRegex = new Regex(
                $"(\"{Regex.Escape(packageId)}\"\\s*:\\s*\\{{\\s*\"version\"\\s*:\\s*\")([^\"]+)(\")",
                RegexOptions.CultureInvariant);

            if (entryRegex.IsMatch(json))
            {
                json = entryRegex.Replace(json, m =>
                {
                    if (m.Groups[2].Value == version)
                        return m.Value;
                    changed = true;
                    return m.Groups[1].Value + version + m.Groups[3].Value;
                });
            }

            // dependencies に無ければ追加（Resolve の要求元）
            if (!IsPackageInDependenciesSection(json, packageId))
            {
                if (TryAddPackageToDependencies(ref json, packageId, version))
                    changed = true;
            }
            else if (!changed)
            {
                // dependencies は既に要求値だが、locked / 実体が古い場合でも Resolve を走らせる
                changed = true;
            }

            return changed;
        }

        private static bool IsPackageInDependenciesSection(string json, string packageId)
        {
            if (!TryGetDependenciesBody(json, out var body, out _, out _))
                return false;

            return Regex.IsMatch(
                body,
                $"\"{Regex.Escape(packageId)}\"\\s*:\\s*\\{{",
                RegexOptions.CultureInvariant);
        }

        private static bool TryAddPackageToDependencies(ref string json, string packageId, string version)
        {
            if (!TryGetDependenciesBody(json, out var body, out var bodyIndex, out var bodyLength))
                return false;

            var hasEntries = body.IndexOf('"') >= 0;
            var comma = hasEntries ? "," : "";
            var newBody = $"\n    \"{packageId}\": {{\n      \"version\": \"{version}\"\n    }}{comma}{body}";
            json = json.Substring(0, bodyIndex) + newBody + json.Substring(bodyIndex + bodyLength);
            return true;
        }

        private static bool TryGetDependenciesBody(string json, out string body, out int bodyIndex, out int bodyLength)
        {
            body = null;
            bodyIndex = 0;
            bodyLength = 0;

            // dependencies 〜 locked 手前のブロックだけを対象にする
            var depsSection = Regex.Match(
                json,
                "\"dependencies\"\\s*:\\s*\\{(?<body>[\\s\\S]*?)\\n(?<indent>\\s*)\\}\\s*,\\s*\\n\\s*\"locked\"",
                RegexOptions.CultureInvariant);

            if (!depsSection.Success)
            {
                depsSection = Regex.Match(
                    json,
                    "\"dependencies\"\\s*:\\s*\\{(?<body>[\\s\\S]*?)\\n(?<indent>\\s*)\\}",
                    RegexOptions.CultureInvariant);
            }

            if (!depsSection.Success)
                return false;

            body = depsSection.Groups["body"].Value;
            bodyIndex = depsSection.Groups["body"].Index;
            bodyLength = depsSection.Groups["body"].Length;
            return true;
        }

        private static string ReadJsonStringField(string json, string fieldName)
        {
            var match = Regex.Match(
                json,
                $"\"{Regex.Escape(fieldName)}\"\\s*:\\s*\"(?<v>[^\"]+)\"",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["v"].Value : null;
        }

        private static bool TryInvokeVpmResolve()
        {
            try
            {
                var resolverType = Type.GetType("VRC.PackageManagement.Resolver.Resolver, com.vrchat.core.vpm-resolver.Editor");
                if (resolverType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name != "com.vrchat.core.vpm-resolver.Editor")
                            continue;
                        resolverType = assembly.GetType("VRC.PackageManagement.Resolver.Resolver");
                        if (resolverType != null)
                            break;
                    }
                }

                if (resolverType == null)
                    return false;

                var method = resolverType.GetMethod("ResolveManifest", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    return false;

                method.Invoke(null, null);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PackageVersionChecker] VPM Resolve の呼び出しに失敗しました: {e.Message}");
                return false;
            }
        }
    }
}
