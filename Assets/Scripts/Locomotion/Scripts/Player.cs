using System;
using UnityEngine;
using UnityEngine.XR;

namespace Locomotion.Scripts
{
    public enum ClimbingMode
    {
        Kinematic,
        Physics
    }

    public enum ClimbGrabMethod
    {
        ForceBasedPush,
        TrapCube
    }

    public class Player : MonoBehaviour
    {
        private static Player _instance;
        public static Player Instance => _instance;

        [Header("Colliders")]
        public SphereCollider headCollider;
        public SphereCollider bodySphereTop;
        public SphereCollider bodySphereBottom;

        [Header("Followers")]
        public Transform leftHandFollower;
        public Transform rightHandFollower;
        public Transform rightHandTransform;
        public Transform leftHandTransform;

        [Header("Velocity and arm length")]
        public int velocityHistorySize;
        public float maxArmLength = 1.5f;
        public float unStickDistance = 1f;

        [Header("Settings")]
        public float velocityLimit;
        public float maxJumpSpeed;
        public float jumpMultiplier;
        public float minimumRaycastDistance = 0.01f;
        public float defaultSlideFactor = 0.03f;
        public float defaultPrecision = 0.995f;

        [Header("Climbing Settings")]
        [Tooltip("Kinematic = Smooth direct movement, Physics = Force-based (more realistic)")]
        public ClimbingMode climbingMode = ClimbingMode.Kinematic;

        [Tooltip("How hand is locked during climbing")]
        public ClimbGrabMethod climbGrabMethod = ClimbGrabMethod.ForceBasedPush;

        [Tooltip("Trap cube prefab spawned to physically trap hand (TrapCube method)")]
        public GameObject handTrapCubePrefab;

        [Tooltip("Physics mode: Strength of climbing forces")]
        [Range(1f, 100f)]
        public float climbStrength = 30f;

        [Tooltip("Physics mode: Velocity damping during climb")]
        [Range(0f, 10f)]
        public float climbDamping = 2f;

        [Tooltip("Kinematic mode: Smoothing factor (higher = smoother but slower response)")]
        [Range(0.01f, 0.5f)]
        public float kinematicSmoothing = 0.15f;

        [Header("Slip/Slide Settings")]
        [Range(0f, 0.98f)]
        [Tooltip("Threshold for slip behavior (below) vs slide behavior (above)")]
        public float slipThreshold = 0.5f;

        [Range(0.01f, 1f)]
        [Tooltip("Overall slip/slide strength multiplier (lower = slower)")]
        public float slipStrength = 0.05f;

        [Header("Moving Platform Settings")]
        public float movingThreshold = 0.1f;
        public bool debugMovingPlatform = false;

        [Header("More Debug Shit")]
        public Vector3 rightHandOffset;
        public Vector3 leftHandOffset;

        [Header("Layer Mask")]
        public LayerMask locomotionEnabledLayers;

        [Header("Debug")]
        public bool wasLeftHandTouching;
        public bool wasRightHandTouching;
        public bool disableMovement;
        public bool handSliding;
        public bool handSlipping;
        public float currentFactor;

        [Header("Item Weight")]
        private float _heldItemWeight = 0f;
        public float HeldItemWeight => _heldItemWeight;

        private Vector3 _lastLeftHandPosition;
        private Vector3 _lastRightHandPosition;
        private Vector3 _lastHeadPosition;
        private Vector3 _lastBodyTopPosition;
        private Vector3 _lastBodyBottomPosition;

        private Rigidbody _playerRigidBody;

        private Vector3[] _velocityHistory;
        private int _velocityIndex;
        private Vector3 _currentVelocity;
        private Vector3 _denormalizedVelocityAverage;
        private bool _jumpHandIsLeft;
        private Vector3 _lastPosition;

        private Surface _leftHandSurface;
        private Surface _rightHandSurface;
        private Surface _currentPlatform;
        private Surface _currentSurface;

        private Vector3 _targetPosition;
        private bool _isClimbing;

        private int _bodyCollidersLayer;
        private int _handsCollidersLayer;

        #region Initialization

        private void Awake()
        {
            if (_instance != null && _instance != this)
                Destroy(gameObject);
            else
                _instance = this;

            InitializeValues();
        }

        private void InitializeValues()
        {
            _playerRigidBody = GetComponent<Rigidbody>();
            _velocityHistory = new Vector3[velocityHistorySize];
            _lastLeftHandPosition = leftHandFollower.transform.position;
            _lastRightHandPosition = rightHandFollower.transform.position;
            _lastHeadPosition = headCollider.transform.position;
            _lastBodyTopPosition = bodySphereTop.transform.position;
            _lastBodyBottomPosition = bodySphereBottom.transform.position;
            _velocityIndex = 0;
            _lastPosition = transform.position;
            currentFactor = defaultSlideFactor;
            _targetPosition = transform.position;

            _bodyCollidersLayer = LayerMask.NameToLayer("Body/Colliders");
            _handsCollidersLayer = LayerMask.NameToLayer("Hands/Colliders");
        }

        #endregion

        #region Item Weight

        public void SetHeldItemWeight(float weight) => _heldItemWeight = weight;
        public void ClearHeldItemWeight() => _heldItemWeight = 0f;

        public static Vector3 CalculateClampedMovement(Vector3 movement, float mass, float dt)
        {
            if (mass <= 0f) return movement;
            float movementFactor = 1f / (mass / 100f);
            return movement * movementFactor * dt;
        }

        #endregion

        #region Hand Position Helpers

        private Vector3 CurrentLeftHandPosition()
        {
            if ((PositionWithOffset(leftHandTransform, leftHandOffset) - headCollider.transform.position).magnitude < maxArmLength)
                return PositionWithOffset(leftHandTransform, leftHandOffset);

            return headCollider.transform.position +
                   (PositionWithOffset(leftHandTransform, leftHandOffset) - headCollider.transform.position)
                   .normalized * maxArmLength;
        }

        private Vector3 CurrentRightHandPosition()
        {
            if ((PositionWithOffset(rightHandTransform, rightHandOffset) - headCollider.transform.position).magnitude < maxArmLength)
                return PositionWithOffset(rightHandTransform, rightHandOffset);

            return headCollider.transform.position +
                   (PositionWithOffset(rightHandTransform, rightHandOffset) - headCollider.transform.position)
                   .normalized * maxArmLength;
        }

        private static Vector3 PositionWithOffset(Transform transformToModify, Vector3 offsetVector)
        {
            return transformToModify.position + transformToModify.rotation * offsetVector;
        }

        #endregion

        #region Main Update Loop

        private void Update()
        {
            Vector3 rigidBodyMovement;
            var firstIterationLeftHand = Vector3.zero;
            var firstIterationRightHand = Vector3.zero;

            var leftHandColliding = false;
            var rightHandColliding = false;

            var distanceTraveled = CurrentLeftHandPosition() - _lastLeftHandPosition +
                                   Vector3.down * (2f * 9.8f * Time.deltaTime * Time.deltaTime);

            if (IterativeCollisionSphereCast(_lastLeftHandPosition, minimumRaycastDistance, distanceTraveled,
                    defaultPrecision, out var finalPosition, true, out _leftHandSurface))
            {
                if (wasLeftHandTouching)
                    firstIterationLeftHand = _lastLeftHandPosition - CurrentLeftHandPosition();
                else
                    firstIterationLeftHand = finalPosition - CurrentLeftHandPosition();

                if (!handSlipping && !handSliding)
                    _playerRigidBody.velocity = Vector3.zero;

                leftHandColliding = true;
            }
            else
            {
                _leftHandSurface = null;
            }

            distanceTraveled = CurrentRightHandPosition() - _lastRightHandPosition +
                               Vector3.down * (2f * 9.8f * Time.deltaTime * Time.deltaTime);

            if (IterativeCollisionSphereCast(_lastRightHandPosition, minimumRaycastDistance, distanceTraveled,
                    defaultPrecision, out finalPosition, true, out _rightHandSurface))
            {
                if (wasRightHandTouching)
                    firstIterationRightHand = _lastRightHandPosition - CurrentRightHandPosition();
                else
                    firstIterationRightHand = finalPosition - CurrentRightHandPosition();

                if (!handSlipping && !handSliding)
                    _playerRigidBody.velocity = Vector3.zero;

                rightHandColliding = true;
            }
            else
            {
                _rightHandSurface = null;
            }

            _isClimbing = (leftHandColliding || wasLeftHandTouching) || (rightHandColliding || wasRightHandTouching);

            float slipSpeedMultiplier = GetAverageSlipSpeedMultiplier(leftHandColliding || wasLeftHandTouching, rightHandColliding || wasRightHandTouching);

            if (!handSlipping && !handSliding)
            {
                if ((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching))
                    rigidBodyMovement = (firstIterationLeftHand + firstIterationRightHand) / 2;
                else
                    rigidBodyMovement = firstIterationLeftHand + firstIterationRightHand;
            }
            else
            {
                Vector3 slipVelocity = (_playerRigidBody.velocity * Time.deltaTime * slipSpeedMultiplier * slipStrength) / (currentFactor * 10f);

                if ((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching))
                    rigidBodyMovement = ((firstIterationLeftHand + firstIterationRightHand) / 2) + slipVelocity;
                else
                    rigidBodyMovement = (firstIterationLeftHand + firstIterationRightHand) + slipVelocity;
            }

            if (_heldItemWeight > 0f)
                rigidBodyMovement = CalculateClampedMovement(rigidBodyMovement, _heldItemWeight, Time.deltaTime);

            Vector3 headStart = headCollider.transform.TransformPoint(headCollider.center);
            float headRadius = headCollider.radius * Mathf.Max(
                headCollider.transform.lossyScale.x,
                headCollider.transform.lossyScale.y,
                headCollider.transform.lossyScale.z);

            Vector3 bodyTopStart = bodySphereTop.transform.TransformPoint(bodySphereTop.center);
            float bodyTopRadius = bodySphereTop.radius * Mathf.Max(
                bodySphereTop.transform.lossyScale.x,
                bodySphereTop.transform.lossyScale.y,
                bodySphereTop.transform.lossyScale.z);

            Vector3 bodyBottomStart = bodySphereBottom.transform.TransformPoint(bodySphereBottom.center);
            float bodyBottomRadius = bodySphereBottom.radius * Mathf.Max(
                bodySphereBottom.transform.lossyScale.x,
                bodySphereBottom.transform.lossyScale.y,
                bodySphereBottom.transform.lossyScale.z);

            Vector3 headFinal = headStart, topFinal = bodyTopStart, bottomFinal = bodyBottomStart;
            bool headHit = IterativeCollisionSphereCast(headStart, headRadius, rigidBodyMovement, defaultPrecision, out headFinal, false, out _);
            bool topHit = IterativeCollisionSphereCast(bodyTopStart, bodyTopRadius, rigidBodyMovement, defaultPrecision, out topFinal, false, out _);
            bool bottomHit = IterativeCollisionSphereCast(bodyBottomStart, bodyBottomRadius, rigidBodyMovement, defaultPrecision, out bottomFinal, false, out _);

            Vector3 moveHead = headHit ? (headFinal - headStart) : rigidBodyMovement;
            Vector3 moveTop = topHit ? (topFinal - bodyTopStart) : rigidBodyMovement;
            Vector3 moveBottom = bottomHit ? (bottomFinal - bodyBottomStart) : rigidBodyMovement;

            Vector3 allowedMove = moveHead;
            if (moveTop.magnitude < allowedMove.magnitude) allowedMove = moveTop;
            if (moveBottom.magnitude < allowedMove.magnitude) allowedMove = moveBottom;

            ApplyMovingPlatform(ref allowedMove);

            if (climbingMode == ClimbingMode.Kinematic)
            {
                if (allowedMove != Vector3.zero)
                {
                    _targetPosition = transform.position + allowedMove;
                    transform.position = Vector3.Lerp(transform.position, _targetPosition, 1f - kinematicSmoothing);
                }
            }
            else
            {
                if (allowedMove != Vector3.zero)
                {
                    transform.position += allowedMove;
                }
            }

            _lastHeadPosition = headCollider.transform.position;

            distanceTraveled = CurrentLeftHandPosition() - _lastLeftHandPosition;

            if (IterativeCollisionSphereCast(_lastLeftHandPosition, minimumRaycastDistance, distanceTraveled,
                    defaultPrecision, out finalPosition,
                    !((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching)), out _leftHandSurface))
            {
                _lastLeftHandPosition = finalPosition;
                leftHandColliding = true;
            }
            else
            {
                _lastLeftHandPosition = CurrentLeftHandPosition();
                _leftHandSurface = null;
            }

            distanceTraveled = CurrentRightHandPosition() - _lastRightHandPosition;

            if (IterativeCollisionSphereCast(_lastRightHandPosition, minimumRaycastDistance, distanceTraveled,
                    defaultPrecision, out finalPosition,
                    !((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching)), out _rightHandSurface))
            {
                _lastRightHandPosition = finalPosition;
                rightHandColliding = true;
            }
            else
            {
                _lastRightHandPosition = CurrentRightHandPosition();
                _rightHandSurface = null;
            }

            StoreVelocities();

            if ((rightHandColliding || leftHandColliding) && !disableMovement)
            {
                var jumpVel = _denormalizedVelocityAverage;
                if (_heldItemWeight > 1f)
                {
                    float weightFactor = Mathf.Clamp01(1f / _heldItemWeight);
                    jumpVel *= weightFactor;
                }
                if (jumpVel.magnitude > velocityLimit)
                {
                    if (jumpVel.magnitude * jumpMultiplier > maxJumpSpeed)
                        _playerRigidBody.velocity = jumpVel.normalized * maxJumpSpeed;
                    else
                        _playerRigidBody.velocity = jumpMultiplier * jumpVel;
                }
            }

            if (leftHandColliding && (CurrentLeftHandPosition() - _lastLeftHandPosition).magnitude > unStickDistance &&
                !Physics.SphereCast(headCollider.transform.position, minimumRaycastDistance * defaultPrecision,
                    CurrentLeftHandPosition() - headCollider.transform.position, out _,
                    (CurrentLeftHandPosition() - headCollider.transform.position).magnitude - minimumRaycastDistance,
                    locomotionEnabledLayers.value))
            {
                _lastLeftHandPosition = CurrentLeftHandPosition();
                leftHandColliding = false;
            }

            if (rightHandColliding &&
                (CurrentRightHandPosition() - _lastRightHandPosition).magnitude > unStickDistance &&
                !Physics.SphereCast(headCollider.transform.position, minimumRaycastDistance * defaultPrecision,
                    CurrentRightHandPosition() - headCollider.transform.position, out _,
                    (CurrentRightHandPosition() - headCollider.transform.position).magnitude - minimumRaycastDistance,
                    locomotionEnabledLayers.value))
            {
                _lastRightHandPosition = CurrentRightHandPosition();
                rightHandColliding = false;
            }

            leftHandFollower.position = _lastLeftHandPosition;
            rightHandFollower.position = _lastRightHandPosition;

            leftHandFollower.rotation = leftHandTransform.rotation;
            rightHandFollower.rotation = rightHandTransform.rotation;

            wasLeftHandTouching = leftHandColliding;
            wasRightHandTouching = rightHandColliding;
        }

        private void FixedUpdate()
        {
            if (climbingMode == ClimbingMode.Physics && _isClimbing)
            {
                ApplyPhysicsBasedClimbing();
            }

            DetectMovingPlatform();
        }

        private void DetectMovingPlatform()
        {
            _currentPlatform = null;

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f);

            foreach (Collider col in hitColliders)
            {
                int colLayer = col.gameObject.layer;

                if (colLayer == _bodyCollidersLayer || colLayer == _handsCollidersLayer)
                {
                    RaycastHit hit;
                    Vector3 direction = col.transform.position - transform.position;

                    if (Physics.Raycast(transform.position, direction.normalized, out hit, 2f))
                    {
                        Surface surface = hit.collider.GetComponent<Surface>();
                        if (surface != null && surface.IsMovable)
                        {
                            _currentPlatform = surface;

                            if (debugMovingPlatform)
                            {
                                Debug.Log($"[Player] Detected platform via {col.gameObject.name} (layer: {LayerMask.LayerToName(colLayer)}) -> Platform: {surface.gameObject.name}");
                            }

                            break;
                        }
                    }
                }
            }
        }

        private void ApplyPhysicsBasedClimbing()
        {
            Vector3 targetPos = _targetPosition;
            Vector3 displacement = targetPos - transform.position;

            if (displacement.magnitude > 0.01f)
            {
                Vector3 climbForce = displacement * climbStrength;
                _playerRigidBody.AddForce(climbForce, ForceMode.Acceleration);

                Vector3 dampingForce = -_playerRigidBody.velocity * climbDamping;
                _playerRigidBody.AddForce(dampingForce, ForceMode.Acceleration);
            }
        }

        #endregion

        #region Moving Platform

        private float GetAverageSlipSpeedMultiplier(bool leftHandTouching, bool rightHandTouching)
        {
            if (!leftHandTouching && !rightHandTouching) return 1f;

            float totalMultiplier = 0f;
            int touchCount = 0;

            if (leftHandTouching && _leftHandSurface != null)
            {
                totalMultiplier += _leftHandSurface.slipSpeedMultiplier;
                touchCount++;
            }

            if (rightHandTouching && _rightHandSurface != null)
            {
                totalMultiplier += _rightHandSurface.slipSpeedMultiplier;
                touchCount++;
            }

            return touchCount > 0 ? totalMultiplier / touchCount : 1f;
        }

        private void ApplyMovingPlatform(ref Vector3 rigidBodyMovement)
        {
            Surface activePlatform = null;

            if (_currentPlatform != null)
            {
                activePlatform = _currentPlatform;
            }
            else if (_leftHandSurface != null && _leftHandSurface.IsMovable)
            {
                activePlatform = _leftHandSurface;
            }
            else if (_rightHandSurface != null && _rightHandSurface.IsMovable)
            {
                activePlatform = _rightHandSurface;
            }

            if (activePlatform != null)
            {
                float surfaceSpeed = activePlatform.surfaceVelocity.magnitude;
                bool platformMoving = surfaceSpeed > movingThreshold;

                if (debugMovingPlatform)
                {
                    Debug.Log($"[Player] Active Platform: {activePlatform.gameObject.name}");
                    Debug.Log($"[Player] Platform velocity: {activePlatform.surfaceVelocity} | Speed: {surfaceSpeed:F3} | Moving: {platformMoving} | Threshold: {movingThreshold}");
                }

                if (platformMoving)
                {
                    Vector3 platformMovement = activePlatform.surfaceVelocity * Time.deltaTime;
                    rigidBodyMovement += platformMovement;

                    if (debugMovingPlatform)
                        Debug.Log($"[Player] Applied platform movement: {platformMovement} | Total movement: {rigidBodyMovement}");
                }
            }
            else if (debugMovingPlatform)
            {
                Debug.Log("[Player] No active platform detected");
            }
        }

        #endregion

        #region Collision Detection

        private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector,
            float precision, out Vector3 endPosition, bool singleHand, out Surface hitSurface)
        {
            hitSurface = null;
            Surface gorillaSurface;
            float slipPercentage;

            if (CollisionsSphereCast(startPosition, sphereRadius * precision, movementVector, precision,
                    out endPosition, out var hitInfo))
            {
                var firstPosition = endPosition;
                gorillaSurface = hitInfo.collider.GetComponent<Surface>();

                if (gorillaSurface != null)
                {
                    _currentSurface = gorillaSurface;
                    if (!gorillaSurface.IsSlippy)
                    {
                        gorillaSurface = null;
                    }
                }
                else
                {
                    _currentSurface = null;
                }

                hitSurface = gorillaSurface;
                slipPercentage = gorillaSurface != null ? gorillaSurface.slipPercentage : (!singleHand ? defaultSlideFactor : 0f);

                var remainingMovement = startPosition + movementVector - firstPosition;
                var slideMovement = Vector3.ProjectOnPlane(remainingMovement, hitInfo.normal) * slipPercentage;

                if (CollisionsSphereCast(endPosition, sphereRadius, slideMovement,
                        precision * precision, out var slideEndPosition, out var hitInfo2))
                {
                    endPosition = slideEndPosition;
                    return true;
                }

                if (CollisionsSphereCast(slideMovement + firstPosition, sphereRadius,
                        startPosition + movementVector - (slideMovement + firstPosition),
                        precision * precision * precision, out var finalSlidePosition, out var hitInfo3))
                {
                    if (gorillaSurface != null && gorillaSurface.IsSlippy)
                    {
                        if (slipPercentage > slipThreshold)
                        {
                            handSliding = true;
                            if (handSliding)
                            {
                                wasLeftHandTouching = false;
                                wasRightHandTouching = false;
                                if (!wasLeftHandTouching && !wasRightHandTouching)
                                {
                                    singleHand = false;
                                    defaultSlideFactor = 1f;
                                    currentFactor = defaultSlideFactor;
                                    startPosition = Vector3.Lerp(firstPosition, finalSlidePosition, currentFactor);
                                }
                            }
                        }
                        else if (slipPercentage < slipThreshold)
                        {
                            handSlipping = true;
                            if (handSlipping)
                            {
                                wasLeftHandTouching = false;
                                wasRightHandTouching = false;
                                if (!wasLeftHandTouching && !wasRightHandTouching)
                                {
                                    singleHand = false;
                                    defaultSlideFactor = slipPercentage;
                                    currentFactor = defaultSlideFactor;
                                    startPosition = Vector3.Lerp(firstPosition, finalSlidePosition, currentFactor);
                                }
                            }
                        }
                    }
                    else if (gorillaSurface == null)
                    {
                        defaultSlideFactor = 0f;
                        currentFactor = defaultSlideFactor;
                        singleHand = true;
                        handSliding = false;
                        handSlipping = false;
                    }

                    endPosition = finalSlidePosition;
                    return true;
                }

                endPosition = firstPosition + slideMovement;
                return true;
            }

            if (CollisionsSphereCast(startPosition, sphereRadius * precision * 0.66f,
                    movementVector.normalized * (movementVector.magnitude + sphereRadius * precision * 0.34f),
                    precision * 0.66f, out endPosition, out var hitInfo4))
            {
                endPosition = startPosition;
                return true;
            }

            endPosition = Vector3.zero;
            return false;
        }

        private bool CollisionsSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector,
            float precision, out Vector3 finalPosition, out RaycastHit hitInfo)
        {
            if (Physics.SphereCast(startPosition, sphereRadius * precision, movementVector, out hitInfo,
                    movementVector.magnitude + sphereRadius * (1 - precision), locomotionEnabledLayers.value))
            {
                finalPosition = hitInfo.point + hitInfo.normal * sphereRadius;

                if (Physics.SphereCast(startPosition, sphereRadius * precision * precision,
                        finalPosition - startPosition, out var innerHit,
                        (finalPosition - startPosition).magnitude + sphereRadius * (1 - precision * precision),
                        locomotionEnabledLayers.value))
                {
                    finalPosition = startPosition + (finalPosition - startPosition).normalized *
                        Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision));
                    hitInfo = innerHit;
                }
                else if (Physics.Raycast(startPosition, finalPosition - startPosition, out innerHit,
                             (finalPosition - startPosition).magnitude + sphereRadius * precision * precision * 0.999f,
                             locomotionEnabledLayers.value))
                {
                    finalPosition = startPosition;
                    hitInfo = innerHit;
                    return true;
                }

                return true;
            }

            if (Physics.Raycast(startPosition, movementVector, out hitInfo,
                    movementVector.magnitude + sphereRadius * precision * 0.999f, locomotionEnabledLayers.value))
            {
                finalPosition = startPosition;
                return true;
            }

            finalPosition = Vector3.zero;
            return false;
        }

        #endregion

        #region Utility Methods

        public bool IsHandTouching(bool forLeftHand)
        {
            return forLeftHand ? wasLeftHandTouching : wasRightHandTouching;
        }

        public void Turn(float degrees)
        {
            transform.RotateAround(headCollider.transform.position, transform.up, degrees);
            _denormalizedVelocityAverage = Quaternion.Euler(0, degrees, 0) * _denormalizedVelocityAverage;

            for (var i = 0; i < _velocityHistory.Length; i++)
                _velocityHistory[i] = Quaternion.Euler(0, degrees, 0) * _velocityHistory[i];
        }

        private void StoreVelocities()
        {
            _velocityIndex = (_velocityIndex + 1) % velocityHistorySize;
            var oldestVelocity = _velocityHistory[_velocityIndex];
            _currentVelocity = (transform.position - _lastPosition) / Time.deltaTime;
            _denormalizedVelocityAverage += (_currentVelocity - oldestVelocity) / velocityHistorySize;
            _velocityHistory[_velocityIndex] = _currentVelocity;
            _lastPosition = transform.position;
        }

        public void HapticImpulse(bool isLeft, float hapticstrength, float hapticduration)
        {
            GetDeviceFromXRNode(isLeft ? XRNode.LeftHand : XRNode.RightHand, out var inputDevice);
            inputDevice.SendHapticImpulse(0, hapticstrength, hapticduration);
        }

        public void GetDeviceFromXRNode(XRNode xrNode, out InputDevice inputDevice)
        {
            inputDevice = InputDevices.GetDeviceAtXRNode(xrNode);
        }

        #endregion
    }
}