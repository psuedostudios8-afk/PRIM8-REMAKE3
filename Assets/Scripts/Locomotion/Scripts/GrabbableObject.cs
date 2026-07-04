using UnityEngine;

namespace Locomotion.Scripts
{
    public class GrabbableObject : MonoBehaviour, IGrabbable
    {
        [SerializeField] private float weight = 1f;
        public float Weight => weight;

        private int grabCount = 0;

        public void OnGrab()
        {
            grabCount++;
            if (grabCount == 1 && Player.Instance != null)
            {
                Player.Instance.SetHeldItemWeight(weight);
                Debug.Log("Grabbed ze object once");
            }
            else
            {
                Debug.Log("Grabbed ze object twice");
            }
        }

        public void OnDrop()
        {
            grabCount = Mathf.Max(0, grabCount - 1);
            if (grabCount == 0 && Player.Instance != null)
            {
                Player.Instance.ClearHeldItemWeight();
                Debug.Log("released ze object once");
            }
            else
            {
                Debug.Log("released ze object twice");
            }
        }
    }
}
