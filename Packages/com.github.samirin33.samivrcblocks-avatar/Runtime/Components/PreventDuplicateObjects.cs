using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Components
{
    /// <summary>
    /// 同じ id を持つオブジェクトがアバター内に複数あるとき、Transforming（MA 前）で 2 つ目以降を削除し、警告を出す。
    /// </summary>
    [AddComponentMenu("SamiVRCBlocks-Avatar/SB PreventDuplicateObjects")]
    public class PreventDuplicateObjects : SamirinMABaseSingle
    {
        [Tooltip("重複判定に使う ID。同じ ID のオブジェクトはアバター内に 1 つだけ残す")]
        public string id;

        public override void OnBuildSingle(
            SamirinBuildPhase buildPhase,
            bool beforeModularAvatar,
            SamirinMABaseSingle[] maScripts,
            GameObject avatarRootObject,
            System.Action<GameObject, SamirinMABaseSingle[]> invokeBuilder,
            System.Action<GameObject, SamirinMABaseSingle[]> invokeReplaceBuilder)
        {
            // 元実装と同様 Transforming。MA 処理前に重複を除去する。
            if (buildPhase != SamirinBuildPhase.Transforming || !beforeModularAvatar)
                return;

            RemoveDuplicates(avatarRootObject, maScripts);
        }

        private static void RemoveDuplicates(GameObject avatarRoot, SamirinMABaseSingle[] maScripts)
        {
            if (avatarRoot == null || maScripts == null)
                return;

            var avatarRootTransform = avatarRoot.transform;
            var components = maScripts.OfType<PreventDuplicateObjects>().ToArray();
            var componentsById = new Dictionary<string, List<PreventDuplicateObjects>>();

            foreach (var component in components)
            {
                if (component == null || string.IsNullOrEmpty(component.id))
                    continue;

                if (!componentsById.TryGetValue(component.id, out var group))
                {
                    group = new List<PreventDuplicateObjects>();
                    componentsById[component.id] = group;
                }

                group.Add(component);
            }

            foreach (var group in componentsById.Values.Where(g => g.Count > 1))
            {
                var objectName = group[0].gameObject.name;
                var hierarchyList = BuildHierarchyList(group, avatarRootTransform);

                // ビルド中（アップロード・Play 時・CI）にモーダルダイアログでブロックしないよう、
                // NDMF のエラーレポートへ警告として出し、ビルドは続行する。
                SamirinRuntimeReport.Warning(
                    $"[PreventDuplicateObjects] {objectName} がアバター内で重複しています。ビルド結果が意図しないものになる可能性があるので、{objectName} をアバター内で1つだけ存在するようにしてください。2つ目以降はビルド時に削除しました。\n\n重複している階層:\n{hierarchyList}",
                    group[0]);

                foreach (var duplicate in group.Skip(1))
                    Object.DestroyImmediate(duplicate.gameObject);
            }

            foreach (var component in components)
            {
                if (component != null)
                    Object.DestroyImmediate(component);
            }
        }

        private static string BuildHierarchyList(
            IReadOnlyList<PreventDuplicateObjects> group,
            Transform avatarRoot)
        {
            var builder = new StringBuilder();

            for (var i = 0; i < group.Count; i++)
            {
                var path = GetRelativePath(group[i].transform, avatarRoot);
                builder.AppendLine($"・{path} （{group[i].id}）");
            }

            return builder.ToString().TrimEnd();
        }

        private static string GetRelativePath(Transform target, Transform avatarRoot)
        {
            if (target == avatarRoot)
                return target.name;

            var parts = new List<string>();
            var current = target;

            while (current != null && current != avatarRoot)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
