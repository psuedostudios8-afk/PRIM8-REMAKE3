using UnityEngine;
using StarLight.Inputs;

namespace Vane.FingerMovement
{
    public class FingerMovementManager : MonoBehaviour
    {
        [Header("Finger References")]
        public Finger index;
        public Finger pinky;
        public Finger thumb;

        [Header("Settings")]
        public float maxCurlAngle = 60f;
        public float backwardBendAngle = -10f;
        public float castLength = 0.05f;
        public float castRadius = 0.015f;
        public LayerMask collisionLayers;
        public float smoothSpeed = 15f;
        
        [Header("Smoothing")]
        [Range(0f, 0.95f)]
        [Tooltip("Input smoothing to eliminate controller jitter. 0 = no smoothing, 0.1 = slight, 0.3+ = heavy")]
        public float inputSmoothing = 0.15f;
        
        [Header("Anti-Jitter")]
        [Range(0.001f, 0.1f)]
        [Tooltip("Hysteresis threshold to prevent collision jitter. Higher = more stable")]
        public float hysteresisThreshold = 0.02f;

        [Range(0f, 1f)] public float minIndexCurl = 0.1f;
        [Range(0f, 1f)] public float minPinkyCurl = 0.1f;
        [Range(0f, 1f)] public float minThumbCurl = 0.1f;

        [Header("Input Settings")]
        public StarLightHand inputHand = StarLightHand.RightHand;

        [Header("Debug")]
        public bool debugMode = false;
        [Range(0f, 1f)] public float debugIndexCurl;
        [Range(0f, 1f)] public float debugPinkyCurl;
        [Range(0f, 1f)] public float debugThumbCurl;
        public bool showDebug;
        
        // Smoothed input values
        private float _smoothedIndexInput;
        private float _smoothedPinkyInput;
        private float _smoothedThumbInput;
        
        // Previous compression values for hysteresis
        private float _prevIndexCompression;
        private float _prevPinkyCompression;
        private float _prevThumbCompression;

        void OnEnable()
        {
            index.Init();
            pinky.Init();
            thumb.Init();
        }

        void FixedUpdate()
        {
            // Get raw input values
            float rawIndexInput = debugMode ? debugIndexCurl : StarLightInputs.GetAxis(AxisCode.Trigger, inputHand) / 100f;
            float rawPinkyInput = debugMode ? debugPinkyCurl : StarLightInputs.GetAxis(AxisCode.Grip, inputHand) / 100f;
            float rawThumbInput = debugMode ? debugThumbCurl : (StarLightInputs.GetButton(ButtonCode.PrimaryButton, inputHand) ? 1f : 0f);

            // Apply input smoothing to eliminate controller jitter
            if (inputSmoothing > 0f)
            {
                _smoothedIndexInput = Mathf.Lerp(rawIndexInput, _smoothedIndexInput, inputSmoothing);
                _smoothedPinkyInput = Mathf.Lerp(rawPinkyInput, _smoothedPinkyInput, inputSmoothing);
                _smoothedThumbInput = Mathf.Lerp(rawThumbInput, _smoothedThumbInput, inputSmoothing);
            }
            else
            {
                _smoothedIndexInput = rawIndexInput;
                _smoothedPinkyInput = rawPinkyInput;
                _smoothedThumbInput = rawThumbInput;
            }

            Animate(index, _smoothedIndexInput, minIndexCurl, ref _prevIndexCompression);
            Animate(pinky, _smoothedPinkyInput, minPinkyCurl, ref _prevPinkyCompression);
            Animate(thumb, _smoothedThumbInput, minThumbCurl, ref _prevThumbCompression);
        }

        void Animate(Finger finger, float targetCurl, float minCurl, ref float prevCompression)
        {
            if (finger.bones == null || finger.bones.Length == 0) return;

            float compression = 0f;

            for (int i = 0; i < finger.bones.Length; i++)
            {
                Vector3 origin = finger.bones[i].position;
                Vector3 dir = finger.bones[i].TransformDirection(finger.localCastDirections[i].normalized);

                float hitRatio = 1f;
                bool hitThis = false;

                if (Physics.SphereCast(origin, castRadius, dir, out RaycastHit hit, castLength, collisionLayers))
                {
                    hitRatio = hit.distance / castLength;
                    hitThis = true;
                }
                else
                {
                    if (Physics.OverlapSphere(origin, castRadius, collisionLayers).Length > 0)
                    {
                        hitRatio = 0f;
                        hitThis = true;
                    }
                }

                if (hitThis) compression = Mathf.Max(compression, 1f - hitRatio);
                if (showDebug) Debug.DrawRay(origin, dir * castLength, hitThis ? Color.red : Color.green);
            }

            // Apply hysteresis to prevent jitter at collision boundaries
            float compressionDiff = Mathf.Abs(compression - prevCompression);
            if (compressionDiff < hysteresisThreshold)
            {
                // Small change - keep the previous value to prevent oscillation
                compression = prevCompression;
            }
            else
            {
                // Significant change - smooth the transition
                compression = Mathf.Lerp(prevCompression, compression, Time.fixedDeltaTime * smoothSpeed * 2f);
            }
            
            prevCompression = compression;

            float clampedCurl = Mathf.Clamp(targetCurl, minCurl, 1f);
            
            // Use fixedDeltaTime since we're in FixedUpdate
            finger.current = Mathf.Lerp(finger.current, clampedCurl, Time.fixedDeltaTime * smoothSpeed);

            float totalForwardAngle = maxCurlAngle * finger.current;
            float totalAngle = Mathf.Lerp(totalForwardAngle, backwardBendAngle, compression);
            float anglePerBone = totalAngle / finger.bones.Length;

            for (int i = 0; i < finger.bones.Length; i++)
            {
                float angle = anglePerBone * (i + 1);
                Quaternion targetRotation = finger.startRot[i] * Quaternion.AngleAxis(angle, finger.axis);
                
                // Use fixedDeltaTime and ensure stable rotation interpolation
                float rotationSpeed = Time.fixedDeltaTime * smoothSpeed;
                finger.bones[i].localRotation = Quaternion.Slerp(finger.bones[i].localRotation, targetRotation, rotationSpeed);
            }
        }

        [System.Serializable]
        public class Finger
        {
            public Transform root;
            public Transform[] bones;
            public Vector3[] localCastDirections;
            public Vector3 axis = Vector3.right;

            [HideInInspector] public Quaternion[] startRot;
            [HideInInspector] public float current;

            public void Init()
            {
                if (bones == null || bones.Length == 0) return;

                startRot = new Quaternion[bones.Length];

                if (localCastDirections == null || localCastDirections.Length != bones.Length)
                    localCastDirections = new Vector3[bones.Length];

                for (int i = 0; i < bones.Length; i++)
                {
                    startRot[i] = bones[i].localRotation;
                    if (localCastDirections[i] == Vector3.zero) localCastDirections[i] = Vector3.up;
                }
            }
        }
    }
}