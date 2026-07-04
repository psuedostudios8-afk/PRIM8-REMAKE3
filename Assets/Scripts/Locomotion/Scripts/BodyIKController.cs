using System.Collections.Generic;
using UnityEngine;

public class BodyIKController : MonoBehaviour
{
    public Renderer[] visualComponents;

    public Transform spineTransform;
    public Transform bodyTransform;
    public Transform neckTransform;
    public Transform viewTransform;
    public Transform leftLimb;
    public Transform rightLimb;

    public float interpolationSpeed = 5f;
    [Range(0f, 1f)] public float limbInfluenceWeight = 0.3f;
    [Range(0f, 1f)] public float pitchInfluenceWeight = 0.5f;

    public bool enableBodyIK = true;
    public float yawOffset = 0f;
    public Vector3 viewOffsetRotation;
    public bool invertPitch = true;

    public float offsetX = 0f;
    public float offsetY = 0f;
    public float offsetZ = 0f;

    public float limbAngleRange = 90f;

    public Color arcColor = Color.red;
    public float arcRadius = 5f;
    public int arcSegments = 20;

    public bool isLeftLimbActive = false;
    public bool isRightLimbActive = false;

    private List<Transform> activeLimbs = new List<Transform>();

    void Update()
    {
        if (!enableBodyIK) return;

        Quaternion updatedNeckRotation = viewTransform.rotation * Quaternion.Euler(viewOffsetRotation);
        neckTransform.rotation = updatedNeckRotation;

        activeLimbs.Clear();

        Vector3 viewPos = viewTransform.position;
        Vector3 viewForward = viewTransform.forward;
        Vector3 flatForward = new Vector3(viewForward.x, 0f, viewForward.z).normalized;

        isLeftLimbActive = IsLimbInFront(leftLimb, viewPos, flatForward);
        if (isLeftLimbActive) activeLimbs.Add(leftLimb);

        isRightLimbActive = IsLimbInFront(rightLimb, viewPos, flatForward);
        if (isRightLimbActive) activeLimbs.Add(rightLimb);

        float totalYawInfluence = 0f;
        foreach (var limb in activeLimbs)
        {
            Vector3 toLimbFlat = (limb.position - viewPos);
            toLimbFlat.y = 0f;
            float limbYaw = Vector3.SignedAngle(flatForward, toLimbFlat.normalized, Vector3.up);
            totalYawInfluence += limbYaw;
        }

        if (activeLimbs.Count > 0)
            totalYawInfluence /= activeLimbs.Count;

        float playerBodyYaw = NormalizeAngle(transform.eulerAngles.y);
        Vector3 flatViewForward = new Vector3(viewTransform.forward.x, 0f, viewTransform.forward.z).normalized;
        float viewYawRelativeToBody = Vector3.SignedAngle(transform.forward, flatViewForward, Vector3.up);
        float viewYaw = playerBodyYaw + viewYawRelativeToBody + yawOffset;
        
        float finalYaw = Mathf.LerpAngle(viewYaw, viewYaw + totalYawInfluence, limbInfluenceWeight);

        Quaternion targetSpineRotation = Quaternion.Euler(0f, finalYaw, 0f);
        spineTransform.rotation = Quaternion.Slerp(spineTransform.rotation, targetSpineRotation, Time.deltaTime * interpolationSpeed);

        float pitch = viewTransform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        float desiredPitch = (pitch > 0f) ? (invertPitch ? -pitch : pitch) * pitchInfluenceWeight : 0f;

        Vector3 spineEuler = spineTransform.eulerAngles;
        spineEuler.x = Mathf.LerpAngle(NormalizeAngle(spineEuler.x), desiredPitch, Time.deltaTime * interpolationSpeed);
        spineEuler.z = 0f;

        spineTransform.rotation = Quaternion.Euler(spineEuler);
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return (angle > 180f) ? angle - 360f : (angle < -180f) ? angle + 360f : angle;
    }

    private bool IsLimbInFront(Transform limb, Vector3 viewPos, Vector3 flatForward)
    {
        if (limb == null) return false;

        Vector3 toLimbFlat = limb.position - viewPos;
        toLimbFlat.y = 0f;
        float angle = Vector3.SignedAngle(flatForward, toLimbFlat.normalized, Vector3.up);
        return Mathf.Abs(angle) <= limbAngleRange;
    }

    private void OnDrawGizmosSelected()
    {
        if (viewTransform == null) return;

        Vector3 flatForward = new Vector3(viewTransform.forward.x, 0f, viewTransform.forward.z).normalized;

        Quaternion leftLimit = Quaternion.Euler(0f, -limbAngleRange, 0f);
        Quaternion rightLimit = Quaternion.Euler(0f, limbAngleRange, 0f);

        Vector3 leftBound = leftLimit * flatForward;
        Vector3 rightBound = rightLimit * flatForward;

        Gizmos.color = arcColor;
        Gizmos.DrawLine(viewTransform.position, viewTransform.position + leftBound * arcRadius);
        Gizmos.DrawLine(viewTransform.position, viewTransform.position + rightBound * arcRadius);

        Gizmos.color = new Color(arcColor.r, arcColor.g, arcColor.b, 0.2f);
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            Quaternion step = Quaternion.Euler(0f, Mathf.Lerp(-limbAngleRange, limbAngleRange, t), 0f);
            Vector3 dir = step * flatForward;
            Gizmos.DrawLine(viewTransform.position, viewTransform.position + dir * arcRadius);
        }

        Gizmos.color = Color.green;
        if (isLeftLimbActive && leftLimb != null) Gizmos.DrawLine(viewTransform.position, leftLimb.position);
        if (isRightLimbActive && rightLimb != null) Gizmos.DrawLine(viewTransform.position, rightLimb.position);
    }
}