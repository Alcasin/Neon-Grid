using NeonGrid.Campaign;
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
        private TutorialRuntime tutorial;
        private Camera gameplayCamera;
        private float fittedCameraAspect = -1f;

        public GameplaySession Session => session;
        public TutorialRuntime Tutorial => tutorial;

        public void Initialize(LevelDefinition levelDefinition)
        {
            Initialize(new GameplaySession(levelDefinition), null);
        }

        public void Initialize(GameplaySession gameplaySession, GameplayResultActions resultActions)
        {
            Initialize(gameplaySession, resultActions, null);
        }

        public void Initialize(GameplaySession gameplaySession, GameplayResultActions resultActions,
            LevelTutorialDefinition tutorialDefinition, int? levelOrdinal = null)
        {
            session = gameplaySession ?? throw new System.ArgumentNullException(nameof(gameplaySession));
            tutorial = new TutorialRuntime(tutorialDefinition);
            boardView = gameObject.AddComponent<BoardView>();
            boardView.Build(session.Board, OnTileTapped);
            boardView.SetCompleted(session.IsCompleted);

            hudView = gameObject.AddComponent<GameplayHudView>();
            hudView.Build(() => Undo(), Restart, () => RequestHint(), resultActions, levelOrdinal);
            hudView.Refresh(session);
            ApplyTutorialPresentation();
            RefreshBoardFraming(true);

            session.BoardChanged += OnBoardChanged;
            session.LevelCompleted += OnLevelCompleted;
            session.HintSearchFailed += OnHintSearchFailed;
        }

        private void OnDestroy()
        {
            if (session == null) return;
            session.BoardChanged -= OnBoardChanged;
            session.LevelCompleted -= OnLevelCompleted;
            session.HintSearchFailed -= OnHintSearchFailed;
            session.Dispose();
        }

        private void Update()
        {
            if (session == null) return;
            RefreshBoardFraming(false);
            if (session.UpdateHintRequest())
                ApplyHintHighlight();
            if (!hudView.IsLeaveConfirmationOpen)
                session.AdvanceTime(Time.deltaTime);
            hudView.Refresh(session);
        }

        private void OnTileTapped(GridPosition position)
        {
            PerformPlayerAction(position);
        }

        public bool PerformPlayerAction(GridPosition position)
        {
            if (session == null || !session.CanInteract ||
                !session.Board.TryGetPlayerAction(position, out PuzzleAction action))
                return false;

            bool applied = session.PerformAction(action);
            if (applied && tutorial.ObserveSuccessfulAction(action))
                ApplyTutorialPresentation();
            return applied;
        }

        private void OnBoardChanged()
        {
            boardView.Refresh(session.Board);
            boardView.SetCompleted(session.IsCompleted);
            ApplyHintHighlight();
            ApplyTutorialPresentation();
            hudView.Refresh(session);
        }

        private void OnLevelCompleted(SessionCompletionResult result)
        {
            boardView.SetCompleted(true);
            boardView.HighlightHint(null);
            boardView.HighlightTutorial(null);
            hudView.HideTutorial();
            hudView.Refresh(session);
        }

        private static void OnHintSearchFailed(System.Exception exception)
        {
            Debug.LogException(exception);
        }

        public bool Undo()
        {
            return session != null && session.Undo();
        }

        public void Restart()
        {
            if (session == null) return;
            tutorial.Restart();
            session.Restart();
            ApplyTutorialPresentation();
        }

        public HintResult RequestHint()
        {
            if (session == null)
                return HintResult.WithoutAction(HintStatus.UnsolvableOrInvalid);

            session.RequestHint();
            session.UpdateHintRequest();
            HintResult hint = session.LastHint;
            ApplyHintHighlight();
            hudView.Refresh(session);
            return hint;
        }

        private void ApplyHintHighlight()
        {
            PuzzleAction? action = session.LastHint.SuggestedAction;
            boardView.HighlightHint(action.HasValue ? action.Value.Position : (GridPosition?)null);
        }

        private void ApplyTutorialPresentation()
        {
            TutorialStepDefinition step = session != null && !session.IsCompleted
                ? tutorial.CurrentStep
                : null;
            boardView.HighlightTutorial(step?.TargetPosition);
            if (step == null)
                hudView.HideTutorial();
            else
                hudView.ShowTutorial(step.Message);
        }

        internal void RefreshBoardFraming(bool force)
        {
            Camera currentCamera = Camera.main;
            if (currentCamera == null || boardView == null) return;
            if (!force && currentCamera == gameplayCamera &&
                Mathf.Abs(currentCamera.aspect - fittedCameraAspect) < 0.0001f)
                return;

            gameplayCamera = currentCamera;
            fittedCameraAspect = currentCamera.aspect;
            BoardViewportFit fit = BoardViewportFitter.Calculate(boardView.GetWorldBounds(),
                fittedCameraAspect, GameplayLayoutMetrics.BoardViewport,
                GameplayLayoutMetrics.BoardPaddingWorld);
            currentCamera.orthographic = true;
            currentCamera.orthographicSize = fit.OrthographicSize;
            currentCamera.transform.position = new Vector3(fit.CameraCenter.x, fit.CameraCenter.y,
                currentCamera.transform.position.z);
        }
    }
}
