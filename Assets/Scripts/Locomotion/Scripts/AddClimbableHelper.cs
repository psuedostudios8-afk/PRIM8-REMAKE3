using UnityEngine;

namespace Locomotion.Scripts
{
    public class AddClimbableHelper : MonoBehaviour
    {
        [ContextMenu("Add ClimbableObject to All Surfaces in Scene")]
        void AddClimbableToSurfaces()
        {
            Surface[] surfaces = FindObjectsByType<Surface>(FindObjectsSortMode.None);
            int added = 0;

            foreach (Surface surface in surfaces)
            {
                if (surface.GetComponent<ClimbableObject>() == null)
                {
                    surface.gameObject.AddComponent<ClimbableObject>();
                    Debug.Log($"✓ Added ClimbableObject to: {surface.gameObject.name}");
                    added++;
                }
                else
                {
                    Debug.Log($"⊗ Already has ClimbableObject: {surface.gameObject.name}");
                }
            }

            Debug.Log($"=== COMPLETE === Added ClimbableObject to {added} surfaces!");
        }

        [ContextMenu("List All ClimbableObjects in Scene")]
        void ListClimbableObjects()
        {
            ClimbableObject[] climbables = FindObjectsByType<ClimbableObject>(FindObjectsSortMode.None);
            
            Debug.Log($"=== CLIMBABLE OBJECTS ({climbables.Length}) ===");
            
            if (climbables.Length == 0)
            {
                Debug.LogWarning("❌ NO ClimbableObject components found! Add them to your surfaces!");
            }
            else
            {
                foreach (var climbable in climbables)
                {
                    string layerName = LayerMask.LayerToName(climbable.gameObject.layer);
                    Debug.Log($"✓ {climbable.gameObject.name} (Layer: {layerName})");
                }
            }
        }

        [ContextMenu("Enable Debug on Both Hands")]
        void EnableDebugOnHands()
        {
            GrabInteractor[] hands = FindObjectsByType<GrabInteractor>(FindObjectsSortMode.None);
            
            foreach (var hand in hands)
            {
                hand.debugClimbing = true;
                Debug.Log($"✓ Enabled debugClimbing on {hand.node}");
            }
            
            Debug.Log($"=== Enabled debug on {hands.Length} hands ===");
        }
    }
}
