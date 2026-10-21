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
        // 入力途中の値（例: "3" → "3.10.0"）で警告しないよう、最後の変更から一定時間後にまとめて照合する。
        private const double CheckDebounceSeconds = 1.5;
        private static double _flushDueTime;
        private static bool _scheduled;
        private static bool _pendingForceDialog;

        [InitializeOnLoadMethod]
        private static void RegisterValidateHook()
        {
            PackageVersionChecker.EditorValidateHook = OnCheckerValidated;
        }

        private static void OnCheckerValidated(PackageVersionChecker checker)
        {
            if (checker == null)
                return;

            // インスペクタで要件を編集中の作者には、インスペクタ上の状態表示で十分なので自動ダイアログを出さない。
            if (Selection.Contains(checker.gameObject))
                return;

            ScheduleCheck(checker, forceDialog: false);
        }

        public sealed class Mismatch
        {
            public string PackageId;
            public string DisplayName;
            public string RequiredVersion;
            public string InstalledVersion;
            public bool IsMissing;
        }

        /// <summary>
        /// 照合を予約する。ダイアログは OnValidate / ObjectChangeEvents の外（EditorApplication.update）で、
        /// 予約されたすべての Checker の不足分をまとめて 1 回だけ出す。
        /// forceDialog=true はセッション内の抑制を無視する（手動操作向け）。
        /// </summary>
        public static void ScheduleCheck(PackageVersionChecker checker, bool forceDialog = false)
        {
            if (checker == null)
                return;

            if (forceDialog)
                _pendingForceDialog = true;

            PendingInstanceIds.Add(checker.GetInstanceID());
            _flushDueTime = EditorApplication.timeSinceStartup + CheckDebounceSeconds;
            if (_scheduled)
                return;

            _scheduled = true;
            EditorApplication.update += FlushWhenReady;
        }

        /// <summary>
        /// 配置された GameObject 配下の PackageVersionChecker を照合する。
        /// </summary>
        public static void CheckPlacedHierarchy(GameObject root, bool forceDialog = false)
        {
            if (root == null)
                return;

            var checkers = root.GetComponentsInChildren<PackageVersionChecker>(true);
            if (checkers == null || checkers.Length == 0)
                return;

            foreach (var checker in checkers)
                ScheduleCheck(checker, forceDialog);
        }

        private static void FlushWhenReady()
        {
            if (EditorApplication.timeSinceStartup < _flushDueTime
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= FlushWhenReady;
            _scheduled = false;

            var forceDialog = _pendingForceDialog;
            _pendingForceDialog = false;

            var ids = PendingInstanceIds.ToArray();
            PendingInstanceIds.Clear();

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var merged = new Dictionary<string, Mismatch>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                var checker = EditorUtility.InstanceIDToObject(id) as PackageVersionChecker;
                if (checker == null || !checker.gameObject.scene.IsValid())
                    continue;

                foreach (var m in CollectMismatches(checker))
                {
                    // 複数の Checker が同じパッケージを要求する場合は、最も高い要求バージョンに揃える。
                    if (!merged.TryGetValue(m.PackageId, out var existing)
                        || IsVersionLower(existing.RequiredVersion, m.RequiredVersion))
                        merged[m.PackageId] = m;
                }
            }

            if (merged.Count == 0)
                return;

            WarnMismatches(merged.Values.ToList(), forceDialog);
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

            WarnMismatches(mismatches, forceDialog);
        }

        private static void WarnMismatches(IReadOnlyList<Mismatch> mismatches, bool forceDialog)
        {
            // 同じ不足内容はセッション（ドメインリロードをまたいでエディタ終了まで）に 1 回だけ自動表示する。
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

        /// <summary>
        /// VRChat Package Resolver（vpm-core-lib）の UpdateVPMPackage / AddVPMPackage で要求バージョンを入れる。
        /// vpm-manifest.json の locked を直接書き換えると、推移的な依存（例: MA が要求する NDMF の版、
        /// SDK Avatars が要求する SDK Base の版）が古いまま残り壊れた構成になり得るため、
        /// 依存解決できる VPM API が使えない・失敗した場合は VCC / ALCOM / vrc-get での手動更新を案内する。
        /// </summary>
        public static bool TryAutoFix(IReadOnlyList<Mismatch> mismatches)
        {
            if (mismatches == null || mismatches.Count == 0)
                return false;

            if (!VpmApi.TryCreate(out var api, out var apiError))
            {
                ShowManualUpdateDialog(mismatches, apiError);
                return false;
            }

            var report = new StringBuilder();
            var failed = new List<Mismatch>();
            var anyInstalled = false;
            try
            {
                for (var i = 0; i < mismatches.Count; i++)
                {
                    var m = mismatches[i];
                    EditorUtility.DisplayProgressBar(
                        "Package Version Checker",
                        $"{m.DisplayName} {m.RequiredVersion} を取得中...",
                        (float)i / mismatches.Count);

                    if (api.TryInstall(m.PackageId, m.RequiredVersion, m.IsMissing, out var error))
                    {
                        anyInstalled = true;
                        report.AppendLine($"・{m.DisplayName} → {m.RequiredVersion}");
                    }
                    else
                    {
                        failed.Add(m);
                        report.AppendLine($"・{m.DisplayName}: {error}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Unity Package Manager への反映（Client.Resolve + AssetDatabase.Refresh）はここで 1 回だけ行う。
            if (anyInstalled)
                api.ForceRefresh();

            if (failed.Count > 0)
            {
                ShowManualUpdateDialog(failed, report.ToString());
                return false;
            }

            Debug.Log("[PackageVersionChecker] パッケージを更新しました。\n" + report);
            return true;
        }

        private static void ShowManualUpdateDialog(IReadOnlyList<Mismatch> mismatches, string detail)
        {
            var sb = new StringBuilder();
            sb.AppendLine("自動修正できませんでした。");
            sb.AppendLine("VRChat Creator Companion / ALCOM の「プロジェクトの管理」から、次のパッケージを更新してください。");
            sb.AppendLine();
            foreach (var m in mismatches)
                sb.AppendLine($"・{m.DisplayName} ({m.PackageId}) {m.RequiredVersion} 以上");
            sb.AppendLine();
            sb.AppendLine("vrc-get を使う場合:");
            foreach (var m in mismatches)
                sb.AppendLine($"  vrc-get install {m.PackageId} {m.RequiredVersion}");
            if (!string.IsNullOrEmpty(detail))
            {
                sb.AppendLine();
                sb.AppendLine("詳細:");
                sb.Append(detail);
            }

            EditorUtility.DisplayDialog("Package Version Checker", sb.ToString(), "閉じる");
        }

        /// <summary>
        /// com.vrchat.core.vpm-resolver / vpm-core-lib への反射アクセス。
        /// asmdef 参照を足すと VPM 非管理プロジェクトでコンパイルできなくなるため反射で呼ぶ。
        /// </summary>
        private sealed class VpmApi
        {
            private object _project;
            private MethodInfo _getPackage;
            private MethodInfo _update;
            private MethodInfo _add;
            private MethodInfo _forceRefresh;

            public static bool TryCreate(out VpmApi api, out string error)
            {
                api = null;
                error = null;

                var reposType = FindType("VRC.PackageManagement.Core.Repos");
                var projectType = FindType("VRC.PackageManagement.Core.Types.UnityProject");
                var resolverType = FindType("VRC.PackageManagement.Resolver.Resolver");
                if (reposType == null || projectType == null || resolverType == null)
                {
                    error = "VRChat Package Resolver（com.vrchat.core.vpm-resolver）が見つかりません。";
                    return false;
                }

                var getPackage = reposType.GetMethod(
                    "GetPackageWithVersionMatch",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null);
                var instanceMethods = projectType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var update = instanceMethods.FirstOrDefault(m =>
                    m.Name == "UpdateVPMPackage" && m.GetParameters().Length == 1);
                var add = instanceMethods.FirstOrDefault(m =>
                {
                    if (m.Name != "AddVPMPackage")
                        return false;
                    var ps = m.GetParameters();
                    return ps.Length >= 1
                           && ps[0].ParameterType != typeof(string)
                           && ps.Skip(1).All(p => p.IsOptional);
                });
                var projectDirProperty = resolverType.GetProperty("ProjectDir", BindingFlags.Public | BindingFlags.Static);
                var forceRefresh = resolverType.GetMethod("ForceRefresh", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

                if (getPackage == null || update == null || add == null || projectDirProperty == null || forceRefresh == null)
                {
                    error = "VRChat Package Resolver の API が想定と異なります（VPM の更新で変わった可能性があります）。";
                    return false;
                }

                object project;
                try
                {
                    var projectDir = projectDirProperty.GetValue(null) as string;
                    if (string.IsNullOrEmpty(projectDir))
                    {
                        error = "VPM プロジェクトのフォルダを取得できませんでした。";
                        return false;
                    }

                    project = Activator.CreateInstance(projectType, projectDir);
                }
                catch (Exception e)
                {
                    error = (e.InnerException ?? e).Message;
                    return false;
                }

                api = new VpmApi
                {
                    _project = project,
                    _getPackage = getPackage,
                    _update = update,
                    _add = add,
                    _forceRefresh = forceRefresh
                };
                return true;
            }

            public bool TryInstall(string packageId, string version, bool isMissing, out string error)
            {
                error = null;
                try
                {
                    var package = _getPackage.Invoke(null, new object[] { packageId, version });
                    if (package == null)
                    {
                        error = $"VPM リポジトリに {version} が見つかりません（リポジトリ未登録の可能性があります）";
                        return false;
                    }

                    if (!isMissing)
                    {
                        _update.Invoke(_project, new[] { package });
                        return true;
                    }

                    var ps = _add.GetParameters();
                    var args = new object[ps.Length];
                    args[0] = package;
                    for (var i = 1; i < ps.Length; i++)
                    {
                        args[i] = ps[i].HasDefaultValue
                            ? ps[i].DefaultValue
                            : ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
                    }

                    if (_add.Invoke(_project, args) is bool added && !added)
                    {
                        error = "追加に失敗しました";
                        return false;
                    }

                    return true;
                }
                catch (Exception e)
                {
                    error = (e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e).Message;
                    return false;
                }
            }

            public void ForceRefresh()
            {
                try
                {
                    _forceRefresh.Invoke(null, null);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PackageVersionChecker] パッケージの再読み込みに失敗しました: {(e.InnerException ?? e).Message}");
                }
            }

            private static Type FindType(string fullName)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                        return type;
                }

                return null;
            }
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

        private static string ReadJsonStringField(string json, string fieldName)
        {
            var match = Regex.Match(
                json,
                $"\"{Regex.Escape(fieldName)}\"\\s*:\\s*\"(?<v>[^\"]+)\"",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["v"].Value : null;
        }
    }
}
