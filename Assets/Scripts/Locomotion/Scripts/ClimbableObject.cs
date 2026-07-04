using UnityEngine;

namespace Locomotion.Scripts
{
    public class ClimbableObject : MonoBehaviour, IGrabbable
    {
        [SerializeField] private float weight = 1f;
        public float Weight => weight;

        private int grabCount = 0;

        public void OnGrab()
        {
            grabCount++;
            Player.Instance.SetHeldItemWeight(weight);
            Debug.Log("Grabbed a climbable obj");
        }

        public void OnDrop()
        {
            grabCount = Mathf.Max(0, grabCount - 1);
            if (grabCount == 0)
                Player.Instance.ClearHeldItemWeight();
            Debug.Log("Released a climbable obj");
        }
    }
}