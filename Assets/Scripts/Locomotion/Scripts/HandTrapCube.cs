using UnityEngine;

namespace Locomotion.Scripts
{
    public class HandTrapCube : MonoBehaviour
    {
        [Header("Visual Settings")]
        public bool showGizmo = true;
        public Color gizmoColor = new Color(0f, 1f, 1f, 0.5f);
        public float cubeSize = 0.15f;
        
        [Header("Debug")]
        public bool logLifecycle = true;

        private void Start()
        {
            if (logLifecycle)
            {
                Debug.Log($"[HandTrapCube] ✓✓✓ SPAWNED at {transform.position}");
                Debug.Log($"[HandTrapCube] Parent: {transform.parent?.name ?? "None"}");
                Debug.Log($"[HandTrapCube] Active: {gameObject.activeSelf}");
            }
        }

        private void OnDestroy()
        {
            if (logLifecycle)
                Debug.Log("[HandTrapCube] ✗✗✗ DESTROYED");
        }

        private void OnDrawGizmos()
        {
            if (!showGizmo) return;

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(transform.position, Vector3.one * cubeSize);
            Gizmos.DrawSphere(transform.position, cubeSize * 0.1f);
        }
    }
}
