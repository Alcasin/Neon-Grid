using NeonGrid.Data;
using NeonGrid.Presentation;
using NeonGrid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace NeonGrid.Tests
{
    public sealed class BoardViewportFitterTests
    {
        [TestCase(3, 1, 1080, 1920)]
        [TestCase(3, 3, 1080, 2340)]
        [TestCase(4, 3, 720, 1280)]
        [TestCase(4, 4, 1080, 1920)]
        [TestCase(4, 4, 1080, 2340)]
        [TestCase(4, 4, 720, 1280)]
        [TestCase(8, 3, 1080, 2340)]
        [TestCase(3, 8, 720, 1280)]
        public void Calculate_ContainsPaddedBoardInsidePlayableRegion(
            int width, int height, int screenWidth, int screenHeight)
        {
            var bounds = new Bounds(new Vector3(2.5f, -1.25f, 0f),
                new Vector3(width - 0.1f, height - 0.1f, 0f));
            float aspect = screenWidth / (float)screenHeight;
            Rect viewport = GameplayLayoutMetrics.BoardViewport;

            BoardViewportFit fit = BoardViewportFitter.Calculate(bounds, aspect, viewport,
                GameplayLayoutMetrics.BoardPaddingWorld);

            Assert.That(fit.OrthographicSize, Is.GreaterThan(0f));
            AssertContains(fit, bounds, aspect, viewport,
                GameplayLayoutMetrics.BoardPaddingWorld);
        }

        [TestCase("PS_01")]
        [TestCase("PS_02")]
        [TestCase("PS_03")]
        [TestCase("PS_04")]
        [TestCase("PS_05")]
        [TestCase("PS_06")]
        [TestCase("PS_10")]
        public void ProductionBoardGeneratedBounds_FitPortraitWithoutClipping(string assetName)
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(
                $"Levels/PowerStation/{assetName}");
            var root = new GameObject($"{assetName} Viewport Fit Test");

            try
            {
                var view = root.AddComponent<BoardView>();
                view.Build(level.CreateBoardState(), _ => { });
                Physics2D.SyncTransforms();
                Bounds generatedBounds = view.GetWorldBounds();
                float aspect = 1080f / 1920f;
                Rect viewport = GameplayLayoutMetrics.BoardViewport;
                BoardViewportFit fit = BoardViewportFitter.Calculate(generatedBounds, aspect,
                    viewport, GameplayLayoutMetrics.BoardPaddingWorld);

                Assert.That(generatedBounds.size.x, Is.EqualTo(level.Width - 0.1f).Within(0.001f));
                Assert.That(generatedBounds.size.y, Is.EqualTo(level.Height - 0.1f).Within(0.001f));
                AssertContains(fit, generatedBounds, aspect, viewport,
                    GameplayLayoutMetrics.BoardPaddingWorld);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GameplayRegion_CentrallyReservesCurrentHudZones()
        {
            Rect viewport = GameplayLayoutMetrics.BoardViewport;

            Assert.That(viewport.xMin, Is.EqualTo(
                GameplayLayoutMetrics.HorizontalReservedPixels /
                GameplayLayoutMetrics.ReferenceWidth));
            Assert.That(viewport.yMin, Is.EqualTo(
                GameplayLayoutMetrics.BottomHudReservedPixels /
                GameplayLayoutMetrics.ReferenceHeight));
            Assert.That(viewport.yMax, Is.EqualTo(1f -
                GameplayLayoutMetrics.TopHudReservedPixels /
                GameplayLayoutMetrics.ReferenceHeight).Within(0.0001f));
            Assert.That(viewport.center.x, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void Calculate_UsesStricterWidthOrHeightRequirement()
        {
            Rect viewport = GameplayLayoutMetrics.BoardViewport;
            const float aspect = 0.5f;
            var wide = new Bounds(Vector3.zero, new Vector3(8f, 1f, 0f));
            var tall = new Bounds(Vector3.zero, new Vector3(1f, 8f, 0f));

            BoardViewportFit wideFit = BoardViewportFitter.Calculate(wide, aspect, viewport, 0f);
            BoardViewportFit tallFit = BoardViewportFitter.Calculate(tall, aspect, viewport, 0f);

            Assert.That(wideFit.OrthographicSize,
                Is.EqualTo(wide.size.x / (2f * aspect * viewport.width)).Within(0.0001f));
            Assert.That(tallFit.OrthographicSize,
                Is.EqualTo(tall.size.y / (2f * viewport.height)).Within(0.0001f));
        }

        [Test]
        public void BoardController_AppliesFitAndRefitsExistingCameraAfterAspectChange()
        {
            Camera camera = Camera.main;
            GameObject ownedCameraObject = null;
            if (camera == null)
            {
                ownedCameraObject = new GameObject("Responsive Fit Camera");
                ownedCameraObject.tag = "MainCamera";
                camera = ownedCameraObject.AddComponent<Camera>();
            }

            float previousAspect = camera.aspect;
            float previousSize = camera.orthographicSize;
            bool previousOrthographic = camera.orthographic;
            Vector3 previousPosition = camera.transform.position;
            int cameraCount = Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            int listenerCount = Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var root = new GameObject("Responsive Board Controller Test");

            try
            {
                camera.aspect = 1080f / 1920f;
                LevelDefinition level = Resources.Load<LevelDefinition>(
                    "Levels/PowerStation/PS_05");
                var controller = root.AddComponent<BoardController>();
                controller.Initialize(level);
                Physics2D.SyncTransforms();
                Bounds bounds = root.GetComponent<BoardView>().GetWorldBounds();
                BoardViewportFit first = BoardViewportFitter.Calculate(bounds, camera.aspect,
                    GameplayLayoutMetrics.BoardViewport, GameplayLayoutMetrics.BoardPaddingWorld);
                Assert.That(camera.orthographicSize,
                    Is.EqualTo(first.OrthographicSize).Within(0.0001f));
                Assert.That(camera.transform.position.x,
                    Is.EqualTo(first.CameraCenter.x).Within(0.0001f));
                Assert.That(camera.transform.position.y,
                    Is.EqualTo(first.CameraCenter.y).Within(0.0001f));

                float firstSize = camera.orthographicSize;
                camera.aspect = 1080f / 2340f;
                controller.RefreshBoardFraming(false);
                BoardViewportFit second = BoardViewportFitter.Calculate(bounds, camera.aspect,
                    GameplayLayoutMetrics.BoardViewport, GameplayLayoutMetrics.BoardPaddingWorld);

                Assert.That(camera.orthographicSize,
                    Is.EqualTo(second.OrthographicSize).Within(0.0001f));
                Assert.That(camera.orthographicSize, Is.GreaterThan(firstSize));
                Assert.That(camera.transform.position.x,
                    Is.EqualTo(second.CameraCenter.x).Within(0.0001f));
                Assert.That(camera.transform.position.y,
                    Is.EqualTo(second.CameraCenter.y).Within(0.0001f));
                Assert.That(Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                    Is.EqualTo(cameraCount));
                Assert.That(Object.FindObjectsByType<AudioListener>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
                    Is.EqualTo(listenerCount));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (ownedCameraObject != null)
                {
                    Object.DestroyImmediate(ownedCameraObject);
                }
                else
                {
                    camera.aspect = previousAspect;
                    camera.orthographicSize = previousSize;
                    camera.orthographic = previousOrthographic;
                    camera.transform.position = previousPosition;
                }
            }
        }

        private static void AssertContains(BoardViewportFit fit, Bounds bounds, float aspect,
            Rect viewport, float padding)
        {
            Assert.That(fit.PlayableWorldRect.xMin,
                Is.LessThanOrEqualTo(bounds.min.x - padding + 0.0001f));
            Assert.That(fit.PlayableWorldRect.xMax,
                Is.GreaterThanOrEqualTo(bounds.max.x + padding - 0.0001f));
            Assert.That(fit.PlayableWorldRect.yMin,
                Is.LessThanOrEqualTo(bounds.min.y - padding + 0.0001f));
            Assert.That(fit.PlayableWorldRect.yMax,
                Is.GreaterThanOrEqualTo(bounds.max.y + padding - 0.0001f));

            float fullHeight = fit.OrthographicSize * 2f;
            float fullWidth = fullHeight * aspect;
            float screenMinX = 0.5f +
                               (bounds.min.x - padding - fit.CameraCenter.x) / fullWidth;
            float screenMaxX = 0.5f +
                               (bounds.max.x + padding - fit.CameraCenter.x) / fullWidth;
            float screenMinY = 0.5f +
                               (bounds.min.y - padding - fit.CameraCenter.y) / fullHeight;
            float screenMaxY = 0.5f +
                               (bounds.max.y + padding - fit.CameraCenter.y) / fullHeight;
            Assert.That(screenMinX, Is.GreaterThanOrEqualTo(viewport.xMin - 0.0001f));
            Assert.That(screenMaxX, Is.LessThanOrEqualTo(viewport.xMax + 0.0001f));
            Assert.That(screenMinY, Is.GreaterThanOrEqualTo(viewport.yMin - 0.0001f));
            Assert.That(screenMaxY, Is.LessThanOrEqualTo(viewport.yMax + 0.0001f));
            Assert.That(screenMinX, Is.GreaterThanOrEqualTo(0f));
            Assert.That(screenMaxX, Is.LessThanOrEqualTo(1f));
            Assert.That(screenMinY, Is.GreaterThanOrEqualTo(0f));
            Assert.That(screenMaxY, Is.LessThanOrEqualTo(1f));
            Assert.That(0.5f + (bounds.center.x - fit.CameraCenter.x) / fullWidth,
                Is.EqualTo(viewport.center.x).Within(0.0001f));
            Assert.That(0.5f + (bounds.center.y - fit.CameraCenter.y) / fullHeight,
                Is.EqualTo(viewport.center.y).Within(0.0001f));
        }
    }
}
