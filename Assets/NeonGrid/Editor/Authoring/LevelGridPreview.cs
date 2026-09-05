using NeonGrid.Simulation;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor.Authoring
{
    public static class LevelGridPreview
    {
        private const float CoordinateGutter = 22f;

        public static GridPosition? Draw(LevelAuthoringModel model, GridPosition? selected, float cellSize)
        {
            float width = CoordinateGutter + model.Width * cellSize;
            float height = CoordinateGutter + model.Height * cellSize;
            Rect area = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            GridPosition? clicked = null;

            var centered = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            var title = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(8, Mathf.RoundToInt(cellSize * 0.15f))
            };
            var marker = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.UpperRight,
                fontSize = 8
            };

            for (int x = 0; x < model.Width; x++)
            {
                Rect labelRect = new Rect(area.x + CoordinateGutter + x * cellSize, area.y, cellSize, CoordinateGutter);
                GUI.Label(labelRect, x.ToString(), centered);
            }

            Handles.BeginGUI();
            try
            {
                for (int displayRow = 0; displayRow < model.Height; displayRow++)
                {
                    int y = model.Height - 1 - displayRow;
                    Rect yLabelRect = new Rect(area.x, area.y + CoordinateGutter + displayRow * cellSize,
                        CoordinateGutter, cellSize);
                    GUI.Label(yLabelRect, y.ToString(), centered);

                    for (int x = 0; x < model.Width; x++)
                    {
                        var position = new GridPosition(x, y);
                        Rect cellRect = new Rect(area.x + CoordinateGutter + x * cellSize,
                            area.y + CoordinateGutter + displayRow * cellSize, cellSize, cellSize);
                        LevelCellData cell = model.GetCell(position);
                        DrawCell(cellRect, cell, selected.HasValue && selected.Value.Equals(position), title, marker);

                        Event current = Event.current;
                        if (current.type == EventType.MouseDown && current.button == 0 &&
                            cellRect.Contains(current.mousePosition))
                        {
                            clicked = position;
                            current.Use();
                        }
                    }
                }
            }
            finally
            {
                Handles.EndGUI();
            }

            return clicked;
        }

        private static void DrawCell(Rect rect, LevelCellData cell, bool selected, GUIStyle title, GUIStyle marker)
        {
            EditorGUI.DrawRect(rect, CellBackground(cell));
            Handles.color = selected ? new Color(0.2f, 0.95f, 1f) : new Color(0.26f, 0.3f, 0.36f);
            Handles.DrawAAPolyLine(selected ? 4f : 1f,
                new Vector3(rect.xMin, rect.yMin), new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax), new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin));

            DrawConnections(rect, cell);
            GUI.Label(rect, TileLabel(cell), title);

            if (!cell.IsRotatable && cell.TileType != TileType.Empty && cell.TileType != TileType.Switch)
            {
                Rect fixedRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, 14f);
                GUI.Label(fixedRect, "FIXED", marker);
            }
        }

        private static void DrawConnections(Rect rect, LevelCellData cell)
        {
            CardinalDirection connections = TileConnections.GetConnections(cell.TileType, cell.Rotation);
            Vector3 center = rect.center;
            Handles.color = new Color(0.1f, 0.9f, 1f);
            foreach (CardinalDirection direction in DirectionUtility.CardinalDirections)
            {
                if ((connections & direction) == 0) continue;
                Vector3 edge = center;
                switch (direction)
                {
                    case CardinalDirection.Up: edge.y = rect.yMin; break;
                    case CardinalDirection.Right: edge.x = rect.xMax; break;
                    case CardinalDirection.Down: edge.y = rect.yMax; break;
                    case CardinalDirection.Left: edge.x = rect.xMin; break;
                }
                Handles.DrawAAPolyLine(4f, center, edge);
            }

            Handles.color = new Color(0.95f, 0.98f, 1f);
            Handles.DrawSolidDisc(center, Vector3.forward, Mathf.Max(2f, rect.width * 0.055f));
        }

        private static Color CellBackground(LevelCellData cell)
        {
            switch (cell.TileType)
            {
                case TileType.PowerSource: return new Color(0.08f, 0.38f, 0.42f);
                case TileType.OutputLamp: return new Color(0.4f, 0.3f, 0.08f);
                case TileType.Switch:
                    return cell.StartingSwitchOn ? new Color(0.1f, 0.4f, 0.18f) : new Color(0.38f, 0.12f, 0.12f);
                case TileType.Empty: return new Color(0.1f, 0.11f, 0.14f);
                default: return new Color(0.14f, 0.17f, 0.22f);
            }
        }

        private static string TileLabel(LevelCellData cell)
        {
            switch (cell.TileType)
            {
                case TileType.Empty: return string.Empty;
                case TileType.StraightWire: return "STRAIGHT";
                case TileType.CornerWire: return "CORNER";
                case TileType.TJunction: return "T";
                case TileType.CrossJunction: return "CROSS";
                case TileType.PowerSource: return $"SOURCE {DirectionArrow(TilePowerFlow.GetOutputSides(cell.TileType, cell.Rotation))}";
                case TileType.OutputLamp: return "LAMP";
                case TileType.Diode: return $"DIODE {DirectionArrow(TilePowerFlow.GetOutputSides(cell.TileType, cell.Rotation))}";
                case TileType.Switch: return cell.StartingSwitchOn ? "SWITCH ON" : "SWITCH OFF";
                case TileType.AndGate: return $"AND {DirectionArrow(TilePowerFlow.GetOutputSides(cell.TileType, cell.Rotation))}";
                case TileType.OrGate: return $"OR {DirectionArrow(TilePowerFlow.GetOutputSides(cell.TileType, cell.Rotation))}";
                default: return cell.TileType.ToString();
            }
        }

        private static string DirectionArrow(CardinalDirection direction)
        {
            if ((direction & CardinalDirection.Up) != 0) return "↑";
            if ((direction & CardinalDirection.Right) != 0) return "→";
            if ((direction & CardinalDirection.Down) != 0) return "↓";
            if ((direction & CardinalDirection.Left) != 0) return "←";
            return string.Empty;
        }
    }
}
