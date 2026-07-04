using UnityEngine;
using UnityEngine.XR;

namespace Locomotion.Scripts
{
    public class GrabInteractor : MonoBehaviour
    {
        public bool testGrab;
        public LayerMask interactableLayer;
        public Transform palm;
        public float sphereRadius = 0.05f;
        public Rigidbody physicHand;
        public Collider handCollider;

        public XRNode node = XRNode.LeftHand;
        private InputDevice _inputDevice;

        [Header("Joint Settings")]
        public float breakForce = 5000f;
        public float climbForceMultiplier = 1000f;

        [Header("Debug")]
        public bool debugClimbing = false;

        public bool isGrabbing;
        private FixedJoint _grabbableJoint;
        private Transform _grabbableTransform;
        private Rigidbody _grabbableRigidbody;
        private IGrabbable _grabbableInterface;
        private bool _isClimbable;

        private GameObject _trapCubeInstance;
        private Vector3 _climbGrabPoint;

        private void TryGrab()
        {
            if (isGrabbing) return;

            Collider[] hitColliders = Physics.OverlapSphere(palm.position, sphereRadius, interactableLayer);
            if (hitColliders.Length == 0) return;

            var target = hitColliders[0];
            if (!target.transform) return;

            isGrabbing = true;
            Grab(target);
        }

        private void Grab(Collider collider)
        {
            _grabbableTransform = collider.transform;
            collider.TryGetComponent(out _grabbableInterface);
            collider.TryGetComponent(out _grabbableRigidbody);

            _isClimbable = collider.GetComponent<ClimbableObject>() != null;

            if (debugClimbing)
                Debug.Log($"[GrabInteractor {node}] Grabbed: {collider.name}, IsClimbable: {_isClimbable}");

            _grabbableInterface?.OnGrab();

            if (!_isClimbable)
            {
                if (_grabbableJoint)
                    Destroy(_grabbableJoint);

                _grabbableJoint = physicHand.gameObject.AddComponent<FixedJoint>();
                _grabbableJoint.breakForce = breakForce;

                if (_grabbableRigidbody)
                {
                    _grabbableJoint.connectedBody = _grabbableRigidbody;
                    _grabbableJoint.autoConfigureConnectedAnchor = true;
                }
                else
                {
                    _grabbableJoint.connectedBody = null;
                    _grabbableJoint.autoConfigureConnectedAnchor = false;
                    _grabbableJoint.anchor = transform.InverseTransformPoint(collider.transform.position);
                    _grabbableJoint.connectedAnchor = collider.transform.position;
                }

                if (handCollider)
                    handCollider.enabled = false;
            }
            else
            {
                ClimbGrabMethod method = Player.Instance.climbGrabMethod;

                if (debugClimbing)
                    Debug.Log($"[GrabInteractor {node}] Climb Grab Method: {method}");

                if (method == ClimbGrabMethod.TrapCube)
                {
                    SpawnTrapCube();
                }
                else
                {
                    _climbGrabPoint = palm.position;
                    if (debugClimbing)
                        Debug.Log($"[GrabInteractor {node}] Using ForceBasedPush mode");
                }
            }
        }

        private void SpawnTrapCube()
        {
            if (Player.Instance == null)
            {
                Debug.LogError("[GrabInteractor] Player.Instance is NULL!");
                return;
            }

            if (Player.Instance.handTrapCubePrefab == null)
            {
                Debug.LogWarning($"[GrabInteractor {node}] Hand Trap Cube Prefab not assigned in Player! Falling back to ForceBasedPush.");
                _climbGrabPoint = palm.position;
                return;
            }

            if (_trapCubeInstance != null)
            {
                if (debugClimbing)
                    Debug.Log($"[GrabInteractor {node}] Destroying old trap cube before spawning new one");
                Destroy(_trapCubeInstance);
                _trapCubeInstance = null;
            }

            Vector3 spawnPosition = transform.position;
            _trapCubeInstance = Instantiate(Player.Instance.handTrapCubePrefab, spawnPosition, Quaternion.identity);

            Debug.Log($"[GrabInteractor {node}] ✓ Spawned Trap Cube at WORLD position {spawnPosition}");

            if (debugClimbing)
            {
                Debug.Log($"[GrabInteractor {node}] Trap Cube Details:");
                Debug.Log($"  - World Position: {_trapCubeInstance.transform.position}");
                Debug.Log($"  - Parent: {(_trapCubeInstance.transform.parent == null ? "NONE (world space)" : _trapCubeInstance.transform.parent.name)}");
                Debug.Log($"  - Active: {_trapCubeInstance.activeSelf}");
            }
        }

        private void Drop()
        {
            if (!isGrabbing) return;
            isGrabbing = false;

            if (debugClimbing)
                Debug.Log($"[GrabInteractor {node}] Dropping grab");

            _grabbableInterface?.OnDrop();

            _grabbableTransform = null;
            _grabbableInterface = null;
            _isClimbable = false;

            if (_grabbableJoint)
                Destroy(_grabbableJoint);

            if (_trapCubeInstance)
            {
                if (debugClimbing)
                    Debug.Log($"[GrabInteractor {node}] Destroying trap cube");
                Destroy(_trapCubeInstance);
                _trapCubeInstance = null;
            }

            if (handCollider)
                handCollider.enabled = true;
        }

        private void FixedUpdate()
        {
            if (isGrabbing && _isClimbable)
            {
                if (Player.Instance.climbGrabMethod == ClimbGrabMethod.ForceBasedPush)
                {
                    ApplyClimbForce();
                }
            }
        }

        private void ApplyClimbForce()
        {
            Vector3 handVelocity = physicHand.velocity;
            Vector3 climbForce = -handVelocity * climbForceMultiplier * Time.fixedDeltaTime;

            Rigidbody playerRb = Player.Instance.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                playerRb.AddForce(climbForce, ForceMode.Force);
            }
        }

        public void Update()
        {
            if (!_inputDevice.isValid)
                _inputDevice = InputDevices.GetDeviceAtXRNode(node);
            else
                testGrab = _inputDevice.TryGetFeatureValue(CommonUsages.grip, out var gripValue) && gripValue > 0.7f;

            if (testGrab)
                TryGrab();

            if (isGrabbing)
            {
                if (!testGrab)
                    Drop();

                if (_grabbableRigidbody)
                {
                    var velocity = _grabbableRigidbody.velocity.magnitude;
                    var dragStrength = Mathf.InverseLerp(3f, 10f, velocity);
                    var hapticStrength = Mathf.Lerp(0f, 1f, dragStrength);

                    Player.Instance.HapticImpulse(
                        isLeft: node == XRNode.LeftHand,
                        hapticstrength: hapticStrength,
                        hapticduration: 0.05f
                    );
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!palm) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(palm.position, sphereRadius);
        }
    }
}