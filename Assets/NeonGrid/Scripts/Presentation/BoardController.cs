using NeonGrid.Data;
using NeonGrid.Session;
using NeonGrid.Simulation;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class BoardController : MonoBehaviour
    {
        private GameplaySession session;
        private BoardView boardView;
        private GameplayHudView hudView;

        public GameplaySession Session => session;

        public void Initialize(LevelDefinition levelDefinition)
        {
            Initialize(new GameplaySession(levelDefinition), null);
        }

        public void Initialize(GameplaySession gameplaySession, GameplayResultActions resultActions)
        {
            session = gameplaySession ?? throw new System.ArgumentNullException(nameof(gameplaySession));
            boardView = gameObject.AddComponent<BoardView>();
            boardView.Build(session.Board, OnTileTapped);
            boardView.SetCompleted(session.IsCompleted);

            hudView = gameObject.AddComponent<GameplayHudView>();
            hudView.Build(() => Undo(), Restart, () => RequestHint(), resultActions);
            hudView.Refresh(session);

            session.BoardChanged += OnBoardChanged;
            session.LevelCompleted += OnLevelCompleted;
        }

        private void OnDestroy()
        {
            if (session == null) return;
            session.BoardChanged -= OnBoardChanged;
            session.LevelCompleted -= OnLevelCompleted;
        }

        private void Update()
        {
            if (session == null) return;
            if (!hudView.IsLeaveConfirmationOpen)
                session.AdvanceTime(Time.deltaTime);
            hudView.Refresh(session);
        }

        private void OnTileTapped(GridPosition position)
        {
            session.InteractWithTile(position);
        }

        private void OnBoardChanged()
        {
            boardView.Refresh(session.Board);
            boardView.SetCompleted(session.IsCompleted);
            ApplyHintHighlight();
            hudView.Refresh(session);
        }

        private void OnLevelCompleted(SessionCompletionResult result)
        {
            boardView.SetCompleted(true);
            boardView.HighlightHint(null);
            hudView.Refresh(session);
        }

        public bool Undo()
        {
            return session != null && session.Undo();
        }

        public void Restart()
        {
            if (session == null) return;
            session.Restart();
        }

        public HintResult RequestHint()
        {
            if (session == null)
                return HintResult.WithoutAction(HintStatus.UnsolvableOrInvalid);

            HintResult hint = session.RequestHint();
            ApplyHintHighlight();
            hudView.Refresh(session);
            return hint;
        }

        private void ApplyHintHighlight()
        {
            PuzzleAction? action = session.LastHint.SuggestedAction;
            boardView.HighlightHint(action.HasValue ? action.Value.Position : (GridPosition?)null);
        }
    }
}
