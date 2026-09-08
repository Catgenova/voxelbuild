using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelBuild.Player
{
    /// <summary>
    /// Orthographic 2.5D camera: fixed pitch, yaw snaps in 90° steps, pans on the ground plane and zooms by ortho size.
    /// Runs on unscaled time so it stays responsive while the game is paused.
    /// </summary>
    public sealed class IsometricCameraController : MonoBehaviour
    {
        public float Pitch = 35f;
        public float MinOrthoSize = 0.8f;   // half the visible height in metres: a handful of blocks fill the screen
        public float MaxOrthoSize = 40f;
        public float PanSpeed = 1.2f;       // fraction of the visible height per second
        public float ZoomStep = 0.12f;
        public float Distance = 150f;
        public float Smoothing = 12f;

        private Camera cam;
        private Bounds bounds;
        private Bounds panBounds;
        private Vector3 focus;
        private float yaw = 45f;
        private float targetYaw = 45f;
        private float orthoSize = 14f;
        private float targetOrtho = 14f;

        public Vector3 Focus => focus;
        public float Yaw => yaw;

        public void Init(Camera camera, Bounds worldBounds, Vector3 initialFocus)
        {
            cam = camera;
            bounds = worldBounds;
            panBounds = ComputePanBounds(worldBounds, Pitch);
            focus = initialFocus;
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = Distance * 3f;
            cam.orthographicSize = orthoSize;
            Apply();
        }

        public void FocusOn(Vector3 point)
        {
            focus = point;
        }

        private void Update()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                var move = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
                if (move.sqrMagnitude > 0f)
                {
                    move.Normalize();
                    float speed = PanSpeed * orthoSize * 2f * (kb.leftShiftKey.isPressed ? 2.5f : 1f);
                    focus += GroundRight() * (move.x * speed * dt) + GroundForward() * (move.y * speed * dt);
                }
                if (kb.qKey.wasPressedThisFrame) targetYaw -= 90f;
                if (kb.eKey.wasPressedThisFrame) targetYaw += 90f;
            }

            if (mouse != null)
            {
                if (mouse.middleButton.isPressed || (mouse.rightButton.isPressed && kb != null && kb.leftAltKey.isPressed))
                {
                    var delta = mouse.delta.ReadValue();
                    float unitsPerPixel = orthoSize * 2f / Mathf.Max(1, Screen.height);
                    focus -= GroundRight() * (delta.x * unitsPerPixel) + GroundForward() * (delta.y * unitsPerPixel / Mathf.Max(0.2f, Mathf.Sin(Pitch * Mathf.Deg2Rad)));
                }
                float scroll = mouse.scroll.ReadValue().y;
                bool ctrl = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
                if (Mathf.Abs(scroll) > 0.01f && !ctrl && !PlayerTools.PointerOverUi())
                {
                    targetOrtho *= scroll > 0f ? 1f - ZoomStep : 1f + ZoomStep;
                    targetOrtho = Mathf.Clamp(targetOrtho, MinOrthoSize, MaxOrthoSize);
                }
            }

            focus.x = Mathf.Clamp(focus.x, panBounds.min.x, panBounds.max.x);
            focus.z = Mathf.Clamp(focus.z, panBounds.min.z, panBounds.max.z);
            focus.y = Mathf.Clamp(focus.y, bounds.min.y, bounds.max.y);

            float k = 1f - Mathf.Exp(-Smoothing * dt);
            yaw = Mathf.LerpAngle(yaw, targetYaw, k);
            orthoSize = Mathf.Lerp(orthoSize, targetOrtho, k);
            Apply();
        }

        /// <summary>
        /// In an isometric view a block far below the focus plane lines up with a surface point displaced towards the
        /// camera by depth / tan(pitch). Widen the pan limits by that much (for the full world height) so every cell
        /// at every level can be brought to the centre of the screen, whatever the yaw.
        /// </summary>
        public static Bounds ComputePanBounds(Bounds world, float pitchDegrees)
        {
            float tan = Mathf.Tan(Mathf.Clamp(pitchDegrees, 5f, 85f) * Mathf.Deg2Rad);
            float margin = world.size.y / tan + 2f;
            var b = world;
            b.Expand(new Vector3(margin * 2f, 0f, margin * 2f));
            return b;
        }

        private Vector3 GroundForward() => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        private Vector3 GroundRight() => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        private void Apply()
        {
            var rot = Quaternion.Euler(Pitch, yaw, 0f);
            transform.rotation = rot;
            transform.position = focus - rot * Vector3.forward * Distance;
            cam.orthographicSize = orthoSize;
        }
    }
}
