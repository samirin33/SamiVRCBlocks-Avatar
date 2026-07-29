#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Samirin.VRCUtility.Avatars.UI
{
    /// <summary>
    /// スタイル付きパネル。SamirinPanelBase.uxml をテンプレートとして使用するカスタムコントロール。
    /// 背景画像・ボーダー・パディングが適用されたコンテナとして利用できます。
    /// </summary>
    public class SamirinPanelBase : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<SamirinPanelBase, UxmlTraits> { }

        public new class UxmlTraits : VisualElement.UxmlTraits { }

        private const string UxmlPath = "Packages/com.github.samirin33.samirin-vrc-utility.avatars/Editor/UI/SPanel.uxml";

        /// <summary>背景付きのコンテナ（name="Background"）。子要素はこの中に追加してください。</summary>
        public VisualElement Background => this.Q("Background");

        public SamirinPanelBase()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (tree != null)
                tree.CloneTree(this);

            pickingMode = PickingMode.Position;
            focusable = false;
        }
    }
}
#endif
