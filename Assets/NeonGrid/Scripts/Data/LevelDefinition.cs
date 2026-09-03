using System.Collections.Generic;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Data
{
    [CreateAssetMenu(fileName = "LevelDefinition", menuName = "Neon Grid/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int width = 4;
        [SerializeField, Min(1)] private int height = 4;
        [SerializeField] private List<TileDefinition> tiles = new List<TileDefinition>();

        public int Width => width;
        public int Height => height;
        public IReadOnlyList<TileDefinition> Tiles => tiles;

        public BoardState CreateBoardState() => new BoardState(width, height, tiles);

#if UNITY_EDITOR
        public void SetData(int newWidth, int newHeight, IEnumerable<TileDefinition> newTiles)
        {
            width = newWidth;
            height = newHeight;
            tiles = new List<TileDefinition>(newTiles);
        }
#endif
    }
}
