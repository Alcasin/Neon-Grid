using System;

namespace NeonGrid.Simulation
{
    [Serializable]
    public sealed class TileDefinition
    {
        public GridPosition position;
        public TileType tileType;
        public int startingRotation;
        public bool isRotatable;
        public bool startingSwitchOn;

        public TileDefinition(GridPosition position, TileType tileType, int startingRotation, bool isRotatable,
            bool startingSwitchOn = false)
        {
            this.position = position;
            this.tileType = tileType;
            this.startingRotation = startingRotation;
            this.isRotatable = isRotatable;
            this.startingSwitchOn = startingSwitchOn;
        }
    }
}
