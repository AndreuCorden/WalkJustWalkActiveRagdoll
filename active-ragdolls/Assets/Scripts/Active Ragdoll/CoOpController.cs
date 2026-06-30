using UnityEngine;

namespace ActiveRagdoll {
    public class CoOpController : MonoBehaviour {
        [Header("--- Framework Links ---")]
        [SerializeField] private ActiveRagdoll _activeRagdoll;
        [SerializeField] private PhysicsModule _physicsModule;
        [SerializeField] private AnimationModule _animationModule;
        [SerializeField] private GripModule _gripModule;
        private AnimatorHelper _ikHelper;

        [Header("--- Posture & Height Tuning ---")]
        public float stanceYOffset = -0.15f; 
        public float footHorizontalSpacing = 0.18f;
        [Tooltip("How far forward the knee hints push to prevent backward bending.")]
        public float kneeHintForwardOffset = 0.4f;

        [Header("--- Player 2 Step Mechanics ---")]
        public float stepHeight = 0.4f;
        public float stepLength = 0.5f;
        public float footTransitSpeed = 12f;

        // Directional blending processors
        private float _leftFootLiftBlender;
        private float _leftFootExtendBlender;
        private float _rightFootLiftBlender;
        private float _rightFootExtendBlender;

        private bool _isGrounded = true;

        private void Start() {
            if (_activeRagdoll == null) _activeRagdoll = GetComponent<ActiveRagdoll>();
            if (_physicsModule == null) _physicsModule = GetComponent<PhysicsModule>();
            if (_animationModule == null) _animationModule = GetComponent<AnimationModule>();
            if (_gripModule == null) _gripModule = GetComponent<GripModule>();

            _ikHelper = _activeRagdoll.AnimatorHelper;

            _ikHelper.LeftLegIKWeight = 1.0f;
            _ikHelper.RightLegIKWeight = 1.0f;

            if (_animationModule.Animator != null) {
                _animationModule.Animator.speed = 0f; 
            }

            _activeRagdoll.Input.OnFloorChangedDelegates += HandleFloorChanged;
            _activeRagdoll.Input.OnLeftArmDelegates += _animationModule.UseLeftArm;
            _activeRagdoll.Input.OnLeftArmDelegates += _gripModule.UseLeftGrip;
            _activeRagdoll.Input.OnRightArmDelegates += _animationModule.UseRightArm;
            _activeRagdoll.Input.OnRightArmDelegates += _gripModule.UseRightGrip;
        }

        private void Update() {
            ProcessPlayer1Director();
            if (_isGrounded) {
                ProcessPlayer2Motor();
            }
        }

        private void ProcessPlayer1Director() {
            if (_activeRagdoll.TryGetComponent(out CameraModule cameraMod) && cameraMod.Camera != null) {
                Vector3 directionalHeading = Vector3.ProjectOnPlane(cameraMod.Camera.transform.forward, Vector3.up).normalized;
                _physicsModule.TargetDirection = directionalHeading;
                _animationModule.AimDirection = directionalHeading;
            }
        }

        private void ProcessPlayer2Motor() {
            Player2Data p2Input = _activeRagdoll.Input.Player2Input;
            Transform physicalTorso = _activeRagdoll.PhysicalTorso.transform;

            // --- 1. PROCESS INPUT MODIFIERS (Q=Lift, A=Forward, Z=Backward) ---
            float targetLeftLift = p2Input.liftLeftLeg ? stepHeight : 0f;
            float targetLeftExtend = 0f;
            if (p2Input.extendLeftLeg) targetLeftExtend = stepLength;       // A key pressed
            else if (Input.GetKey(KeyCode.Z)) targetLeftExtend = -stepLength; // Z key pressed

            _leftFootLiftBlender = Mathf.MoveTowards(_leftFootLiftBlender, targetLeftLift, Time.deltaTime * footTransitSpeed);
            _leftFootExtendBlender = Mathf.MoveTowards(_leftFootExtendBlender, targetLeftExtend, Time.deltaTime * footTransitSpeed);

            // --- 2. PROCESS INPUT MODIFIERS (E=Lift, D=Forward, C=Backward) ---
            float targetRightLift = p2Input.liftRightLeg ? stepHeight : 0f;
            float targetRightExtend = 0f;
            if (p2Input.extendRightLeg) targetRightExtend = stepLength;       // D key pressed
            else if (Input.GetKey(KeyCode.C)) targetRightExtend = -stepLength; // C key pressed

            _rightFootLiftBlender = Mathf.MoveTowards(_rightFootLiftBlender, targetRightLift, Time.deltaTime * footTransitSpeed);
            _rightFootExtendBlender = Mathf.MoveTowards(_rightFootExtendBlender, targetRightExtend, Time.deltaTime * footTransitSpeed);

            // --- 3. BASE POSITION ANCHOR ---
            Vector3 groundBaseAnchor = physicalTorso.position;
            groundBaseAnchor.y = transform.position.y; 

            // --- 4. CALCULATE TARGET WORLD POSITIONS ---
            Vector3 finalLeftWorldPos = groundBaseAnchor + 
                                         (physicalTorso.right * -footHorizontalSpacing) + 
                                         (Vector3.up * (_leftFootLiftBlender + stanceYOffset)) + 
                                         (physicalTorso.forward * _leftFootExtendBlender);

            Vector3 finalRightWorldPos = groundBaseAnchor + 
                                          (physicalTorso.right * footHorizontalSpacing) + 
                                          (Vector3.up * (_rightFootLiftBlender + stanceYOffset)) + 
                                          (physicalTorso.forward * _rightFootExtendBlender);

            // --- 5. SHIP TRANSFORMS TO HINT DRIVERS ---
            _ikHelper.LeftFootTarget.position = finalLeftWorldPos;
            _ikHelper.RightFootTarget.position = finalRightWorldPos;

            _ikHelper.LeftFootTarget.rotation = Quaternion.LookRotation(physicalTorso.forward, Vector3.up);
            _ikHelper.RightFootTarget.rotation = Quaternion.LookRotation(physicalTorso.forward, Vector3.up);

            // --- 6. POSITION KNEE HINTS DYNAMICALLY IN FRONT OF LEGS ---
            if (_ikHelper.LeftKneeHint != null) {
                _ikHelper.LeftKneeHint.position = groundBaseAnchor + 
                                                  (physicalTorso.right * -footHorizontalSpacing) + 
                                                  (Vector3.up * (_leftFootLiftBlender + stanceYOffset + 0.4f)) + 
                                                  (physicalTorso.forward * (_leftFootExtendBlender + kneeHintForwardOffset));
            }
            if (_ikHelper.RightKneeHint != null) {
                _ikHelper.RightKneeHint.position = groundBaseAnchor + 
                                                   (physicalTorso.right * footHorizontalSpacing) + 
                                                   (Vector3.up * (_rightFootLiftBlender + stanceYOffset + 0.4f)) + 
                                                   (physicalTorso.forward * (_rightFootExtendBlender + kneeHintForwardOffset));
            }
        }

        private void HandleFloorChanged(bool onFloor) {
            _isGrounded = onFloor;
            if (onFloor) {
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.STABILIZER_JOINT);
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(1f);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(1f);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(1f);
                _animationModule.PlayAnimation("Idle");
            }
            else {
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.MANUAL_TORQUE);
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0.1f);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0.05f);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0.05f);
                _animationModule.PlayAnimation("InTheAir");
            }
        }
    }
}