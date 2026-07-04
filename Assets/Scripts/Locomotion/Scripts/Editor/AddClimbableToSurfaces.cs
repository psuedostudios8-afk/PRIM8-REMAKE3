using UnityEngine;
using UnityEditor;
using Locomotion.Scripts;

namespace Locomotion.Scripts.Editor
{
    public class AddClimbableToSurfaces : EditorWindow
    {
        [MenuItem("Tools/Climbing/Add ClimbableObject to All Surfaces")]
        public static void AddToAllSurfaces()
        {
            Surface[] surfaces = FindObjectsByType<Surface>(FindObjectsSortMode.None);
            int added = 0;
            int skipped = 0;

            foreach (Surface surface in surfaces)
            {
                if (surface.GetComponent<ClimbableObject>() == null)
                {
                    Undo.AddComponent<ClimbableObject>(surface.gameObject);
                    Debug.Log($"✓ Added ClimbableObject to: {surface.gameObject.name}");
                    added++;
                }
                else
                {
                    Debug.Log($"⊗ Skipped (already has ClimbableObject): {surface.gameObject.name}");
                    skipped++;
                }
            }

            Debug.Log($"=== COMPLETE ===\nAdded: {added}\nSkipped: {skipped}\nTotal: {surfaces.Length}");
            
            if (added > 0)
            {
                EditorUtility.DisplayDialog(
                    "ClimbableObject Added!", 
                    $"Added ClimbableObject to {added} surface(s)!\n\nYour surfaces are now climbable with the trap cube method.", 
                    "Awesome!"
                );
            }
        }

        [MenuItem("Tools/Climbing/List All Climbable Objects")]
        public static void ListAllClimbable()
        {
            ClimbableObject[] climbables = FindObjectsByType<ClimbableObject>(FindObjectsSortMode.None);
            
            Debug.Log($"=== CLIMBABLE OBJECTS ({climbables.Length}) ===");
            
            foreach (var climbable in climbables)
            {
                string layerName = LayerMask.LayerToName(climbable.gameObject.layer);
                Debug.Log($"✓ {climbable.gameObject.name} (Layer: {layerName})");
            }
            
            if (climbables.Length == 0)
            {
                Debug.LogWarning("No ClimbableObject components found in scene!");
            }
        }
    }
}
