using System;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public static class GameplayLayoutMetrics
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float HorizontalReservedPixels = 64f;
        public const float TopHudReservedPixels = 280f;
        public const float BottomHudReservedPixels = 240f;
        public const float BoardPaddingWorld = 0.35f;

        public static Rect BoardViewport => new Rect(
            HorizontalReservedPixels / ReferenceWidth,
            BottomHudReservedPixels / ReferenceHeight,
            (ReferenceWidth - HorizontalReservedPixels * 2f) / ReferenceWidth,
            (ReferenceHeight - TopHudReservedPixels - BottomHudReservedPixels) /
            ReferenceHeight);
    }

    public readonly struct BoardViewportFit
    {
        public float OrthographicSize { get; }
        public Vector2 CameraCenter { get; }
        public Rect PlayableWorldRect { get; }

        internal BoardViewportFit(float orthographicSize, Vector2 cameraCenter,
            Rect playableWorldRect)
        {
            OrthographicSize = orthographicSize;
            CameraCenter = cameraCenter;
            PlayableWorldRect = playableWorldRect;
        }
    }

    public static class BoardViewportFitter
    {
        public static BoardViewportFit Calculate(Bounds boardBounds, float cameraAspect,
            Rect normalizedViewport, float padding)
        {
            if (cameraAspect <= 0f || float.IsNaN(cameraAspect) || float.IsInfinity(cameraAspect))
                throw new ArgumentOutOfRangeException(nameof(cameraAspect));
            if (normalizedViewport.width <= 0f || normalizedViewport.height <= 0f ||
                normalizedViewport.xMin < 0f || normalizedViewport.yMin < 0f ||
                normalizedViewport.xMax > 1f || normalizedViewport.yMax > 1f)
                throw new ArgumentOutOfRangeException(nameof(normalizedViewport));
            if (padding < 0f || float.IsNaN(padding) || float.IsInfinity(padding))
                throw new ArgumentOutOfRangeException(nameof(padding));

            float paddedWidth = boardBounds.size.x + padding * 2f;
            float paddedHeight = boardBounds.size.y + padding * 2f;
            float widthFit = paddedWidth / (2f * cameraAspect * normalizedViewport.width);
            float heightFit = paddedHeight / (2f * normalizedViewport.height);
            float orthographicSize = Mathf.Max(0.01f, widthFit, heightFit);

            float fullWorldHeight = orthographicSize * 2f;
            float fullWorldWidth = fullWorldHeight * cameraAspect;
            Vector2 viewportCenter = normalizedViewport.center;
            var cameraCenter = new Vector2(
                boardBounds.center.x - (viewportCenter.x - 0.5f) * fullWorldWidth,
                boardBounds.center.y - (viewportCenter.y - 0.5f) * fullWorldHeight);
            var playableWorldRect = new Rect(
                cameraCenter.x + (normalizedViewport.xMin - 0.5f) * fullWorldWidth,
                cameraCenter.y + (normalizedViewport.yMin - 0.5f) * fullWorldHeight,
                normalizedViewport.width * fullWorldWidth,
                normalizedViewport.height * fullWorldHeight);
            return new BoardViewportFit(orthographicSize, cameraCenter, playableWorldRect);
        }
    }
}
