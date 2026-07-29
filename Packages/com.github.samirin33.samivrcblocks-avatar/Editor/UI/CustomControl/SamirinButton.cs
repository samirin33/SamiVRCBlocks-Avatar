#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Samirin.VRCUtility.Avatars
{
    /// <summary>ボタン内の表示内容: テキストのみ / アイコンのみ / 両方</summary>
    public enum SamirinButtonDisplayMode
    {
        TextOnly,
        IconOnly,
        Both
    }

    /// <summary>ButtonBG フォルダ内の背景画像をプルダウンで選択します。</summary>
    public enum SamirinButtonBackgroundStyle
    {
        Default,
        Black,
        Blue,
        Green,
        Orange,
        Red
    }

    /// <summary>
    /// スタイル付きボタン。SamirinButton.uxml をテンプレートとして使用するカスタムコントロール。
    /// ホバー時は .SamirinButtonBody.hover、クリック時イベント・ツールチップ対応。
    /// </summary>
    public class SamirinButton : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<SamirinButton, UxmlTraits> { }
        private const string ButtonBgFolder = "Packages/com.github.samirin33.samirin-vrc-utility.avatars/Editor/Image/ButtonBG";

        public new class UxmlTraits : VisualElement.UxmlTraits
        {
            readonly UxmlStringAttributeDescription m_Text = new UxmlStringAttributeDescription { name = "text", defaultValue = "Button" };
            readonly UxmlEnumAttributeDescription<SamirinButtonDisplayMode> m_DisplayMode = new UxmlEnumAttributeDescription<SamirinButtonDisplayMode> { name = "display-mode", defaultValue = SamirinButtonDisplayMode.TextOnly };
            readonly UxmlStringAttributeDescription m_Tooltip = new UxmlStringAttributeDescription { name = "tooltip", defaultValue = "" };
            readonly UxmlEnumAttributeDescription<SamirinButtonBackgroundStyle> m_BackgroundStyle = new UxmlEnumAttributeDescription<SamirinButtonBackgroundStyle> { name = "background-style", defaultValue = SamirinButtonBackgroundStyle.Default };
            readonly UxmlStringAttributeDescription m_IconImage = new UxmlStringAttributeDescription { name = "icon-image", defaultValue = "" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                var button = (SamirinButton)ve;
                button.text = m_Text.GetValueFromBag(bag, cc);
                button.displayMode = m_DisplayMode.GetValueFromBag(bag, cc);
                var tip = m_Tooltip.GetValueFromBag(bag, cc);
                if (!string.IsNullOrEmpty(tip))
                    button.tooltip = tip;
                var bgStyle = m_BackgroundStyle.GetValueFromBag(bag, cc);
                if (bgStyle != SamirinButtonBackgroundStyle.Default)
                    button.SetBackgroundStyle(bgStyle);
                var iconRef = m_IconImage.GetValueFromBag(bag, cc);
                if (!string.IsNullOrEmpty(iconRef))
                    button.SetIconImageFromReference(iconRef);
            }
        }

        private const string UxmlPath = "Packages/com.github.samirin33.samirin-vrc-utility.avatars/Editor/UI/SamirinButton.uxml";

        /// <summary>ボタン文言を表示する Label（name="ButtonText"）。親の最小サイズに合わせてフォントサイズが自動調整されます。</summary>
        public Label ButtonText => this.Q<Label>("ButtonText");
        /// <summary>アイコン用 VisualElement（name="ButtonIcon"）。親の最小サイズに合わせてサイズが自動調整されます。</summary>
        public VisualElement ButtonIcon => this.Q("ButtonIcon");
        /// <summary>背景画像を差し替え可能な VisualElement（name="ButtonBackground"）</summary>
        public VisualElement ButtonBackground => this.Q("ButtonBackground");
        /// <summary>ホバー/アクティブ用のスタイルを適用する Body（name="ButtonBody"）</summary>
        public VisualElement ButtonBody => this.Q("SamirinButton");

        private SamirinButtonDisplayMode _displayMode = SamirinButtonDisplayMode.Both;

        /// <summary>表示モード: テキストのみ / アイコンのみ / 両方</summary>
        public SamirinButtonDisplayMode displayMode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                ApplyDisplayMode();
            }
        }

        /// <summary>ボタンに表示するテキスト</summary>
        public string text
        {
            get => ButtonText?.text ?? "";
            set { if (ButtonText != null) ButtonText.text = value ?? ""; }
        }

        /// <summary>背景スタイル（ButtonBG フォルダからプルダウン選択）。Default のときはテンプレートのまま。</summary>
        public SamirinButtonBackgroundStyle backgroundStyle
        {
            get => _backgroundStyle;
            set { _backgroundStyle = value; SetBackgroundStyle(value); }
        }
        private SamirinButtonBackgroundStyle _backgroundStyle = SamirinButtonBackgroundStyle.Default;

        /// <summary>アイコン画像をテクスチャまたは VectorImage でアタッチします。C# から object で代入するときに使用。</summary>
        public Texture2D iconImageAsTexture
        {
            set => SetIconImage(value);
        }

        /// <summary>アイコン画像を VectorImage でアタッチします。</summary>
        public VectorImage iconImageAsVector
        {
            set => SetIconImage(value);
        }

        public SamirinButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (tree != null)
                tree.CloneTree(this);

            pickingMode = PickingMode.Position;
            // focusable = false: UI Builder でフォーカス時に Inspector が空シーケンスで .First() して落ちる不具合を避ける
            focusable = false;

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            // ホバー判定は大元の親（this）で行う。TrickleDown で子の上でもルートが先に受け取り、Leave はルート外に出たときだけ解除する
            // RegisterCallback<MouseEnterEvent>(OnMouseEnter, TrickleDown.TrickleDown);
            // RegisterCallback<MouseLeaveEvent>(OnMouseLeave, TrickleDown.TrickleDown);
            // RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            // RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

            ApplyDisplayMode();
        }

        private void ApplyDisplayMode()
        {
            var showText = _displayMode == SamirinButtonDisplayMode.Both || _displayMode == SamirinButtonDisplayMode.TextOnly;
            var showIcon = _displayMode == SamirinButtonDisplayMode.Both || _displayMode == SamirinButtonDisplayMode.IconOnly;
            if (ButtonText != null)
                ButtonText.style.display = showText ? DisplayStyle.Flex : DisplayStyle.None;
            if (ButtonIcon != null)
                ButtonIcon.style.display = showIcon ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>ButtonBackground の背景画像を指定したテクスチャに差し替えます。</summary>
        public void SetBackgroundImage(Texture2D texture)
        {
            var bg = ButtonBackground;
            if (bg == null) return;
            bg.style.backgroundImage = texture != null ? new StyleBackground(texture) : StyleKeyword.Null;
        }

        /// <summary>ButtonBackground の背景画像を指定した VectorImage に差し替えます。</summary>
        public void SetBackgroundImage(VectorImage vectorImage)
        {
            var bg = ButtonBackground;
            if (bg == null) return;
            bg.style.backgroundImage = vectorImage != null ? new StyleBackground(vectorImage) : StyleKeyword.Null;
        }

        /// <summary>ButtonBG フォルダ内の背景スタイルをプルダウンで選択した値に差し替えます。</summary>
        public void SetBackgroundStyle(SamirinButtonBackgroundStyle style)
        {
            if (style == SamirinButtonBackgroundStyle.Default) return;
            var fileName = style.ToString() + ".png";
            var path = ButtonBgFolder + "/" + fileName;
            SetBackgroundImageFromPath(path);
        }

        /// <summary>プロジェクト内のアセットパス（Assets/... または Packages/...）で背景画像を差し替えます。テクスチャまたは VectorImage に対応。</summary>
        public void SetBackgroundImageFromPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var path = NormalizeAssetPath(assetPath);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) { SetBackgroundImage(tex); return; }
            var vec = AssetDatabase.LoadAssetAtPath<VectorImage>(path);
            if (vec != null) SetBackgroundImage(vec);
        }

        /// <summary>アイコン画像を指定したテクスチャに差し替えます。</summary>
        public void SetIconImage(Texture2D texture)
        {
            var icon = ButtonIcon;
            if (icon == null) return;
            icon.style.backgroundImage = texture != null ? new StyleBackground(texture) : StyleKeyword.Null;
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }

        /// <summary>アイコン画像を指定した VectorImage に差し替えます。</summary>
        public void SetIconImage(VectorImage vectorImage)
        {
            var icon = ButtonIcon;
            if (icon == null) return;
            icon.style.backgroundImage = vectorImage != null ? new StyleBackground(vectorImage) : StyleKeyword.Null;
            icon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }

        /// <summary>プロジェクト内のアセットパスでアイコン画像を差し替えます。テクスチャまたは VectorImage に対応。</summary>
        public void SetIconImageFromPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var path = NormalizeAssetPath(assetPath);
            LoadAndSetIcon(path);
        }

        /// <summary>アイコンをアセット参照で設定します。パス・project://database/...?guid=... 形式・GUID 文字列のいずれかに対応。UI Builder でアセットをドラッグしてアタッチしたときに使われます。</summary>
        public void SetIconImageFromReference(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return;
            var guid = TryParseGuidFromReference(reference);
            if (guid != null)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    LoadAndSetIcon(path);
                return;
            }
            var pathOnly = NormalizeAssetPath(reference);
            LoadAndSetIcon(pathOnly);
        }

        private void LoadAndSetIcon(string assetPath)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null) { SetIconImage(tex); return; }
            var vec = AssetDatabase.LoadAssetAtPath<VectorImage>(assetPath);
            if (vec != null) SetIconImage(vec);
        }

        private static string TryParseGuidFromReference(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return null;
            var guidStart = reference.IndexOf("guid=", StringComparison.OrdinalIgnoreCase);
            if (guidStart >= 0)
            {
                guidStart += 5;
                var guidEnd = reference.IndexOf('&', guidStart);
                var len = guidEnd >= 0 ? guidEnd - guidStart : reference.Length - guidStart;
                if (len == 32)
                {
                    var guid = reference.Substring(guidStart, 32);
                    foreach (var c in guid)
                        if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                            return null;
                    return guid;
                }
            }
            if (reference.Length == 32)
            {
                foreach (var c in reference)
                    if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                        return null;
                return reference;
            }
            return null;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var p = path;
            if (p.StartsWith("project://database/", StringComparison.OrdinalIgnoreCase))
                p = p.Substring("project://database/".Length);
            var q = p.IndexOf('?');
            if (q >= 0) p = p.Substring(0, q);
            return p;
        }

        /// <summary>クリック時に呼ばれるコールバックを登録します。</summary>
        public void RegisterClicked(Action<ClickEvent> callback)
        {
            RegisterCallback<ClickEvent>(evt => callback(evt));
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateContentSizes();
        }

        private void UpdateContentSizes()
        {
            var w = resolvedStyle.width;
            var h = resolvedStyle.height;
            if (float.IsNaN(w) || float.IsNaN(h) || w <= 0 || h <= 0) return;

            var minSize = Mathf.Min(w, h);
            var iconSize = Mathf.Max(20, minSize * 0.45f);
            var fontSize = Mathf.Max(12, minSize * 0.25f);

            var icon = ButtonIcon;
            if (icon != null)
            {
                icon.style.width = iconSize;
                icon.style.height = iconSize;
            }

            var text = ButtonText;
            if (text != null)
                text.style.fontSize = fontSize;
        }

        private void OnMouseEnter(MouseEnterEvent evt)
        {
            // 大元の親（this）で判定: TrickleDown でルートが先に受け取るため、子の上にマウスが入ってもここでホバー ON
            SetBodyHover(true);
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            // ルートで TrickleDown 登録しているため、Unity は「要素とその子孫の外にマウスが出たとき」にルートへ MouseLeave を送る
            SetBodyHover(false);
            SetBodyActive(false);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0)
                SetBodyActive(true);
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.button == 0)
                SetBodyActive(false);
        }

        private void SetBodyHover(bool hover)
        {
            var body = ButtonBody;
            if (body != null)
            {
                if (hover) body.AddToClassList("hover");
                else body.RemoveFromClassList("hover");
            }
        }

        private void SetBodyActive(bool active)
        {
            var body = ButtonBody;
            if (body != null)
            {
                if (active) body.AddToClassList("active");
                else body.RemoveFromClassList("active");
            }
        }
    }
}
#endif
