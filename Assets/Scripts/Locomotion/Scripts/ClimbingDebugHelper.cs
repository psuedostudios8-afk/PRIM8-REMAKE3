using UnityEngine;

namespace Locomotion.Scripts
{
    public class ClimbingDebugHelper : MonoBehaviour
    {
        [Header("Check Player Settings")]
        [TextArea(3, 10)]
        public string debugInfo = "Press Play to check climbing configuration...";

        private void Start()
        {
            CheckConfiguration();
        }

        [ContextMenu("Check Configuration")]
        public void CheckConfiguration()
        {
            Debug.Log("=== CLIMBING CONFIGURATION CHECK ===");

            if (Player.Instance == null)
            {
                Debug.LogError("❌ Player.Instance is NULL! Make sure Player script is in scene.");
                debugInfo = "ERROR: No Player instance found!";
                return;
            }

            Debug.Log($"✓ Player Instance: {Player.Instance.name}");
            Debug.Log($"✓ Climbing Mode: {Player.Instance.climbingMode}");
            Debug.Log($"✓ Climb Grab Method: {Player.Instance.climbGrabMethod}");

            if (Player.Instance.handTrapCubePrefab == null)
            {
                Debug.LogWarning("⚠ Hand Trap Cube Prefab is NOT assigned in Player!");
                Debug.LogWarning("  → Assign your trap cube prefab in Player component");
                debugInfo = "WARNING: Trap cube prefab not assigned!";
            }
            else
            {
                Debug.Log($"✓ Hand Trap Cube Prefab: {Player.Instance.handTrapCubePrefab.name}");
                debugInfo = $"Configuration OK!\nMethod: {Player.Instance.climbGrabMethod}\nPrefab: {Player.Instance.handTrapCubePrefab.name}";
            }

            GrabInteractor[] grabbers = FindObjectsByType<GrabInteractor>(FindObjectsSortMode.None);
            Debug.Log($"✓ Found {grabbers.Length} GrabInteractor(s)");

            foreach (var grabber in grabbers)
            {
                Debug.Log($"  - {grabber.name} (Node: {grabber.node})");
            }

            Debug.Log("=== END CONFIGURATION CHECK ===");
        }

        private void OnGUI()
        {
            if (Player.Instance != null && Player.Instance.climbGrabMethod == ClimbGrabMethod.TrapCube)
            {
                GUILayout.BeginArea(new Rect(10, 10, 400, 150));
                GUILayout.Box("TRAP CUBE CLIMBING ACTIVE");
                GUILayout.Label($"Prefab: {(Player.Instance.handTrapCubePrefab != null ? Player.Instance.handTrapCubePrefab.name : "NOT ASSIGNED!")}");
                
                GrabInteractor[] grabbers = FindObjectsByType<GrabInteractor>(FindObjectsSortMode.None);
                foreach (var grabber in grabbers)
                {
                    GUILayout.Label($"{grabber.node}: Grabbing={grabber.isGrabbing}");
                }
                
                GUILayout.EndArea();
            }
        }
    }
}
