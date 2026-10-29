using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    /// <summary>
    /// Orbit camera with wall collision and zoom. Alt frees the cursor, Escape toggles it.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 targetOffset = new(0f, 1.6f, 0f);
        public float distance = 5f;
        public float minDistance = 1.5f;
        public float maxDistance = 12f;
        public float mouseSensitivity = 0.12f;
        public float stickSensitivity = 180f;
        public float minPitch = -40f;
        public float maxPitch = 75f;
        public float followSmooth = 15f;
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;

        float yaw;
        float pitch = 12f;
        float currentDistance;
        Vector3 smoothedTarget;
        bool cursorFree;

        static ThirdPersonCamera instance;
        float shakeAmount, shakeTime, shakeDuration;

        void Awake() => instance = this;

        /// <summary>Camera shake (intensity in metres, duration in seconds, unscaled time).</summary>
        public static void Shake(float intensity, float duration)
        {
            if (!instance) return;
            instance.shakeAmount = Mathf.Max(instance.shakeAmount * (1f - instance.shakeTime / Mathf.Max(0.01f, instance.shakeDuration)), intensity);
            instance.shakeDuration = duration;
            instance.shakeTime = 0f;
        }

        /// <summary>Shake attenuated by distance from a world-space source.</summary>
        public static void ShakeAt(Vector3 source, float intensity, float duration)
        {
            if (!instance) return;
            float d = Vector3.Distance(source, instance.transform.position);
            Shake(intensity * Mathf.Clamp01(1f - d / 25f), duration);
        }

        void Start()
        {
            currentDistance = distance;
            if (target)
            {
                yaw = target.eulerAngles.y;
                smoothedTarget = target.position + targetOffset;
            }
        }

        void LateUpdate()
        {
            if (!target) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var gp = Gamepad.current;

            bool altHeld = kb != null && kb.leftAltKey.isPressed;
            if (cursorFree && !altHeld && mouse != null && mouse.leftButton.wasPressedThisFrame) cursorFree = false;
            bool free = cursorFree || altHeld || GameState.UIOpen;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;

            Vector2 look = Vector2.zero;
            if (!free && mouse != null) look += mouse.delta.ReadValue() * mouseSensitivity;
            if (gp != null && !GameState.UIOpen) look += gp.rightStick.ReadValue() * stickSensitivity * Time.unscaledDeltaTime;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

            if (!free && mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * 0.8f, minDistance, maxDistance);
            }

            float dt = Time.deltaTime;
            smoothedTarget = Vector3.Lerp(smoothedTarget, target.position + targetOffset, 1f - Mathf.Exp(-followSmooth * dt));

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;
            float wanted = distance;
            if (Physics.SphereCast(smoothedTarget, collisionRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                wanted = Mathf.Max(0.3f, hit.distance);
            currentDistance = wanted < currentDistance ? wanted : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-5f * dt));

            Vector3 shake = Vector3.zero;
            if (shakeTime < shakeDuration)
            {
                shakeTime += Time.unscaledDeltaTime;
                float k = 1f - shakeTime / shakeDuration;
                float t = Time.unscaledTime * 40f;
                shake = (rotation * new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f)) * (shakeAmount * k * 2f);
            }
            transform.SetPositionAndRotation(smoothedTarget + back * currentDistance + shake, rotation);
        }
    }
}
