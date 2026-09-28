// Orbit camera around the fountain. Right-drag (or Q/E) to orbit, scroll to zoom, middle-drag
// or WASD to pan. It tilts down further as the crust sinks so the dig site stays in view.
using UnityEngine;

namespace WishExtractor.View
{
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public float Yaw, Pitch = 50f, Dist = 44f;
        float yawT, pitchOffsetT, distT = 44f;
        Vector3 pan, panT;
        float surfaceY = 0.5f, depth01;
        float shake;
        public bool InputEnabled = true;
        public float ScreenShiftX = 0f;   // fraction of view width to shift the fountain (UI panels)

        public void Init(Camera cam)
        {
            Cam = cam;
            yawT = Yaw;
            distT = Dist;
        }

        public void SetDepth(float surface, float d01) { surfaceY = surface; depth01 = d01; }
        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }
        public void ResetView() { yawT = 0; pitchOffsetT = 0; distT = 44f; panT = Vector3.zero; }

        public void HandleInput(float dt, bool pointerOverUI)
        {
            if (!InputEnabled) return;
            if (Input.GetMouseButton(1))
            {
                yawT += Input.GetAxis("Mouse X") * 3.2f;
                pitchOffsetT = Mathf.Clamp(pitchOffsetT - Input.GetAxis("Mouse Y") * 2.0f, -22f, 28f);
            }
            if (Input.GetMouseButton(2))
            {
                var right = Quaternion.Euler(0, yawT, 0) * Vector3.right;
                var fwd = Quaternion.Euler(0, yawT, 0) * Vector3.forward;
                panT -= (right * Input.GetAxis("Mouse X") + fwd * Input.GetAxis("Mouse Y")) * distT * 0.03f;
            }
            if (!pointerOverUI)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f) distT = Mathf.Clamp(distT * Mathf.Pow(0.9f, scroll), 14f, 72f);
            }
            float k = dt * 60f;
            if (Input.GetKey(KeyCode.Q)) yawT += k * 1.2f;
            if (Input.GetKey(KeyCode.E)) yawT -= k * 1.2f;
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move += Vector3.forward;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move += Vector3.back;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move += Vector3.left;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += Vector3.right;
            if (move != Vector3.zero) panT += Quaternion.Euler(0, yawT, 0) * move * dt * 16f;
            panT = Vector3.ClampMagnitude(panT, 24f);
            if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Home)) ResetView();
        }

        void LateUpdate()
        {
            if (Cam == null) return;
            float dt = Time.unscaledDeltaTime;
            float s = 1 - Mathf.Exp(-dt * 8f);
            Yaw = Mathf.LerpAngle(Yaw, yawT, s);
            Dist = Mathf.Lerp(Dist, distT, s);
            pan = Vector3.Lerp(pan, panT, s);
            float basePitch = Mathf.Lerp(48f, 64f, Mathf.SmoothStep(0, 1, depth01));
            Pitch = Mathf.Lerp(Pitch, Mathf.Clamp(basePitch + pitchOffsetT, 18f, 84f), s);
            Vector3 target = new Vector3(0, Mathf.Lerp(0.5f, surfaceY, 0.5f) - 0.5f, 1.2f) + pan;
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            // shift the orbit target sideways so the fountain sits in the space the UI leaves free
            target += rot * Vector3.right * (ScreenShiftX * Dist * 0.8f);
            Vector3 pos = target + rot * new Vector3(0, 0, -Dist);
            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - dt * 2.5f);
                pos += Random.insideUnitSphere * shake * 0.35f;
            }
            Cam.transform.SetPositionAndRotation(pos, rot);
        }
    }
}
