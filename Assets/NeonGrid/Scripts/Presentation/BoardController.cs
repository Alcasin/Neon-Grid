using NeonGrid.Data;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class BoardController : MonoBehaviour
    {
        private CircuitSimulation simulation;
        private BoardView boardView;

        public void Initialize(LevelDefinition levelDefinition)
        {
            simulation = new CircuitSimulation(levelDefinition.CreateBoardState());
            boardView = gameObject.AddComponent<BoardView>();
            boardView.Build(simulation.Board, OnTileTapped);
            boardView.SetCompleted(simulation.IsLevelCompleted);

            simulation.BoardChanged += OnBoardChanged;
            simulation.LevelCompleted += OnLevelCompleted;
        }

        private void OnDestroy()
        {
            if (simulation == null) return;
            simulation.BoardChanged -= OnBoardChanged;
            simulation.LevelCompleted -= OnLevelCompleted;
        }

        private void OnTileTapped(GridPosition position)
        {
            simulation.RotateTileClockwise(position);
        }

        private void OnBoardChanged()
        {
            boardView.Refresh(simulation.Board);
            boardView.SetCompleted(simulation.IsLevelCompleted);
        }

        private void OnLevelCompleted()
        {
            boardView.SetCompleted(true);
        }
    }
}
