namespace Locomotion.Scripts
{
    public interface IGrabbable
    {
        float Weight { get; }
        void OnGrab();
        void OnDrop();
    }
}