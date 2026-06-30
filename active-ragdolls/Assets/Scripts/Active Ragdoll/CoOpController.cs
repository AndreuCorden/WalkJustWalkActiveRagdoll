using UnityEngine;

namespace ActiveRagdoll {
    public class CoOpController : MonoBehaviour {
        [Header("--- Framework Links ---")]
        [SerializeField] private ActiveRagdoll _activeRagdoll;
        [SerializeField] private PhysicsModule _physicsModule;
        [SerializeField] private AnimationModule _animationModule;
        [SerializeField] private GripModule _gripModule;
        private AnimatorHelper _ikHelper;

        [Header("--- Player 2 Foot Tuning ---")]
        public float stepHeight = 0.45f;
        public float stepLength = 0.5f;
        public float footTransitSpeed = 10f;
        public float footHorizontalSpacing = 0.18f;

        // Position state blending vectors
        private Vector3 _leftFootLocalTarget;
        private Vector3 _rightFootLocalTarget;
        private bool _isGrounded = true;

        private void Start() {
            if (_activeRagdoll == null) _activeRagdoll = GetComponent<ActiveRagdoll>();
            if (_physicsModule == null) _physicsModule = GetComponent<PhysicsModule>();
            if (_animationModule == null) _animationModule = GetComponent<AnimationModule>();
            if (_gripModule == null) _gripModule = GetComponent<GripModule>();

            _ikHelper = _activeRagdoll.AnimatorHelper;

            // Turn on Inverse Kinematics dominance for the legs
            _ikHelper.LeftLegIKWeight = 1.0f;
            _ikHelper.RightLegIKWeight = 1.0f;

            // Pause automated walk loops from fighting our inputs
            if (_animationModule.Animator != null) {
                _animationModule.Animator.speed = 0f; 
            }

            _leftFootLocalTarget = new Vector3(-footHorizontalSpacing, 0f, 0f);
            _rightFootLocalTarget = new Vector3(footHorizontalSpacing, 0f, 0f);

            // Wire up internal events
            _activeRagdoll.Input.OnFloorChangedDelegates += HandleFloorChanged;
            
            // Wire up Player 1 arm/grip system interactions (formerly in DefaultBehaviour)
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
            // Player 1 handles camera aiming trajectory data
            if (_activeRagdoll.TryGetComponent(out CameraModule cameraMod) && cameraMod.Camera != null) {
                Vector3 directionalHeading = Vector3.ProjectOnPlane(cameraMod.Camera.transform.forward, Vector3.up).normalized;
                
                _physicsModule.TargetDirection = directionalHeading;
                _animationModule.AimDirection = directionalHeading;
            }
        }

        private void ProcessPlayer2Motor() {
            Player2Data p2Input = _activeRagdoll.Input.Player2Input;
            Transform physicalTorso = _activeRagdoll.PhysicalTorso.transform;

            // --- PROCESS LEFT FOOT ---
            float targetLeftY = p2Input.liftLeftLeg ? stepHeight : 0f;
            float targetLeftZ = p2Input.extendLeftLeg ? stepLength : -stepLength * 0.3f;

            _leftFootLocalTarget.x = -footHorizontalSpacing;
            _leftFootLocalTarget.y = Mathf.MoveTowards(_leftFootLocalTarget.y, targetLeftY, Time.deltaTime * footTransitSpeed);
            _leftFootLocalTarget.z = Mathf.MoveTowards(_leftFootLocalTarget.z, targetLeftZ, Time.deltaTime * footTransitSpeed);

            // --- PROCESS RIGHT FOOT ---
            float targetRightY = p2Input.liftRightLeg ? stepHeight : 0f;
            float targetRightZ = p2Input.extendRightLeg ? stepLength : -stepLength * 0.3f;

            _rightFootLocalTarget.x = footHorizontalSpacing;
            _rightFootLocalTarget.y = Mathf.MoveTowards(_rightFootLocalTarget.y, targetRightY, Time.deltaTime * footTransitSpeed);
            _rightFootLocalTarget.z = Mathf.MoveTowards(_rightFootLocalTarget.z, targetRightZ, Time.deltaTime * footTransitSpeed);

            // --- SPATIAL CONVERSION TO RAGDOLL SYSTEM TARGETS ---
            Vector3 centerGroundBase = physicalTorso.position;
            centerGroundBase.y = transform.position.y; 

            Vector3 finalLeftWorldPos = centerGroundBase + 
                                         (physicalTorso.right * _leftFootLocalTarget.x) + 
                                         (Vector3.up * _leftFootLocalTarget.y) + 
                                         (physicalTorso.forward * _leftFootLocalTarget.z);

            Vector3 finalRightWorldPos = centerGroundBase + 
                                          (physicalTorso.right * _rightFootLocalTarget.x) + 
                                          (Vector3.up * _rightFootLocalTarget.y) + 
                                          (physicalTorso.forward * _rightFootLocalTarget.z);

            _ikHelper.LeftFootTarget.position = finalLeftWorldPos;
            _ikHelper.RightFootTarget.position = finalRightWorldPos;

            _ikHelper.LeftFootTarget.rotation = Quaternion.LookRotation(physicalTorso.forward, Vector3.up);
            _ikHelper.RightFootTarget.rotation = Quaternion.LookRotation(physicalTorso.forward, Vector3.up);
        }

        /// <summary>
        /// Replaces the old floor logic inside DefaultBehaviour to prevent control loops from clashing.
        /// </summary>
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
                // If tripped or airborne, drop joints strength scaling to simulate ragdolling out of control
                _physicsModule.SetBalanceMode(PhysicsModule.BALANCE_MODE.MANUAL_TORQUE);
                _activeRagdoll.GetBodyPart("Head Neck")?.SetStrengthScale(0.1f);
                _activeRagdoll.GetBodyPart("Right Leg")?.SetStrengthScale(0.05f);
                _activeRagdoll.GetBodyPart("Left Leg")?.SetStrengthScale(0.05f);
                _animationModule.PlayAnimation("InTheAir");
            }
        }
    }
}