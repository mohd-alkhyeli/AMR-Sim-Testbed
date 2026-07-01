using UnityEngine;

public class WheelAligner : MonoBehaviour
{
    [Tooltip("The Wheel Collider to align with.")]
    public WheelCollider wheelCollider;

    [Tooltip("Assign an offset if your wheel mesh is not perfectly centered.")]
    public Vector3 meshOffset = Vector3.zero;

    private void FixedUpdate()
    {
        if (wheelCollider == null) return;

        // Get the world position and rotation of the wheel collider
        wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion rot);

        // Apply the pose and the offset to the visual wheel mesh
        transform.position = pos + transform.TransformDirection(meshOffset);

        // FIX: Create a 90-degree rotation offset on the Z-axis to stand the cylinder upright
        Quaternion cylinderCorrection = Quaternion.Euler(0, 0, 90);

        // Multiply the rotations (Collider rotation applied first, then the correction)
        transform.rotation = rot * cylinderCorrection;
    }
}