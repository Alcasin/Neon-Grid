using NeonGrid.Data;
using UnityEngine;

namespace NeonGrid.Presentation
{
    public sealed class NeonGridBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;

        private void Awake()
        {
            if (level == null)
            {
                Debug.LogError("NeonGridBootstrap requires a LevelDefinition.", this);
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

#if UNITY_EDITOR
        public void SetLevel(LevelDefinition levelDefinition)
        {
            level = levelDefinition;
        }
#endif
    }
}
