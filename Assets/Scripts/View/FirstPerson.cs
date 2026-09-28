// First-person movement: CharacterController walking, mouse look, jumping, head bob and wading.
// Input arrives as an FPInput struct each frame so the -uitest harness can drive the same code
// path a player does.
using UnityEngine;

namespace WishExtractor.View
{
    public struct FPInput
    {
        public Vector2 Move;          // x = strafe, y = forward, each -1..1
        public Vector2 Look;          // degrees this frame (yaw, pitch up)
        public bool Jump, Sprint;
        public bool Primary;          // held
        public bool PrimaryDown;      // pressed this frame
        public bool Interact;         // pressed this frame
        public int Hotbar;            // 0 = no change, 1..3 = select slot
        public bool Rotate;           // R: rotate the build ghost
        public bool Demolish;         // X: toggle demolish mode
        public int Cycle;             // mouse wheel in build mode: next / previous buildable
        public bool Catalogue;        // Tab: open the build catalogue
    }

    public sealed class FirstPersonController : MonoBehaviour
    {
        public const float EyeHeight = 1.62f, Height = 1.8f, Radius = 0.32f;
        public CharacterController Cc { get; private set; }
        public Camera Cam { get; private set; }
        public Transform Head { get; private set; }
        public float Yaw, Pitch;
        public float BaseSpeed = 4.6f, SprintMult = 1.55f;
        public float SpeedMult = 1f;          // tech × carry container × wading
        public bool CanJump = true;
        public bool HeadBob = true;
        public float Fov = 75f;
        public bool Grounded => Cc != null && Cc.isGrounded;
        public float Moved { get; private set; }      // metres walked this frame
        public float Speed01 { get; private set; }
        Vector3 velocity;
        float bobPhase, bobAmount, landKick;
        bool wasGrounded = true;
        public System.Action<float> OnLand;         // fall speed
        public System.Action OnStep;                // a footfall (for footstep sounds)

        public Vector3 Feet => transform.position;
        public Vector3 EyePos => Head.position;
        public Ray AimRay => new Ray(Cam.transform.position, Cam.transform.forward);

        public void Init(Camera cam)
        {
            Cc = gameObject.AddComponent<CharacterController>();
            Cc.height = Height;
            Cc.radius = Radius;
            Cc.center = new Vector3(0, Height / 2, 0);
            Cc.stepOffset = 0.5f;
            Cc.slopeLimit = 50f;
            Cc.skinWidth = 0.03f;
            Cc.minMoveDistance = 0;
            Head = new GameObject("Head").transform;
            Head.SetParent(transform, false);
            Head.localPosition = new Vector3(0, EyeHeight, 0);
            Cam = cam;
            cam.transform.SetParent(Head, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Teleport (spawn, load, tests). Yaw/pitch in degrees.</summary>
        public void Place(Vector3 feet, float yaw, float pitch)
        {
            Cc.enabled = false;
            transform.position = feet;
            Cc.enabled = true;
            Yaw = yaw;
            Pitch = pitch;
            velocity = Vector3.zero;
            ApplyLook();
        }

        void ApplyLook()
        {
            transform.rotation = Quaternion.Euler(0, Yaw, 0);
            Head.localRotation = Quaternion.Euler(-Pitch, 0, 0);
        }

        /// <summary>Point the view at a world position (tests and the tour).</summary>
        public void LookAt(Vector3 world)
        {
            Vector3 d = world - Head.position;
            Yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            Pitch = Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            ApplyLook();
        }

        public void Tick(FPInput input, float dt)
        {
            if (Cc == null || dt <= 0) return;
            Yaw += input.Look.x;
            Pitch = Mathf.Clamp(Pitch + input.Look.y, -86f, 86f);
            ApplyLook();

            Vector2 mv = Vector2.ClampMagnitude(input.Move, 1f);
            Vector3 wish = transform.right * mv.x + transform.forward * mv.y;
            float speed = BaseSpeed * SpeedMult * (input.Sprint && mv.y > 0.1f ? SprintMult : 1f);
            Vector3 horiz = wish * speed;
            // a little acceleration so starts and stops don't feel robotic
            Vector3 curH = new Vector3(velocity.x, 0, velocity.z);
            curH = Vector3.MoveTowards(curH, horiz, (Grounded ? 40f : 12f) * dt);
            velocity.x = curH.x;
            velocity.z = curH.z;

            if (Grounded)
            {
                if (velocity.y < 0) velocity.y = -2f;
                if (input.Jump && CanJump) velocity.y = 5.4f;
            }
            velocity.y -= 19f * dt;
            Vector3 before = transform.position;
            Cc.Move(velocity * dt);
            Vector3 after = transform.position;
            Moved = new Vector2(after.x - before.x, after.z - before.z).magnitude;
            Speed01 = Mathf.Clamp01(Moved / Mathf.Max(1e-4f, dt) / (BaseSpeed * 1.2f));
            if ((Cc.collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0) velocity.y = 0;

            bool g = Grounded;
            if (g && !wasGrounded)
            {
                OnLand?.Invoke(-velocity.y);
                landKick = Mathf.Clamp01(-velocity.y / 12f) * 0.08f;
            }
            wasGrounded = g;

            // head bob
            float targetBob = g && HeadBob ? Speed01 : 0;
            bobAmount = Mathf.MoveTowards(bobAmount, targetBob, dt * 4);
            float before2 = bobPhase;
            bobPhase += dt * Mathf.Lerp(6f, 10f, Speed01) * (Speed01 > 0.05f ? 1 : 0.3f);
            if (g && Speed01 > 0.15f && Mathf.Floor(bobPhase / Mathf.PI) != Mathf.Floor(before2 / Mathf.PI)) OnStep?.Invoke();
            landKick = Mathf.MoveTowards(landKick, 0, dt * 0.4f);
            float by = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.045f * bobAmount - landKick;
            float bx = Mathf.Sin(bobPhase) * 0.025f * bobAmount;
            Head.localPosition = new Vector3(bx, EyeHeight + by - 0.02f * bobAmount, 0);
            Cam.fieldOfView = Mathf.Lerp(Cam.fieldOfView, Fov + (input.Sprint && Speed01 > 0.5f ? 5f : 0f), 1 - Mathf.Exp(-dt * 8));
        }

        /// <summary>Horizontal bob phase and amount for the view-model hands.</summary>
        public float BobPhase => bobPhase;
        public float BobAmount => bobAmount;
    }
}
