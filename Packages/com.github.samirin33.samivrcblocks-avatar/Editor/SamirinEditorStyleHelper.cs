using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Samirin33.Editor
{
    public static class SamirinEditorStyleHelper
    {
        private const string EditorFontGUID = "04057a843a9bbc940b6ccd557401ad44";
        private const string BackgroundImageGUID = "cd4f6a17398736c48a4cfa0d8c9f6cb5";

        private const float BackgroundAlpha = 0.3f;
        private static readonly Color BackgroundTintColor = new Color(1f, 1f, 1f, 1f);

        private static RectOffset _backgroundMargin;
        private static RectOffset BackgroundMargin => _backgroundMargin ??= new RectOffset(8, 8, 8, 8);

        private static RectOffset _backgroundPadding;
        private static RectOffset BackgroundPadding => _backgroundPadding ??= new RectOffset(12, 12, 12, 12);

        private static readonly Color OutlineColor = new Color(0.3f, 0.12f, 0.3f, 0.3f);
        private const int CornerRadius = 8;
        private const int OutlineWidth = 4;

        private static GUIStyle _blueBackgroundStyle;
        private static GUIStyle _minimalBoxStyle;
        private static Font _editorFont;
        private static Font _defaultFont;
        private static Texture2D _backgroundTexture;
        private static Texture2D _backgroundTintedTexture;
        private static Texture2D _backgroundFinalTexture;
        private static Color _cachedBackgroundColor;
        private static Material _backgroundMaterial;

        private static readonly Dictionary<int, float> _hoverScaleValues = new Dictionary<int, float>();

        private static Font EditorFont
        {
            get
            {
                if (_editorFont == null)
                {
                    var path = AssetDatabase.GUIDToAssetPath(EditorFontGUID);
                    _editorFont = AssetDatabase.LoadAssetAtPath<Font>(path);
                }
                return _editorFont;
            }
        }

        private static Texture2D BackgroundTexture
        {
            get
            {
                if (_backgroundTexture == null)
                {
                    var path = AssetDatabase.GUIDToAssetPath(BackgroundImageGUID);
                    if (!string.IsNullOrEmpty(path))
                    {
                        _backgroundTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                        if (_backgroundTexture == null)
                        {
                            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                            if (sprite != null) _backgroundTexture = sprite.texture;
                        }
                        if (_backgroundTexture == null)
                        {
                            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                            if (obj is Texture2D t) _backgroundTexture = t;
                            else if (obj is Sprite s) _backgroundTexture = s.texture;
                        }
                    }
                }
                return _backgroundTexture;
            }
        }

        private static Material BackgroundMaterial
        {
            get
            {
                if (_backgroundMaterial == null)
                {
                    var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                    if (shader != null)
                        _backgroundMaterial = new Material(shader);
                }
                return _backgroundMaterial;
            }
        }

        private static Texture2D GetTintedBackgroundTexture()
        {
            var source = BackgroundTexture;
            if (source == null) return null;

            var drawColor = new Color(BackgroundTintColor.r, BackgroundTintColor.g, BackgroundTintColor.b, BackgroundAlpha);
            if (_backgroundTintedTexture != null && _cachedBackgroundColor == drawColor)
                return _backgroundTintedTexture;

            if (_backgroundFinalTexture != null) { UnityEngine.Object.DestroyImmediate(_backgroundFinalTexture); _backgroundFinalTexture = null; }
            var mat = BackgroundMaterial;
            if (mat == null) return source;

            mat.SetColor("_Color", drawColor);
            var maxSize = 512;
            var aspect = (float)source.width / source.height;
            int w, h;
            if (aspect >= 1f)
            {
                w = Mathf.Min(source.width, maxSize);
                h = Mathf.RoundToInt(w / aspect);
            }
            else
            {
                h = Mathf.Min(source.height, maxSize);
                w = Mathf.RoundToInt(h * aspect);
            }
            w = Mathf.Max(w, 1);
            h = Mathf.Max(h, 1);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt, mat);
            if (_backgroundTintedTexture == null || _backgroundTintedTexture.width != w || _backgroundTintedTexture.height != h)
            {
                if (_backgroundTintedTexture != null) UnityEngine.Object.DestroyImmediate(_backgroundTintedTexture);
                _backgroundTintedTexture = new Texture2D(w, h);
                if (_backgroundFinalTexture != null) { UnityEngine.Object.DestroyImmediate(_backgroundFinalTexture); _backgroundFinalTexture = null; }
            }
            _backgroundTintedTexture.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            _backgroundTintedTexture.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            _cachedBackgroundColor = drawColor;
            return _backgroundTintedTexture;
        }

        private static bool IsInsideRoundedRect(float x, float y, int w, int h, float r)
        {
            if (r <= 0) return x >= 0 && x < w && y >= 0 && y < h;
            if (x >= r && x < w - r && y >= r && y < h - r) return true;
            if (x >= r && x < w - r && y >= 0 && y < r) return true;
            if (x >= r && x < w - r && y >= h - r && y < h) return true;
            if (x >= 0 && x < r && y >= r && y < h - r) return true;
            if (x >= w - r && x < w && y >= r && y < h - r) return true;
            if (x < r && y < r) return (x - r) * (x - r) + (y - r) * (y - r) <= r * r;
            if (x >= w - r && y < r) return (x - (w - r)) * (x - (w - r)) + (y - r) * (y - r) <= r * r;
            if (x < r && y >= h - r) return (x - r) * (x - r) + (y - (h - r)) * (y - (h - r)) <= r * r;
            if (x >= w - r && y >= h - r) return (x - (w - r)) * (x - (w - r)) + (y - (h - r)) * (y - (h - r)) <= r * r;
            return false;
        }

        private static bool IsInsideConcentricInner(float x, float y, int w, int h, float rOuter, float rInner)
        {
            if (rInner <= 0) return false;
            if (x >= rOuter && x < w - rOuter && y >= rOuter && y < h - rOuter) return true;
            if (x >= rOuter && x < w - rOuter && y >= rInner && y < rOuter) return true;
            if (x >= rOuter && x < w - rOuter && y >= h - rOuter && y < h - rInner) return true;
            if (x >= rInner && x < rOuter && y >= rOuter && y < h - rOuter) return true;
            if (x >= w - rOuter && x < w - rInner && y >= rOuter && y < h - rOuter) return true;
            if (x < rOuter && y < rOuter) return (x - rOuter) * (x - rOuter) + (y - rOuter) * (y - rOuter) <= rInner * rInner;
            if (x >= w - rOuter && y < rOuter) return (x - (w - rOuter)) * (x - (w - rOuter)) + (y - rOuter) * (y - rOuter) <= rInner * rInner;
            if (x < rOuter && y >= h - rOuter) return (x - rOuter) * (x - rOuter) + (y - (h - rOuter)) * (y - (h - rOuter)) <= rInner * rInner;
            if (x >= w - rOuter && y >= h - rOuter) return (x - (w - rOuter)) * (x - (w - rOuter)) + (y - (h - rOuter)) * (y - (h - rOuter)) <= rInner * rInner;
            return false;
        }

        private static bool IsOnOutline(float x, float y, int w, int h, float rOuter, float rInner)
        {
            if (!IsInsideRoundedRect(x, y, w, h, rOuter)) return false;
            if (rInner <= 0) return true;
            return !IsInsideConcentricInner(x, y, w, h, rOuter, rInner);
        }

        private static Texture2D ApplyRoundedRectAndOutline(Texture2D source)
        {
            if (source == null) return null;
            if (!source.isReadable)
            {
                Debug.LogWarning("[SamirinEditorStyleHelper] Background texture is not readable. Outline will be skipped.");
                return source;
            }
            var w = source.width;
            var h = source.height;
            var r = Mathf.Min((float)CornerRadius, Mathf.Min(w, h) * 0.5f);
            var rInner = Mathf.Max(0, r - OutlineWidth);
            var pixels = source.GetPixels();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var px = x + 0.5f;
                    var py = h - 1 - y + 0.5f;
                    if (IsOnOutline(px, py, w, h, r, rInner))
                    {
                        pixels[i] = OutlineColor;
                    }
                    else if (!IsInsideRoundedRect(px, py, w, h, r))
                    {
                        pixels[i] = new Color(0, 0, 0, 0);
                    }
                }
            }
            var result = new Texture2D(w, h);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        private static Texture2D GetFinalBackgroundTexture()
        {
            var tinted = GetTintedBackgroundTexture();
            if (tinted == null) return null;
            if (_backgroundFinalTexture != null && _backgroundFinalTexture.width == tinted.width && _backgroundFinalTexture.height == tinted.height)
                return _backgroundFinalTexture;
            if (_backgroundFinalTexture != null) UnityEngine.Object.DestroyImmediate(_backgroundFinalTexture);
            _backgroundFinalTexture = ApplyRoundedRectAndOutline(tinted);
            return _backgroundFinalTexture;
        }

        private static GUIStyle BlueBackgroundStyle
        {
            get
            {
                if (_blueBackgroundStyle == null)
                    _blueBackgroundStyle = new GUIStyle();
                _blueBackgroundStyle.margin = BackgroundMargin;
                _blueBackgroundStyle.padding = BackgroundPadding;
                _blueBackgroundStyle.border = new RectOffset(CornerRadius, CornerRadius, CornerRadius, CornerRadius);
                var tex = GetFinalBackgroundTexture();
                if (tex != null)
                    _blueBackgroundStyle.normal.background = tex;
                return _blueBackgroundStyle;
            }
        }

        private static GUIStyle MinimalBoxStyle
        {
            get
            {
                if (_minimalBoxStyle == null)
                {
                    _minimalBoxStyle = new GUIStyle();
                    _minimalBoxStyle.margin = BackgroundMargin;
                    _minimalBoxStyle.padding = BackgroundPadding;
                    _minimalBoxStyle.border = new RectOffset(0, 0, 0, 0);
                }
                return _minimalBoxStyle;
            }
        }

        public static void DrawWithBlueBackground(Action drawContent)
        {
            var style = SamirinEditorPreferences.UseCustomBackground ? BlueBackgroundStyle : MinimalBoxStyle;
            EditorGUILayout.BeginVertical(style, GUILayout.ExpandWidth(true));
            DrawWithBlueBackgroundContent(drawContent);
        }

        /// <summary>
        /// <see cref="EditorWindow.position"/> 全体など、指定の矩形内に青背景（またはミニマル枠）を拡大して塗る。
        /// 内部で <c>GUILayout.BeginArea</c> を行う。
        /// </summary>
        public static void DrawWithBlueBackground(Action drawContent, Rect area)
        {
            var style = SamirinEditorPreferences.UseCustomBackground ? BlueBackgroundStyle : MinimalBoxStyle;
            GUILayout.BeginArea(area);
            try
            {
                EditorGUILayout.BeginVertical(style, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawWithBlueBackgroundContent(drawContent);
            }
            finally
            {
                GUILayout.EndArea();
            }
        }

        /// <summary>
        /// <see cref="UnityEditor.Editor.OnInspectorGUI"/> や <see cref="UnityEditor.SettingsProvider"/>.<c>OnGUI</c> 用。
        /// インスペクターは親の GUILayout に従う必要があるため、<see cref="GUILayout.BeginArea"/> は使わない。
        /// </summary>
        public static void DrawWithBlueBackgroundForInspectorOrSettings(Action drawContent)
        {
            DrawWithBlueBackground(drawContent);
        }

        /// <summary>
        /// <see cref="UnityEditor.SettingsProvider"/>.<c>OnGUI</c> 用（<see cref="DrawWithBlueBackgroundForInspectorOrSettings"/> と同じ挙動）。
        /// </summary>
        public static void DrawWithBlueBackgroundForSettingsGui(Action drawContent)
        {
            DrawWithBlueBackgroundForInspectorOrSettings(drawContent);
        }

        private static void DrawWithBlueBackgroundContent(Action drawContent)
        {
            Font previousFont = null;
            if (SamirinEditorPreferences.UseCustomFont && EditorFont != null)
            {
                previousFont = GUI.skin.font;
                if (_defaultFont == null) _defaultFont = previousFont;
                GUI.skin.font = EditorFont;
            }
            try
            {
                drawContent?.Invoke();
            }
            finally
            {
                if (previousFont != null)
                {
                    GUI.skin.font = previousFont;
                }
            }
            EditorGUILayout.EndVertical();
        }

        public static void DrawHelpBoxWithDefaultFont(string message, MessageType type)
        {
            DrawWithDefaultFont(() => EditorGUILayout.HelpBox(message, type));
        }

        public static void DrawWithDefaultFont(Action drawAction)
        {
            var prevFont = GUI.skin.font;
            GUI.skin.font = _defaultFont ?? EditorStyles.helpBox.font;
            try
            {
                drawAction?.Invoke();
            }
            finally
            {
                GUI.skin.font = prevFont;
            }
        }

        public static float GetBlinkFactor01(float speed = 2f)
        {
            if (!SamirinEditorPreferences.EnableRealtimeAnimation)
                return 0.5f;
            var t = (float)EditorApplication.timeSinceStartup * speed * Mathf.PI * 2f;
            return (Mathf.Sin(t) + 1f) * 0.5f;
        }

        public static bool GetBlinkState(float speed = 2f)
        {
            if (!SamirinEditorPreferences.EnableRealtimeAnimation)
                return true;
            var t = (float)EditorApplication.timeSinceStartup * speed * Mathf.PI * 2f;
            return Mathf.Sin(t) >= 0f;
        }

        public static Color GetBlinkColor(Color baseColor, Color targetColor, float speed = 2f)
        {
            if (!SamirinEditorPreferences.EnableRealtimeAnimation)
                return baseColor;
            var lerp = GetBlinkFactor01(speed);
            return Color.Lerp(baseColor, targetColor, lerp);
        }

        public static bool IsMouseHover(Rect rect)
        {
            var current = Event.current;
            if (current == null) return false;
            return rect.Contains(current.mousePosition);
        }

        public static void DrawHoverScale(Rect rect, Action drawContent, float baseScale = 1f, float hoverScale = 1.05f, float animationSpeed = 10f)
        {
            if (drawContent == null) return;
            if (!SamirinEditorPreferences.EnableRealtimeAnimation)
            {
                drawContent();
                return;
            }

            var id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var isHover = IsMouseHover(rect);

            if (!_hoverScaleValues.TryGetValue(id, out var currentScale))
            {
                currentScale = baseScale;
            }

            var targetScale = isHover ? hoverScale : baseScale;
            var smooth = 1f - Mathf.Exp(-animationSpeed * 0.02f);
            currentScale = Mathf.Lerp(currentScale, targetScale, smooth);
            _hoverScaleValues[id] = currentScale;

            var pivot = rect.center;
            var previousMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(currentScale, currentScale), pivot);
            try
            {
                drawContent();
            }
            finally
            {
                GUI.matrix = previousMatrix;
            }
        }
    }
}
