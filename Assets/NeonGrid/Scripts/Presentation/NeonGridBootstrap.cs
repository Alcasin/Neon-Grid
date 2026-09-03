using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public static class NeonGridBootstrap
    {
        private const string PrototypeLevelResourcePath = "Levels/TestLevel4x4";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartPrototype()
        {
            LevelDefinition level = Resources.Load<LevelDefinition>(PrototypeLevelResourcePath);
            if (level == null)
            {
                Debug.LogError($"No LevelDefinition found at Resources/{PrototypeLevelResourcePath}.");
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(level.Width, level.Height) * 0.72f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(0.008f, 0.012f, 0.03f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            var gameObject = new GameObject("Neon Grid Board");
            gameObject.AddComponent<BoardController>().Initialize(level);
        }
    }
}
