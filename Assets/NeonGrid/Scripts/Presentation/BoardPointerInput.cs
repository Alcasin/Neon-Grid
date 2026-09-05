using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NeonGrid.Presentation
{
    public sealed class BoardPointerInput : MonoBehaviour
    {
        private Camera targetCamera;
        private InputAction pressAction;
        private bool boardInputEnabled = true;
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

        public void Initialize(Camera camera)
        {
            targetCamera = camera;
            pressAction = new InputAction("Tap Circuit Tile", InputActionType.Button);
            pressAction.AddBinding("<Mouse>/leftButton");
            pressAction.AddBinding("<Touchscreen>/primaryTouch/press");
            pressAction.performed += OnPressPerformed;

            if (isActiveAndEnabled)
                pressAction.Enable();
        }

        private void OnEnable()
        {
            pressAction?.Enable();
        }

        private void OnDisable()
        {
            pressAction?.Disable();
        }

        private void OnDestroy()
        {
            if (pressAction == null) return;
            pressAction.performed -= OnPressPerformed;
            pressAction.Dispose();
        }

        public void SetBoardInputEnabled(bool enabled)
        {
            boardInputEnabled = enabled;
        }

        private void OnPressPerformed(InputAction.CallbackContext context)
        {
            if (!boardInputEnabled || targetCamera == null) return;

            Vector2 screenPosition;
            if (context.control.device is Mouse mouse)
                screenPosition = mouse.position.ReadValue();
            else if (context.control.device is Touchscreen touchscreen)
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
            else
                return;

            if (IsPointerOverUi(screenPosition)) return;

            Ray pointerRay = targetCamera.ScreenPointToRay(screenPosition);
            RaycastHit2D hit = Physics2D.GetRayIntersection(pointerRay);
            if (hit.collider != null && hit.collider.TryGetComponent(out CircuitTileInput tileInput))
                tileInput.HandleTap();
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            uiRaycastResults.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, uiRaycastResults);
            return uiRaycastResults.Count > 0;
        }
    }
}
