using UnityEngine;

namespace Locomotion.Scripts
{
    public class Surface : MonoBehaviour
    {
        [Header("Surface Type")]
        [Tooltip("Is this surface slippery/icy?")]
        public bool IsSlippy = false;

        [Tooltip("Is this a moving platform?")]
        public bool IsMovable = false;

        [Header("Slip/Slide Settings")]
        [Range(0f, 1f)]
        [Tooltip("0 = No sliding (sticky), 1 = Full sliding (ice)")]
        public float slipPercentage = 0.03f;

        [Range(0.1f, 2f)]
        [Tooltip("Multiplier for slip speed (lower = slower sliding)")]
        public float slipSpeedMultiplier = 0.5f;

        [Header("Moving Platform Settings")]
        [Tooltip("The velocity of the platform (auto-computed from animation or rigidbody)")]
        public Vector3 surfaceVelocity = Vector3.zero;

        private Rigidbody _surfaceRigidbody;
        private Animator _animator;
        private Vector3 _lastPosition;

        [Header("Audio (Optional)")]
        public AudioClip[] handTouchSounds;

        [Header("Debug")]
        public bool showDebug = false;

        private void Awake()
        {
            _surfaceRigidbody = GetComponent<Rigidbody>();
            _animator = GetComponent<Animator>();
            _lastPosition = transform.position;
        }

        private void Update()
        {
            if (IsMovable)
            {
                if (_surfaceRigidbody != null && !_surfaceRigidbody.isKinematic)
                {
                    surfaceVelocity = _surfaceRigidbody.velocity;
                }
                else
                {
                    Vector3 currentPosition = transform.position;
                    surfaceVelocity = (currentPosition - _lastPosition) / Time.deltaTime;
                    _lastPosition = currentPosition;

                    if (showDebug && surfaceVelocity.magnitude > 0.01f)
                    {
                        Debug.Log($"[Surface] {gameObject.name} velocity: {surfaceVelocity.magnitude:F3} m/s - Direction: {surfaceVelocity}");
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsMovable && showDebug)
            {
                Debug.Log($"[Surface] {gameObject.name} collision ENTER with {collision.gameObject.name} on layer {LayerMask.LayerToName(collision.gameObject.layer)}");
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (IsMovable && showDebug)
            {
                Debug.Log($"[Surface] {gameObject.name} collision STAY with {collision.gameObject.name} on layer {LayerMask.LayerToName(collision.gameObject.layer)}");
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (IsMovable && showDebug)
            {
                Debug.Log($"[Surface] {gameObject.name} collision EXIT with {collision.gameObject.name} on layer {LayerMask.LayerToName(collision.gameObject.layer)}");
            }
        }

        public void PlayHandTouchSound(Vector3 position)
        {
            if (handTouchSounds != null && handTouchSounds.Length > 0)
            {
                AudioClip clip = handTouchSounds[Random.Range(0, handTouchSounds.Length)];
                AudioSource.PlayClipAtPoint(clip, position, 0.5f);
            }
        }
    }
}