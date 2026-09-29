using System;
using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    /// <summary>
    /// Owns the single production audio/haptics service pair for a campaign runtime.
    /// Gameplay state remains authoritative; this component only binds presentation events.
    /// </summary>
    public sealed class ProductionGameplayFeedbackController : MonoBehaviour
    {
        private BoardController activeBoard;

        public ProductionGameplayFeedbackDefinition Definition { get; private set; }
        public NeonGridAudioService AudioService { get; private set; }
        public NeonGridHapticsService HapticsService { get; private set; }
        public BoardController ActiveBoard => activeBoard;

        public void Initialize(ProductionGameplayFeedbackDefinition definition, Camera camera)
        {
            if (definition == null || !definition.IsConfigured)
                throw new ArgumentException(
                    "Production gameplay feedback must be fully configured.", nameof(definition));

            EnsureAudioListener(camera);
            if (Definition == definition && AudioService != null && HapticsService != null)
                return;

            ExitGameplay();
            Definition = definition;
            AudioService = GetComponent<NeonGridAudioService>() ??
                           gameObject.AddComponent<NeonGridAudioService>();
            HapticsService = GetComponent<NeonGridHapticsService>() ??
                             gameObject.AddComponent<NeonGridHapticsService>();
            AudioService.Initialize(definition.AudioDefinition);
            HapticsService.Initialize(definition.HapticsDefinition);
        }

        public void EnterGameplay(BoardController board)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (Definition == null || AudioService == null || HapticsService == null)
                throw new InvalidOperationException(
                    "Production gameplay feedback must be initialized before level entry.");
            if (board.BoardView?.JuiceCoordinator == null)
                throw new InvalidOperationException(
                    "Production gameplay must initialize circuit juice before feedback binding.");

            if (activeBoard != null && activeBoard != board)
                activeBoard.DetachFeedbackServices();
            activeBoard = board;
            HapticsService.ResetCompletionLifecycle();
            activeBoard.AttachFeedbackServices(AudioService, HapticsService);
            AudioService.RequestAmbience(NeonGridAmbienceMode.Gameplay);
        }

        public void ExitGameplay()
        {
            if (activeBoard != null)
                activeBoard.DetachFeedbackServices();
            activeBoard = null;
            AudioService?.Bind(null);
            AudioService?.StopAll();
            AudioService?.RequestAmbience(NeonGridAmbienceMode.None);
            HapticsService?.Bind(null);
        }

        private void OnDestroy()
        {
            if (activeBoard != null)
                activeBoard.DetachFeedbackServices();
            activeBoard = null;
            AudioService?.Bind(null);
            AudioService?.StopAll();
            AudioService?.StopAmbienceImmediately();
            HapticsService?.Bind(null);
        }

        internal static AudioListener EnsureAudioListener(Camera camera)
        {
            AudioListener existing = FindFirstObjectByType<AudioListener>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                existing.enabled = true;
                return existing;
            }
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            return camera.gameObject.AddComponent<AudioListener>();
        }
    }
}
