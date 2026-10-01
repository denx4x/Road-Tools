using UnityEditor;
using UnityEngine;

namespace Dyma.SplineLevelToolkit.Editor
{
    internal static class RoadToolsWindowStyles
    {
        private static bool cachedDarkTheme;
        private static GUIStyle header;
        private static GUIStyle card;
        private static GUIStyle title;
        private static GUIStyle subtitle;
        private static GUIStyle sectionTitle;
        private static GUIStyle body;
        private static Texture2D headerTexture;
        private static Texture2D cardTexture;

        internal static GUIStyle Header { get { EnsureStyles(); return header; } }
        internal static GUIStyle Card { get { EnsureStyles(); return card; } }
        internal static GUIStyle Title { get { EnsureStyles(); return title; } }
        internal static GUIStyle Subtitle { get { EnsureStyles(); return subtitle; } }
        internal static GUIStyle SectionTitle { get { EnsureStyles(); return sectionTitle; } }
        internal static GUIStyle Body { get { EnsureStyles(); return body; } }

        private static void EnsureStyles()
        {
            bool dark = EditorGUIUtility.isProSkin;
            if (header != null && cachedDarkTheme == dark)
                return;

            if (headerTexture != null) Object.DestroyImmediate(headerTexture);
            if (cardTexture != null) Object.DestroyImmediate(cardTexture);
            cachedDarkTheme = dark;
            headerTexture = MakeTexture(dark ? new Color(0.16f, 0.21f, 0.25f) : new Color(0.83f, 0.90f, 0.93f));
            cardTexture = MakeTexture(dark ? new Color(0.20f, 0.22f, 0.24f) : new Color(0.94f, 0.95f, 0.96f));

            header = new GUIStyle { padding = new RectOffset(14, 12, 12, 11), margin = new RectOffset(0, 0, 0, 6) };
            header.normal.background = headerTexture;
            card = new GUIStyle { padding = new RectOffset(12, 12, 10, 12), margin = new RectOffset(8, 8, 0, 0) };
            card.normal.background = cardTexture;
            title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            title.normal.textColor = dark ? new Color(0.82f, 0.93f, 0.96f) : new Color(0.08f, 0.24f, 0.31f);
            subtitle = new GUIStyle(EditorStyles.miniLabel);
            subtitle.normal.textColor = dark ? new Color(0.67f, 0.75f, 0.78f) : new Color(0.28f, 0.40f, 0.45f);
            sectionTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            sectionTitle.normal.textColor = dark ? new Color(0.65f, 0.85f, 0.91f) : new Color(0.11f, 0.37f, 0.45f);
            body = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            body.normal.textColor = dark ? new Color(0.75f, 0.77f, 0.79f) : new Color(0.30f, 0.33f, 0.35f);
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
