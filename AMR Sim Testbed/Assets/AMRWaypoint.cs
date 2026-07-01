using UnityEngine;

public class AMRWaypoint : MonoBehaviour
{
    [Tooltip("The transform the AMR should drive toward. If empty, this waypoint object's own transform is used.")]
    public Transform targetTransform;

    public Transform Target => targetTransform != null ? targetTransform : transform;
}