using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    internal sealed class TechnicalNeonBoardRenderer
    {
        public TechnicalNeonBoardRenderer(Transform root, Sprite squareSprite,
            CircuitVisualThemeDefinition theme, int width, int height)
        {
            var surface = new GameObject("Production Board Surface").transform;
            surface.SetParent(root, false);
            surface.localPosition = new Vector3((width - 1) * 0.5f,
                (height - 1) * 0.5f, 0f);

            Color background = theme.Background;
            Color board = theme.Board;
            Color edge = theme.SecondaryBlue;
            edge.a = 0.14f;
            Color socket = Color.Lerp(board, background, 0.55f);

            CreatePart(surface, "Gameplay Background Plate", squareSprite,
                new Vector3(0f, 0f, 0.35f), new Vector3(width + 3.8f, height + 5.2f, 1f),
                background, -30);
            CreatePart(surface, "Board Outer Shadow", squareSprite,
                new Vector3(0.06f, -0.08f, 0.3f), new Vector3(width + 0.56f, height + 0.56f, 1f),
                WithAlpha(Color.black, 0.55f), -24);
            CreatePart(surface, "Board Edge Keyline", squareSprite,
                new Vector3(0f, 0f, 0.25f), new Vector3(width + 0.42f, height + 0.42f, 1f),
                edge, -23);
            CreatePart(surface, "Board Technical Plate", squareSprite,
                new Vector3(0f, 0f, 0.2f), new Vector3(width + 0.30f, height + 0.30f, 1f),
                board, -22);
            CreatePart(surface, "Board Recess", squareSprite,
                new Vector3(0f, 0f, 0.15f), new Vector3(width + 0.10f, height + 0.10f, 1f),
                socket, -21);

            float x = width * 0.5f + 0.08f;
            float y = height * 0.5f + 0.08f;
            CreateFastener(surface, squareSprite, "Top Left", new Vector3(-x, y, 0f), theme);
            CreateFastener(surface, squareSprite, "Top Right", new Vector3(x, y, 0f), theme);
            CreateFastener(surface, squareSprite, "Bottom Left", new Vector3(-x, -y, 0f), theme);
            CreateFastener(surface, squareSprite, "Bottom Right", new Vector3(x, -y, 0f), theme);
        }

        private static void CreateFastener(Transform parent, Sprite sprite, string name,
            Vector3 position, CircuitVisualThemeDefinition theme)
        {
            Color collar = theme.InactiveConductor;
            Color bolt = theme.SecondaryBlue;
            bolt.a = 0.55f;
            CreatePart(parent, $"Board Fastener Collar {name}", sprite, position,
                new Vector3(0.14f, 0.14f, 1f), collar, -20);
            CreatePart(parent, $"Board Fastener Bolt {name}", sprite,
                position + new Vector3(0f, 0f, -0.01f), new Vector3(0.055f, 0.055f, 1f),
                bolt, -19, 45f);
        }

        private static SpriteRenderer CreatePart(Transform parent, string name, Sprite sprite,
            Vector3 position, Vector3 scale, Color color, int order, float rotation = 0f)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            child.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
