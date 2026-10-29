using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public enum MoveState { Grounded, Airborne, Gliding, Climbing, Swimming }

    /// <summary>
    /// Third-person traversal inspired by Genshin / Wuthering Waves:
    /// run, sprint, jump, glide, climb any steep surface and swim, all driven by a stamina pool.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        public Transform cameraTransform;
        public Transform visual;
        public GameObject glider;

        [Header("Ground")]
        public float walkSpeed = 2.2f;
        public float runSpeed = 5.2f;
        public float sprintSpeed = 8.5f;
        public float acceleration = 10f;
        public float turnSmoothTime = 0.08f;
        public float gravity = -25f;
        public float jumpHeight = 1.3f;
        public float airControl = 3f;
        public float maxFallSpeed = 50f;

        [Header("Glide")]
        public float glideFallSpeed = 2f;
        public float glideSpeed = 7.5f;
        public float minGlideHeight = 1.5f;

        [Header("Climb")]
        public float climbSpeed = 2f;
        public float climbJumpDistance = 2.2f;
        public float climbBoostDuration = 0.25f;
        [Tooltip("Surfaces whose normal.y is below this are climbable (0.64 ≈ 50°).")]
        public float maxClimbNormalY = 0.64f;

        [Header("Swim")]
        public float waterLevel = 30f;
        public float swimSpeed = 2.8f;
        public float fastSwimSpeed = 5.5f;
        public float swimDepth = 1.25f;
        public float swimEnterDepth = 1.35f;
        public float swimExitDepth = 0.9f;

        [Header("Stamina")]
        public float maxStamina = 160f;
        public float sprintStartCost = 15f;
        public float sprintCostPerSec = 14f;
        public float glideCostPerSec = 9f;
        public float climbCostPerSec = 10f;
        public float climbJumpCost = 25f;
        public float swimCostPerSec = 4f;
        public float fastSwimCostPerSec = 16f;
        public float staminaRegen = 35f;
        public float regenDelay = 1f;

        public MoveState State { get; private set; }
        public float Stamina { get; private set; }
        public float MaxStamina => maxStamina;
        public bool IsSprinting { get; private set; }
        public Vector3 PlanarVelocity => planarVelocity;
        public float VerticalSpeed => verticalSpeed;
        public Vector2 MoveInput => moveInput;

        [Tooltip("Simple bobbing of the visual root; disable when a ProceduralAnimator drives the body.")]
        public bool visualBob = true;
        float WaterDepth => waterLevel - transform.position.y;

        CharacterController cc;
        int envMask;

        Vector2 moveInput;
        Vector3 moveDir;
        bool jumpPressed, sprintHeld, dropPressed, walkToggle;

        Vector3 planarVelocity;
        float verticalSpeed;
        float turnVelocity;
        float airTime;

        bool usedStamina, exhausted;
        float regenTimer;

        Vector3 wallNormal;
        float climbCooldown;
        float climbBoostTimer;
        Vector3 climbBoostDir;

        Vector3 lastSafePosition;
        float safeTimer;

        Vector3 visualBasePos;
        float bobPhase;

        float movementLockTimer;
        float dashTimer;
        Vector3 dashVelocity;

        public static PlayerController Instance { get; private set; }
        public bool IsDashing => dashTimer > 0f;

        /// <summary>Blocks player-driven movement (used during attacks and casts).</summary>
        public void LockMovement(float duration) => movementLockTimer = Mathf.Max(movementLockTimer, duration);

        /// <summary>Forces a planar velocity for a short time (dodges, attack lunges, knockback).</summary>
        public void Dash(Vector3 velocity, float duration)
        {
            velocity.y = 0f;
            dashVelocity = velocity;
            dashTimer = duration;
            if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }

        public bool SpendStamina(float amount) => SpendBurst(amount);

        public void FaceTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction.normalized);
        }

        /// <summary>World-space desired move direction from the current input (camera relative).</summary>
        public Vector3 DesiredDirection => moveDir;

        public void RestoreStamina() => Stamina = maxStamina;

        public void SetMaxStamina(float value)
        {
            maxStamina = value;
            Stamina = Mathf.Min(Stamina, maxStamina);
        }

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            Stamina = maxStamina;
            int playerLayer = LayerMask.NameToLayer("Player");
            envMask = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;
            lastSafePosition = transform.position;
            if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;
            if (glider) glider.SetActive(false);
            if (visual) visualBasePos = visual.localPosition;
            State = MoveState.Airborne;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            usedStamina = false;
            ReadInput();
            if (GameState.InputBlocked)
            {
                moveInput = Vector2.zero;
                jumpPressed = sprintHeld = dropPressed = false;
            }

            Vector3 camForward = cameraTransform
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized
                : transform.forward;
            Vector3 camRight = Vector3.Cross(Vector3.up, camForward);
            moveDir = camForward * moveInput.y + camRight * moveInput.x;
            if (climbCooldown > 0f) climbCooldown -= dt;

            if (movementLockTimer > 0f)
            {
                movementLockTimer -= dt;
                moveInput = Vector2.zero;
                moveDir = Vector3.zero;
                jumpPressed = false;
            }

            switch (State)
            {
                case MoveState.Grounded: UpdateGrounded(dt); break;
                case MoveState.Airborne: UpdateAirborne(dt); break;
                case MoveState.Gliding: UpdateGliding(dt); break;
                case MoveState.Climbing: UpdateClimbing(dt); break;
                case MoveState.Swimming: UpdateSwimming(dt); break;
            }

            UpdateStaminaRegen(dt);
            UpdateSafePosition(dt);
            UpdateVisual(dt);
            if (transform.position.y < -50f) Respawn();
        }

        void ReadInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var gp = Gamepad.current;

            // Keys are physical positions: WASD on QWERTY is ZQSD on AZERTY.
            Vector2 m = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) m.y += 1f;
                if (kb.sKey.isPressed) m.y -= 1f;
                if (kb.dKey.isPressed) m.x += 1f;
                if (kb.aKey.isPressed) m.x -= 1f;
                if (kb.leftCtrlKey.wasPressedThisFrame) walkToggle = !walkToggle;
            }
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                if (stick.sqrMagnitude > m.sqrMagnitude) m = stick;
            }
            moveInput = Vector2.ClampMagnitude(m, 1f);

            jumpPressed = (kb != null && kb.spaceKey.wasPressedThisFrame) || (gp != null && gp.buttonSouth.wasPressedThisFrame);
            // Holding the dodge button keeps sprinting after the dash (Genshin-style); the dash itself is in PlayerCombat.
            sprintHeld = (kb != null && kb.leftShiftKey.isPressed) || (mouse != null && mouse.rightButton.isPressed) || (gp != null && gp.rightTrigger.isPressed);
            dropPressed = (kb != null && kb.xKey.wasPressedThisFrame) || (gp != null && gp.buttonEast.wasPressedThisFrame);
        }

        // ---------------------------------------------------------------- States

        void UpdateGrounded(float dt)
        {
            bool moving = moveDir.sqrMagnitude > 0.01f;
            if (exhausted && Stamina > maxStamina * 0.25f) exhausted = false;

            if (sprintHeld && moving && !exhausted)
            {
                if (!IsSprinting) IsSprinting = SpendBurst(sprintStartCost);
                else if (!Drain(sprintCostPerSec, dt)) { IsSprinting = false; exhausted = true; }
            }
            else IsSprinting = false;

            float speed = 0f;
            if (moving)
                speed = IsSprinting ? sprintSpeed : (walkToggle || moveInput.magnitude < 0.5f) ? walkSpeed : runSpeed;
            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                planarVelocity = dashVelocity;
            }
            else
            {
                planarVelocity = Vector3.Lerp(planarVelocity, moveDir.normalized * speed, 1f - Mathf.Exp(-acceleration * dt));
                if (moving) FaceDirection(moveDir, dt);
            }

            verticalSpeed = -4f;
            if (jumpPressed)
            {
                verticalSpeed = Mathf.Sqrt(-2f * gravity * jumpHeight);
                SetState(MoveState.Airborne);
            }

            if (moving && climbCooldown <= 0f && Stamina > 0f && TryFindWall(moveDir, out var hit))
            {
                StartClimb(hit);
                return;
            }

            cc.Move((planarVelocity + Vector3.up * verticalSpeed) * dt);

            if (State == MoveState.Grounded)
            {
                if (cc.isGrounded) airTime = 0f;
                else if ((airTime += dt) > 0.2f) SetState(MoveState.Airborne);
            }
            if (WaterDepth > swimEnterDepth) SetState(MoveState.Swimming);
        }

        void UpdateAirborne(float dt)
        {
            bool moving = moveDir.sqrMagnitude > 0.01f;
            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                planarVelocity = dashVelocity;
            }
            else if (moving)
            {
                float targetSpeed = Mathf.Max(planarVelocity.magnitude, runSpeed * 0.75f);
                planarVelocity = Vector3.Lerp(planarVelocity, moveDir.normalized * targetSpeed, 1f - Mathf.Exp(-airControl * dt));
                FaceDirection(moveDir, dt);
            }
            verticalSpeed = Mathf.Max(verticalSpeed + gravity * dt, -maxFallSpeed);

            if (jumpPressed && Stamina > 0f && verticalSpeed < 3f &&
                !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, minGlideHeight, envMask, QueryTriggerInteraction.Ignore))
            {
                SetState(MoveState.Gliding);
                return;
            }
            if (moving && climbCooldown <= 0f && Stamina > 0f && TryFindWall(moveDir, out var hit))
            {
                StartClimb(hit);
                return;
            }

            cc.Move((planarVelocity + Vector3.up * verticalSpeed) * dt);

            if (cc.isGrounded && verticalSpeed < 0f) SetState(MoveState.Grounded);
            else if ((cc.collisionFlags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if (WaterDepth > swimEnterDepth) SetState(MoveState.Swimming);
        }

        void UpdateGliding(float dt)
        {
            if (jumpPressed || !Drain(glideCostPerSec, dt))
            {
                SetState(MoveState.Airborne);
                return;
            }

            bool moving = moveDir.sqrMagnitude > 0.01f;
            Vector3 desired = moving ? moveDir.normalized * glideSpeed : transform.forward * glideSpeed * 0.35f;
            planarVelocity = Vector3.Lerp(planarVelocity, desired, 1f - Mathf.Exp(-2.5f * dt));
            if (moving) FaceDirection(moveDir, dt);
            verticalSpeed = Mathf.MoveTowards(verticalSpeed, -glideFallSpeed, 25f * dt);

            Vector3 probe = planarVelocity.sqrMagnitude > 0.01f ? planarVelocity : transform.forward;
            if (climbCooldown <= 0f && TryFindWall(probe, out var hit))
            {
                StartClimb(hit);
                return;
            }

            cc.Move((planarVelocity + Vector3.up * verticalSpeed) * dt);

            if (cc.isGrounded) SetState(MoveState.Grounded);
            if (WaterDepth > swimEnterDepth) SetState(MoveState.Swimming);
        }

        void UpdateClimbing(float dt)
        {
            if (dropPressed)
            {
                StopClimb();
                return;
            }

            float reach = cc.radius + 0.6f;
            Vector3 chest = transform.position + Vector3.up * 1f;
            if (!Physics.Raycast(chest, -wallNormal, out var hit, reach, envMask, QueryTriggerInteraction.Ignore))
            {
                // Chest is above the wall top: pull up over the ledge if the feet still touch it.
                if (Physics.Raycast(transform.position + Vector3.up * 0.15f, -wallNormal, reach, envMask, QueryTriggerInteraction.Ignore))
                    Vault();
                else
                    StopClimb();
                return;
            }
            if (hit.normal.y >= maxClimbNormalY)
            {
                Vault();
                return;
            }

            wallNormal = Vector3.Slerp(wallNormal, hit.normal, 1f - Mathf.Exp(-12f * dt)).normalized;
            Vector3 right = Vector3.Cross(wallNormal, Vector3.up).normalized;
            Vector3 up = Vector3.Cross(right, wallNormal).normalized;

            Vector3 move;
            if (climbBoostTimer > 0f)
            {
                climbBoostTimer -= dt;
                move = climbBoostDir * (climbJumpDistance / climbBoostDuration);
                usedStamina = true;
            }
            else
            {
                move = (right * moveInput.x + up * moveInput.y) * climbSpeed;
                if (moveInput.sqrMagnitude > 0.01f)
                {
                    if (!Drain(climbCostPerSec, dt))
                    {
                        StopClimb();
                        return;
                    }
                }
                else usedStamina = true; // hanging still blocks regeneration

                if (jumpPressed && SpendBurst(climbJumpCost))
                {
                    climbBoostTimer = climbBoostDuration;
                    Vector3 d = right * moveInput.x + up * Mathf.Max(moveInput.y, 0f);
                    climbBoostDir = d.sqrMagnitude > 0.01f ? d.normalized : up;
                }
            }

            Vector3 stick = -wallNormal * (hit.distance - (cc.radius + 0.05f));
            cc.Move(move * dt + stick);

            Vector3 face = Vector3.ProjectOnPlane(-wallNormal, Vector3.up);
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-12f * dt));

            if (moveInput.y < -0.1f && cc.isGrounded) SetState(MoveState.Grounded);
            if (WaterDepth > swimEnterDepth) SetState(MoveState.Swimming);
        }

        void UpdateSwimming(float dt)
        {
            bool moving = moveDir.sqrMagnitude > 0.01f;
            bool fast = sprintHeld && moving;
            if (!Drain(fast ? fastSwimCostPerSec : swimCostPerSec, dt))
            {
                Respawn(); // drowned
                return;
            }

            float speed = moving ? (fast ? fastSwimSpeed : swimSpeed) : 0f;
            planarVelocity = Vector3.Lerp(planarVelocity, moveDir.normalized * speed, 1f - Mathf.Exp(-3f * dt));
            if (moving) FaceDirection(moveDir, dt);
            verticalSpeed = Mathf.Clamp((waterLevel - swimDepth - transform.position.y) * 3f, -6f, 3f);

            if (moving && climbCooldown <= 0f && TryFindWall(moveDir, out var hit))
            {
                StartClimb(hit);
                return;
            }

            cc.Move((planarVelocity + Vector3.up * verticalSpeed) * dt);

            if (WaterDepth < swimExitDepth) SetState(MoveState.Grounded);
        }

        // ---------------------------------------------------------------- Climbing helpers

        bool TryFindWall(Vector3 dir, out RaycastHit hit)
        {
            dir.y = 0f;
            dir.Normalize();
            Vector3 origin = transform.position + Vector3.up * 1f;
            if (Physics.Raycast(origin, dir, out hit, cc.radius + 0.35f, envMask, QueryTriggerInteraction.Ignore))
                return hit.normal.y < maxClimbNormalY && hit.normal.y > -0.3f && Vector3.Dot(dir, -hit.normal) > 0.5f;
            return false;
        }

        void StartClimb(RaycastHit hit)
        {
            wallNormal = hit.normal;
            planarVelocity = Vector3.zero;
            verticalSpeed = 0f;
            climbBoostTimer = 0f;
            SetState(MoveState.Climbing);
        }

        void StopClimb()
        {
            SetState(MoveState.Airborne);
            planarVelocity = Vector3.ProjectOnPlane(wallNormal, Vector3.up) * 1.5f;
            verticalSpeed = 0f;
            climbCooldown = 0.5f;
        }

        void Vault()
        {
            SetState(MoveState.Airborne);
            verticalSpeed = 7.5f;
            planarVelocity = Vector3.ProjectOnPlane(-wallNormal, Vector3.up).normalized * 2.5f;
            climbCooldown = 0.3f;
        }

        // ---------------------------------------------------------------- Stamina

        bool Drain(float perSecond, float dt)
        {
            usedStamina = true;
            Stamina = Mathf.Max(0f, Stamina - perSecond * dt);
            return Stamina > 0f;
        }

        bool SpendBurst(float amount)
        {
            if (Stamina < amount) return false;
            Stamina -= amount;
            usedStamina = true;
            return true;
        }

        void UpdateStaminaRegen(float dt)
        {
            if (usedStamina || State == MoveState.Climbing || State == MoveState.Gliding || State == MoveState.Swimming)
            {
                regenTimer = regenDelay;
                return;
            }
            if (regenTimer > 0f)
            {
                regenTimer -= dt;
                return;
            }
            Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * dt);
        }

        // ---------------------------------------------------------------- Misc

        void UpdateSafePosition(float dt)
        {
            if (State != MoveState.Grounded || !cc.isGrounded || WaterDepth > 0.3f) return;
            safeTimer += dt;
            if (safeTimer > 1f)
            {
                safeTimer = 0f;
                lastSafePosition = transform.position;
            }
        }

        public void Respawn() => Teleport(lastSafePosition);

        public void Teleport(Vector3 position)
        {
            cc.enabled = false;
            transform.position = position + Vector3.up * 0.1f;
            cc.enabled = true;
            planarVelocity = Vector3.zero;
            verticalSpeed = 0f;
            Stamina = maxStamina;
            SetState(MoveState.Airborne);
        }

        void FaceDirection(Vector3 dir, float dt)
        {
            float target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, target, ref turnVelocity, turnSmoothTime, Mathf.Infinity, dt);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        void SetState(MoveState next)
        {
            if (State == next) return;
            State = next;
            airTime = 0f;
            if (next != MoveState.Grounded) IsSprinting = false;
            if (glider) glider.SetActive(next == MoveState.Gliding);
        }

        void UpdateVisual(float dt)
        {
            if (!visual) return;

            Vector3 localVel = transform.InverseTransformDirection(planarVelocity);
            Quaternion target = State switch
            {
                MoveState.Gliding => Quaternion.Euler(65f, 0f, -localVel.x * 3f),
                MoveState.Swimming => Quaternion.Euler(moveDir.sqrMagnitude > 0.01f ? 75f : 15f, 0f, 0f),
                MoveState.Climbing => Quaternion.Euler(-5f, 0f, 0f),
                _ => Quaternion.Euler(Mathf.Clamp(localVel.z * 1.2f, 0f, 12f), 0f, 0f),
            };
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 1f - Mathf.Exp(-10f * dt));

            if (!visualBob) return;
            float speed = planarVelocity.magnitude;
            if (State == MoveState.Grounded && speed > 0.5f) bobPhase += dt * speed * 2.2f;
            else bobPhase = Mathf.MoveTowards(bobPhase, Mathf.Round(bobPhase / Mathf.PI) * Mathf.PI, dt * 4f);
            visual.localPosition = visualBasePos + Vector3.up * (Mathf.Abs(Mathf.Sin(bobPhase)) * 0.06f * Mathf.Clamp01(speed / runSpeed));
        }
    }
}
