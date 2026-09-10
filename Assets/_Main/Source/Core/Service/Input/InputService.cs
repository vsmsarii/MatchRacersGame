using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CasualKit.Core
{
    public sealed class InputService : Service, IInputService
    {
        private readonly IAssetProvider m_Assets;
        private const string PressActionName = AddressableKeys.TapAction;

        private InputActionAsset m_Asset;
        private InputAction m_Press;
        private bool m_HasPending;
        private Vector2 m_PendingPosition;

        public InputService(IAssetProvider assets)
        {
            m_Assets = assets;
        }

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (m_Press != null)
                return;

            m_Asset = await m_Assets.LoadAsset<InputActionAsset>(AddressableKeys.InputActions, cancellationToken);
            if (m_Asset == null)
            {
                EditorLog.Error("Input action asset failed to load: " + AddressableKeys.InputActions);
                return;
            }

            m_Press = m_Asset.FindAction(PressActionName, false);
            if (m_Press == null)
            {
                EditorLog.Error("Input action not found: " + PressActionName);
                m_Asset = null;
                return;
            }

            m_Press.started += OnPressed;
            m_Press.Enable();
        }

        public bool TryConsumeTap(out Vector2 screenPosition)
        {
            if (!m_HasPending)
            {
                screenPosition = default;
                return false;
            }

            m_HasPending = false;
            screenPosition = m_PendingPosition;
            return true;
        }

        protected override void OnDispose()
        {
            if (m_Press != null)
            {
                m_Press.started -= OnPressed;
                m_Press.Disable();
                m_Press = null;
            }

            m_Asset = null;
            m_HasPending = false;
            m_Assets.ReleaseAsset(AddressableKeys.InputActions);
        }

        private void OnPressed(InputAction.CallbackContext context)
        {
            m_HasPending = true;
            m_PendingPosition = ReadPointerPosition();
        }

        private static Vector2 ReadPointerPosition()
        {
            if (Pointer.current != null)
                return Pointer.current.position.ReadValue();

            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();

            if (Touchscreen.current != null)
                return Touchscreen.current.primaryTouch.position.ReadValue();

            return Vector2.zero;
        }
    }
}
