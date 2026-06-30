using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ActiveRagdoll {
    // Isolated data packets that can easily be serialized across a network later
    [System.Serializable]
    public struct Player1Data {
        public Vector2 lookDelta;
    }

    [System.Serializable]
    public struct Player2Data {
        public float torsoLean;       // Backwards / Forwards [-1, 1]
        public bool liftLeftLeg;
        public bool extendLeftLeg;    // True = Forward, False = Backward
        public bool liftRightLeg;
        public bool extendRightLeg;   // True = Forward, False = Backward
    }

    public class InputModule : Module {
        [Header("--- CO-OP NETWORK STATES ---")]
        public Player1Data Player1Input;
        public Player2Data Player2Input;

        // --- BACKWARD COMPATIBILITY DELEGATES (Fixes compilation errors) ---
        public delegate void onMoveDelegate(Vector2 movement);
        public onMoveDelegate OnMoveDelegates { get; set; }

        public delegate void onLeftArmDelegate(float armWeight);
        public onLeftArmDelegate OnLeftArmDelegates { get; set; }

        public delegate void onRightArmDelegate(float armWeight);
        public onRightArmDelegate OnRightArmDelegates { get; set; }

        public delegate void onFloorChangedDelegate(bool onFloor);
        public onFloorChangedDelegate OnFloorChangedDelegates { get; set; }

        [Header("--- FLOOR DETECTION ---")]
        public float floorDetectionDistance = 0.3f;
        public float maxFloorSlope = 60;

        private bool _isOnFloor = true;
        public bool IsOnFloor { get { return _isOnFloor; } }

        private Rigidbody _rightFoot, _leftFoot;

        void Start() {
            _rightFoot = _activeRagdoll.GetPhysicalBone(HumanBodyBones.RightFoot).GetComponent<Rigidbody>();
            _leftFoot = _activeRagdoll.GetPhysicalBone(HumanBodyBones.LeftFoot).GetComponent<Rigidbody>();
        }

        void Update() {
            UpdateOnFloor();
            ReadLocalInputs(); // Temporarily polling locally for single player
        }

        // --- INPUT SYSTEM BROADCAST HANDLERS ---
        // These keep the original input callbacks functional
        public void OnMove(InputValue value) {
            Vector2 move = value.Get<Vector2>();
            OnMoveDelegates?.Invoke(move);
        }

        public void OnLeftArm(InputValue value) {
            float weight = value.Get<float>();
            OnLeftArmDelegates?.Invoke(weight);
        }

        public void OnRightArm(InputValue value) {
            float weight = value.Get<float>();
            OnRightArmDelegates?.Invoke(weight);
        }

        /// <summary>
        /// Reads local hardware keys for Player 2. In multiplayer, this block will 
        /// only execute on Client 2 and then sync over the network.
        /// </summary>
        private void ReadLocalInputs() {
            // Torso Lean (W = Lean Forward, S = Lean Backward)
            if (Keyboard.current.wKey.isPressed) Player2Input.torsoLean = 1f;
            else if (Keyboard.current.sKey.isPressed) Player2Input.torsoLean = -1f;
            else Player2Input.torsoLean = 0f;

            // Left Leg (Q = Lift Up, A = Reach Forward)
            Player2Input.liftLeftLeg = Keyboard.current.qKey.isPressed;
            Player2Input.extendLeftLeg = Keyboard.current.aKey.isPressed;

            // Right Leg (E = Lift Up, D = Reach Forward)
            Player2Input.liftRightLeg = Keyboard.current.eKey.isPressed;
            Player2Input.extendRightLeg = Keyboard.current.dKey.isPressed;
        }

        // ---------- INTERNAL FLOOR SENSORS ----------
        private void UpdateOnFloor() {
            bool lastIsOnFloor = _isOnFloor;
            _isOnFloor = CheckRigidbodyOnFloor(_rightFoot, out Vector3 foo) || CheckRigidbodyOnFloor(_leftFoot, out foo);

            if (_isOnFloor != lastIsOnFloor)
                OnFloorChangedDelegates?.Invoke(_isOnFloor);
        }

        public bool CheckRigidbodyOnFloor(Rigidbody bodyPart, out Vector3 normal) {
            Ray ray = new Ray(bodyPart.position, Vector3.down);
            bool onFloor = Physics.Raycast(ray, out RaycastHit info, floorDetectionDistance, ~(1 << bodyPart.gameObject.layer));
            onFloor = onFloor && Vector3.Angle(info.normal, Vector3.up) <= maxFloorSlope;

            if (onFloor && info.collider.gameObject.TryGetComponent<Floor>(out Floor floor))
                onFloor = floor.isFloor;

            normal = info.normal;
            return onFloor;
        }
    }
}