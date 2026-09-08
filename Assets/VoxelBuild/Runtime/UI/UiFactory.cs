using System;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelBuild.UI
{
    /// <summary>Small helpers for building uGUI at runtime without prefabs.</summary>
    public static class UiFactory
    {
        public static Font Font;

        public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.11f, 0.86f);
        public static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        public static readonly Color ButtonActiveColor = new Color(0.85f, 0.6f, 0.2f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.92f, 0.9f, 1f);
        public static readonly Color DimTextColor = new Color(0.65f, 0.65f, 0.65f, 1f);

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = true;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = ScaleFor(Screen.width, Screen.height);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>User-adjustable multiplier on top of the automatic resolution scaling.</summary>
        public static float UserScale = 1f;

        /// <summary>
        /// UI scale: never below 1 (text stays at native pixel size on small views) and grows on
        /// high-resolution displays so a 4K screen gets roughly the same physical layout as 1080p.
        /// </summary>
        public static float ScaleFor(int width, int height)
        {
            float byHeight = height / 1080f;
            float byWidth = width / 1920f;
            float auto = Mathf.Max(1f, Mathf.Min(byHeight, byWidth));
            return auto * UserScale;
        }

        public static void ApplyScale(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.scaleFactor = ScaleFor(Screen.width, Screen.height);
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return (RectTransform)go.transform;
        }

        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors a rect to a corner/edge with an offset and size.</summary>
        public static void Anchor(RectTransform rt, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static VerticalLayoutGroup Vertical(RectTransform rt, float spacing, int padding, bool fitHeight = true, bool fitWidth = false)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            if (fitHeight || fitWidth)
            {
                var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = fitHeight ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
                fitter.horizontalFit = fitWidth ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            }
            return v;
        }

        public static HorizontalLayoutGroup Horizontal(RectTransform rt, float spacing, int padding, bool fitWidth = false)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            if (fitWidth)
            {
                var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            return h;
        }

        public static RectTransform Row(Transform parent, string name, float spacing = 4f)
        {
            var rt = Group(parent, name);
            Horizontal(rt, spacing, 0);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 24f;
            return rt;
        }

        public static Text Label(Transform parent, string text, int size = 15, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.text = text;
            t.alignment = align;
            t.color = color ?? TextColor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = size + 6;
            le.flexibleWidth = 1f;
            return t;
        }

        public static Button Button(Transform parent, string text, Action onClick, float width = 0f, float height = 30f, int fontSize = 15)
        {
            var go = new GameObject("Button " + text, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = ButtonColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.normalColor = Color.white;
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }
            else le.flexibleWidth = 1f;

            var label = Label(go.transform, text, fontSize, TextAnchor.MiddleCenter);
            Stretch((RectTransform)label.transform, Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
            Destroy(label.GetComponent<LayoutElement>());
            return btn;
        }

        public static void SetActive(Button btn, bool active)
        {
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = active ? ButtonActiveColor : ButtonColor;
        }

        public static void SetText(Button btn, string text)
        {
            var t = btn.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        /// <summary>A horizontal bar. Returns the fill rect; set its anchorMax.x to the 0..1 value.</summary>
        public static RectTransform Bar(Transform parent, Color color, float height = 8f)
        {
            var bg = Panel(parent, "Bar", new Color(0f, 0f, 0f, 0.5f));
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            var fill = Panel(bg, "Fill", color);
            Stretch(fill, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            return fill;
        }

        public static void SetBar(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        private static void Destroy(UnityEngine.Object o)
        {
            if (o != null) UnityEngine.Object.Destroy(o);
        }
    }
}
