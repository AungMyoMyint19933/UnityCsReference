// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.UIElements.Debugger
{
    [Flags]
    internal enum OverlayContent
    {
        Content = 1 << 0,
        Padding = 1 << 1,
        Border = 1 << 2,
        Margin = 1 << 3,
        All = Content | Padding | Border | Margin
    }

    internal class OverlayData
    {
        public OverlayData(VisualElement ve, float alpha)
        {
            this.element = ve;
            this.alpha = alpha;
            this.defaultAlpha = alpha;
            this.fadeOutRate = 0;
        }

        public VisualElement element;
        public float alpha;
        public float defaultAlpha;
        public float fadeOutRate;
        public OverlayContent content;
    }

    internal abstract class BaseOverlayPainter
    {
        protected Dictionary<VisualElement, OverlayData> m_OverlayData = new Dictionary<VisualElement, OverlayData>();
        protected List<VisualElement> m_CleanUpOverlay = new List<VisualElement>();

        public void Draw()
        {
            Draw(GUIClip.topmostRect);
        }

        public virtual void Draw(Rect clipRect)
        {
            PaintAllOverlay(clipRect);

            foreach (var ve in m_CleanUpOverlay)
            {
                m_OverlayData.Remove(ve);
            }
            m_CleanUpOverlay.Clear();
        }

        private void PaintAllOverlay(Rect clipRect)
        {
            using (new GUIClip.ParentClipScope(Matrix4x4.identity, clipRect))
            {
                HandleUtility.ApplyWireMaterial();
                GL.PushMatrix();

                foreach (var kvp in m_OverlayData)
                {
                    var overlayData = kvp.Value;
                    overlayData.alpha -= overlayData.fadeOutRate;

                    DrawOverlayData(overlayData);
                    if (overlayData.alpha < Mathf.Epsilon)
                    {
                        m_CleanUpOverlay.Add(kvp.Key);
                    }
                }

                GL.PopMatrix();
            }
        }

        public int overlayCount
        {
            get { return m_OverlayData.Count; }
        }

        public void ClearOverlay()
        {
            m_OverlayData.Clear();
        }

        protected abstract void DrawOverlayData(OverlayData overlayData);

        protected void DrawRect(Rect rect, Color color, float alpha)
        {
            float x0 = rect.x;
            float x3 = rect.xMax;
            float y0 = rect.yMax;
            float y3 = rect.y;

            color.a = alpha;

            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            GL.Vertex3(x0, y0, 0);
            GL.Vertex3(x3, y0, 0);
            GL.Vertex3(x0, y3, 0);

            GL.Vertex3(x3, y0, 0);
            GL.Vertex3(x3, y3, 0);
            GL.Vertex3(x0, y3, 0);
            GL.End();
        }

        protected void DrawBorder(Rect rect, Color color, float alpha)
        {
            rect.xMin++;
            rect.xMax--;
            rect.yMin++;
            rect.yMax--;

            color.a = alpha;

            GL.Begin(GL.LINES);
            GL.Color(color);
            GL.Vertex3(rect.xMin, rect.yMin, 0);
            GL.Vertex3(rect.xMax, rect.yMin, 0);

            GL.Vertex3(rect.xMax, rect.yMin, 0);
            GL.Vertex3(rect.xMax, rect.yMax, 0);

            GL.Vertex3(rect.xMax, rect.yMax, 0);
            GL.Vertex3(rect.xMin, rect.yMax, 0);

            GL.Vertex3(rect.xMin, rect.yMax, 0);
            GL.Vertex3(rect.xMin, rect.yMin, 0);
            GL.End();
        }
    }

    internal class HighlightOverlayPainter : BaseOverlayPainter
    {
        private static readonly float kDefaultHighlightAlpha = 0.4f;
        private static readonly Color kHighlightContentColor = new Color(0.1f, 0.6f, 0.9f);
        private static readonly Color kHighlightPaddingColor = new Color(0.1f, 0.9f, 0.1f);
        private static readonly Color kHighlightBorderColor = new Color(1.0f, 1.0f, 0.4f);
        private static readonly Color kHighlightMarginColor = new Color(1.0f, 0.6f, 0.0f);

        private Rect[] m_MarginRects = new Rect[4];
        private Rect[] m_BorderRects = new Rect[4];
        private Rect[] m_PaddingRects = new Rect[4];

        public void AddOverlay(VisualElement ve, OverlayContent content = OverlayContent.All)
        {
            OverlayData overlayData = null;
            if (!m_OverlayData.TryGetValue(ve, out overlayData))
            {
                overlayData = new OverlayData(ve, kDefaultHighlightAlpha);
                m_OverlayData[ve] = overlayData;
            }

            overlayData.content = content;
        }

        protected override void DrawOverlayData(OverlayData od)
        {
            DrawHighlights(od);
        }

        private void DrawHighlights(OverlayData od)
        {
            var ve = od.element;
            Rect contentRect = ve.LocalToWorld(ve.contentRect);

            FillHighlightRects(od.element);

            var contentFlag = od.content;
            if ((contentFlag & OverlayContent.Content) == OverlayContent.Content)
            {
                DrawRect(contentRect, kHighlightContentColor, od.alpha);
            }

            if ((contentFlag & OverlayContent.Padding) == OverlayContent.Padding)
            {
                for (int i = 0; i < 4; i++)
                {
                    DrawRect(m_PaddingRects[i], kHighlightPaddingColor, od.alpha);
                }
            }

            if ((contentFlag & OverlayContent.Border) == OverlayContent.Border)
            {
                for (int i = 0; i < 4; i++)
                {
                    DrawRect(m_BorderRects[i], kHighlightBorderColor, od.alpha);
                }
            }

            if ((contentFlag & OverlayContent.Margin) == OverlayContent.Margin)
            {
                for (int i = 0; i < 4; i++)
                {
                    DrawRect(m_MarginRects[i], kHighlightMarginColor, od.alpha);
                }
            }
        }

        private void FillHighlightRects(VisualElement ve)
        {
            var style = ve.resolvedStyle;
            Rect contentRect = ve.LocalToWorld(ve.contentRect);

            // Paddings
            float paddingLeft = style.paddingLeft;
            float paddingRight = style.paddingRight;
            float paddingBottom = style.paddingBottom;
            float paddingTop = style.paddingTop;

            Rect paddingLeftRect = Rect.zero;
            Rect paddingRightRect = Rect.zero;
            Rect paddingBottomRect = Rect.zero;
            Rect paddingTopRect = Rect.zero;

            paddingLeftRect = new Rect(contentRect.xMin - paddingLeft, contentRect.yMin,
                paddingLeft, contentRect.height);

            paddingRightRect = new Rect(contentRect.xMax, contentRect.yMin,
                paddingRight, contentRect.height);

            paddingTopRect = new Rect(contentRect.xMin - paddingLeft, contentRect.yMin - paddingTop,
                contentRect.width + paddingLeft + paddingRight, paddingTop);

            paddingBottomRect = new Rect(contentRect.xMin - paddingLeft, contentRect.yMax,
                contentRect.width + paddingLeft + paddingRight, paddingBottom);

            m_PaddingRects[0] = paddingLeftRect;
            m_PaddingRects[1] = paddingRightRect;
            m_PaddingRects[2] = paddingTopRect;
            m_PaddingRects[3] = paddingBottomRect;

            // Borders
            float borderLeft = style.borderLeftWidth;
            float borderRight = style.borderRightWidth;
            float borderBottom = style.borderBottomWidth;
            float borderTop = style.borderTopWidth;

            Rect borderLeftRect = Rect.zero;
            Rect borderRightRect = Rect.zero;
            Rect borderBottomRect = Rect.zero;
            Rect borderTopRect = Rect.zero;

            borderLeftRect = new Rect(paddingLeftRect.xMin - borderLeft, paddingTopRect.yMin,
                borderLeft, paddingLeftRect.height + paddingBottomRect.height + paddingTopRect.height);

            borderRightRect = new Rect(paddingRightRect.xMax, paddingTopRect.yMin,
                borderRight, paddingRightRect.height + paddingBottomRect.height + paddingTopRect.height);

            borderTopRect = new Rect(paddingTopRect.xMin - borderLeft, paddingTopRect.yMin - borderTop,
                paddingTopRect.width + borderLeft + borderRight, borderTop);

            borderBottomRect = new Rect(paddingBottomRect.xMin - borderLeft, paddingBottomRect.yMax,
                paddingBottomRect.width + borderLeft + borderRight, borderBottom);

            m_BorderRects[0] = borderLeftRect;
            m_BorderRects[1] = borderRightRect;
            m_BorderRects[2] = borderTopRect;
            m_BorderRects[3] = borderBottomRect;

            // Margins
            float marginLeft = style.marginLeft;
            float marginRight = style.marginRight;
            float marginBotton = style.marginBottom;
            float marginTop = style.marginTop;

            Rect marginLeftRect = Rect.zero;
            Rect marginRightRect = Rect.zero;
            Rect marginBottomRect = Rect.zero;
            Rect marginTopRect = Rect.zero;

            marginLeftRect = new Rect(borderLeftRect.xMin - marginLeft, borderTopRect.yMin,
                marginLeft, borderLeftRect.height + borderBottomRect.height + borderTopRect.height);

            marginRightRect = new Rect(borderRightRect.xMax, borderTopRect.yMin,
                marginRight, borderRightRect.height + borderBottomRect.height + borderTopRect.height);

            marginTopRect = new Rect(borderTopRect.xMin - marginLeft, borderTopRect.yMin - marginTop,
                borderTopRect.width + marginLeft + marginRight, marginTop);

            marginBottomRect = new Rect(borderBottomRect.xMin - marginLeft, borderBottomRect.yMax,
                borderBottomRect.width + marginLeft + marginRight, marginBotton);

            m_MarginRects[0] = marginLeftRect;
            m_MarginRects[1] = marginRightRect;
            m_MarginRects[2] = marginTopRect;
            m_MarginRects[3] = marginBottomRect;
        }
    }

    internal class RepaintOverlayPainter : BaseOverlayPainter
    {
        private static readonly Color kRepaintColor = Color.green;
        private static readonly float kOverlayFadeOut = 0.01f;
        private static readonly float kDefaultRepaintAlpha = 0.2f;

        public void AddOverlay(VisualElement ve)
        {
            OverlayData overlayData = null;
            if (!m_OverlayData.TryGetValue(ve, out overlayData))
            {
                overlayData = new OverlayData(ve, kDefaultRepaintAlpha) { fadeOutRate = kOverlayFadeOut };
                m_OverlayData[ve] = overlayData;
            }
            else
            {
                // Reset alpha
                overlayData.alpha = overlayData.defaultAlpha;
            }
        }

        protected override void DrawOverlayData(OverlayData od)
        {
            DrawRect(od.element.worldBound, kRepaintColor, od.alpha);
            DrawBorder(od.element.worldBound, kRepaintColor, od.alpha * 4);
        }
    }

    internal class LayoutOverlayPainter : BaseOverlayPainter
    {
        private static readonly float kDefaultAlpha = 1.0f;
        private static readonly Color kBoundColor = Color.gray;
        private static readonly Color kSelectedBoundColor = Color.green;

        public VisualElement selectedElement;

        public void AddOverlay(VisualElement ve)
        {
            OverlayData overlayData = null;
            if (!m_OverlayData.TryGetValue(ve, out overlayData))
            {
                overlayData = new OverlayData(ve, kDefaultAlpha);
                m_OverlayData[ve] = overlayData;
            }
        }

        public override void Draw(Rect clipRect)
        {
            base.Draw(clipRect);

            if (selectedElement != null)
                DrawBorder(selectedElement.worldBound, kSelectedBoundColor, kDefaultAlpha);
        }

        protected override void DrawOverlayData(OverlayData od)
        {
            DrawBorder(od.element.worldBound, kBoundColor, od.alpha);
        }
    }
}
