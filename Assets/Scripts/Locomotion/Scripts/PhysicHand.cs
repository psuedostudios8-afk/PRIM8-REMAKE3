using UnityEngine;

namespace Locomotion.Scripts
{
    public class PhysicHand : MonoBehaviour
    {
        public GrabInteractor interactor;
        public Rigidbody playerRigidbody, _rigidbody;

        [Header("PID")]
        public Transform target;
        public float posFrequency = 50f, posDamping = 1f;
        public float rotFrequency = 25f, rotDamping = 0.9f;

        [Header("Move forces")]
        public float climbForce = 1000;
        public float climbDrag = 500;
        public float normalForce = 1000;
        public float normalDrag = 500;

        private Vector3 _previousPosition;
        private bool _isColliding;
        private bool _isClimbing;
        private float _moveForce, _moveDrag;

        public LayerMask climbableLayers;
        public LayerMask grabbableLayers;

        private void Start()
        {
            _rigidbody.maxAngularVelocity = float.PositiveInfinity;

            transform.position = target.position;
            transform.rotation = target.rotation;
            _previousPosition = transform.position;
        }

        private void FixedUpdate()
        {
            // PID
            UpdatePosition();
            UpdateRotation();

            // HookesLaw
            if (_isColliding)
                HookesLaw();
        }

        private void UpdatePosition()
        {
            PidLogic(posFrequency, posDamping, out var ksg, out var kdg);

            var force = (target.position - transform.position) * ksg + (playerRigidbody.velocity - _rigidbody.velocity) * kdg;
            _rigidbody.AddForce(force, ForceMode.Acceleration);
        }

        private void UpdateRotation()
        {
            PidLogic(rotFrequency, rotDamping, out var ksg, out var kdg);

            var q = target.rotation * Quaternion.Inverse(transform.rotation);
            if (q.w < 0f)
            {
                q.x = -q.x;
                q.y = -q.y;
                q.z = -q.z;
                q.w = -q.w;
            }

            q.ToAngleAxis(out var angle, out var axis);
            axis.Normalize();
            axis *= Mathf.Deg2Rad;

            var torque = axis * (ksg * angle) + -_rigidbody.angularVelocity * kdg;
            _rigidbody.AddTorque(torque, ForceMode.Acceleration);
        }

        private void PidLogic(float frequency, float damping, out float ksg, out float kdg)
        {
            var kp = (6f * frequency) * (6f * frequency) * 0.25f;
            var kd = 4.5f * frequency * damping;
            var g = 1 / (1 + kd * Time.fixedDeltaTime + kp * Time.fixedDeltaTime * Time.fixedDeltaTime);
            ksg = kp * g;
            kdg = (kd + kp * Time.fixedDeltaTime) * g;
        }

        private void HookesLaw()
        {
            var displacementFromResting = transform.position - target.position;
            var force = displacementFromResting * _moveForce;
            var drag = GetDrag();

            playerRigidbody.AddForce(force, ForceMode.Acceleration);
            playerRigidbody.AddForce(-playerRigidbody.velocity * (drag * _moveDrag), ForceMode.Acceleration);
        }

        private float GetDrag()
        {
            var handVelocity = (transform.position - _previousPosition) / Time.fixedDeltaTime;
            _previousPosition = transform.position;

            var drag = Mathf.Clamp(handVelocity.magnitude, 0.03f, 1f);
            return drag;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (interactor != null && ((1 << other.gameObject.layer) & interactor.interactableLayer) != 0)
            {
                _isColliding = true;
                if (interactor != null && ((1 << other.gameObject.layer) & climbableLayers) != 0)
                {
                    _moveForce = climbForce;
                    _moveDrag = climbDrag;
                }
                else
                {
                    _moveForce = normalForce;
                    _moveDrag = normalDrag;
                }
            }
        }

        private void OnCollisionExit(Collision other)
        {
            if (interactor != null && ((1 << other.gameObject.layer) & interactor.interactableLayer) != 0)
                _isColliding = false;
        }
    }
}