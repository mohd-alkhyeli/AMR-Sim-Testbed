using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum AMRSensorZone
{
    Front,
    FrontLeft,
    FrontRight,
    Left,
    Right,
    Rear,
    RearLeft,
    RearRight,
    Custom
}

[System.Serializable]
public struct AMRSensorReading
{
    public AMRSensorZone zone;

    public bool hasHit;
    public bool warning;
    public bool blocked;

    public float closestDistance;
    public float clearFraction;
    public float danger01;

    public Vector3 closestPoint;
    public Vector3 closestNormal;

    public Collider closestCollider;
}

[DisallowMultipleComponent]
public class AMRSensor : MonoBehaviour
{
    [Header("Sensor Identity")]
    public AMRSensorZone zone = AMRSensorZone.Front;

    [Tooltip("Root object of the robot. Colliders under this root will be ignored.")]
    public Transform rootToIgnore;

    [Header("Detection")]
    [Tooltip("Layers this sensor can detect. Usually Obstacles, Walls, Robots, DynamicObstacles. Exclude Ground and your own Robot layer.")]
    public LayerMask obstacleLayers = ~0;

    [Tooltip("Maximum detection distance.")]
    public float range = 2f;

    [Tooltip("Sphere cast radius. Higher = wider safety margin. Set to 0 for thin raycasts.")]
    public float sphereRadius = 0.08f;

    [Range(0f, 180f)]
    [Tooltip("Horizontal field of view in degrees.")]
    public float horizontalFOV = 30f;

    [Range(1, 31)]
    [Tooltip("Number of rays inside the field of view. Higher = better coverage but more physics checks.")]
    public int rayCount = 7;

    [Tooltip("Ignore trigger colliders unless you specifically use triggers as obstacles.")]
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("State Thresholds")]
    [Tooltip("Distance where this sensor starts reporting warning = true.")]
    public float warningDistance = 1.0f;

    [Tooltip("Distance where this sensor reports blocked = true.")]
    public float stopDistance = 0.35f;

    [Header("Behavior")]
    [Tooltip("Automatically scan in FixedUpdate. Leave enabled for autonomous driving.")]
    public bool scanInFixedUpdate = true;

    [Tooltip("Keep rays horizontal on the XZ plane. Good for AMR sensors.")]
    public bool keepRaysHorizontal = true;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public bool drawLabels = true;

    [Tooltip("Run scans in edit mode so gizmos show detection before pressing Play.")]
    public bool scanInEditMode = true;

    [Tooltip("Maximum hits checked per ray. Increase only if many colliders overlap.")]
    public int maxHitsPerRay = 16;

    private RaycastHit[] hitBuffer;

    public AMRSensorReading Reading { get; private set; }

    public bool HasObstacle => Reading.hasHit;
    public bool IsWarning => Reading.warning;
    public bool IsBlocked => Reading.blocked;
    public float ClearDistance => Reading.hasHit ? Reading.closestDistance : range;
    public float Danger01 => Reading.danger01;

    private void Awake()
    {
        AutoAssignIgnoreRoot();
        EnsureBuffer();
    }

    private void OnValidate()
    {
        range = Mathf.Max(0.01f, range);
        sphereRadius = Mathf.Max(0f, sphereRadius);
        rayCount = Mathf.Clamp(rayCount, 1, 31);
        warningDistance = Mathf.Clamp(warningDistance, 0f, range);
        stopDistance = Mathf.Clamp(stopDistance, 0f, warningDistance);
        maxHitsPerRay = Mathf.Max(1, maxHitsPerRay);
    }

    private void FixedUpdate()
    {
        if (scanInFixedUpdate)
        {
            Scan();
        }
    }

    public AMRSensorReading Scan()
    {
        EnsureBuffer();

        Vector3 origin = transform.position;

        bool foundHit = false;
        RaycastHit closestHit = default;
        float closestDistance = range;

        int rays = Mathf.Max(1, rayCount);

        for (int i = 0; i < rays; i++)
        {
            float t = rays == 1 ? 0.5f : i / (float)(rays - 1);
            float angle = Mathf.Lerp(-horizontalFOV * 0.5f, horizontalFOV * 0.5f, t);

            Vector3 direction = GetRayDirection(angle);

            if (TryCast(origin, direction, out RaycastHit hit))
            {
                if (hit.distance < closestDistance)
                {
                    foundHit = true;
                    closestHit = hit;
                    closestDistance = hit.distance;
                }
            }
        }

        AMRSensorReading reading = new AMRSensorReading();
        reading.zone = zone;
        reading.hasHit = foundHit;

        if (foundHit)
        {
            reading.closestDistance = closestDistance;
            reading.closestPoint = closestHit.point;
            reading.closestNormal = closestHit.normal;
            reading.closestCollider = closestHit.collider;

            reading.warning = closestDistance <= warningDistance;
            reading.blocked = closestDistance <= stopDistance;

            reading.clearFraction = Mathf.Clamp01(closestDistance / range);

            // 0 = safe/far, 1 = danger/inside stop distance
            reading.danger01 = Mathf.Clamp01(
                Mathf.InverseLerp(warningDistance, stopDistance, closestDistance)
            );
        }
        else
        {
            reading.closestDistance = range;
            reading.closestPoint = origin + transform.forward * range;
            reading.closestNormal = Vector3.zero;
            reading.closestCollider = null;

            reading.warning = false;
            reading.blocked = false;
            reading.clearFraction = 1f;
            reading.danger01 = 0f;
        }

        Reading = reading;
        return Reading;
    }

    private bool TryCast(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
    {
        closestHit = default;

        int hitCount;

        if (sphereRadius > 0.0001f)
        {
            hitCount = Physics.SphereCastNonAlloc(
                origin,
                sphereRadius,
                direction,
                hitBuffer,
                range,
                obstacleLayers,
                triggerInteraction
            );
        }
        else
        {
            hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                hitBuffer,
                range,
                obstacleLayers,
                triggerInteraction
            );
        }

        bool foundValidHit = false;
        float closestDistance = range;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];

            if (hit.collider == null)
                continue;

            if (ShouldIgnore(hit.collider))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundValidHit = true;
            }
        }

        return foundValidHit;
    }

    private bool ShouldIgnore(Collider collider)
    {
        Transform ignoreRoot = rootToIgnore;

        if (ignoreRoot == null)
        {
            Rigidbody parentRb = GetComponentInParent<Rigidbody>();

            if (parentRb != null)
                ignoreRoot = parentRb.transform;
        }

        if (ignoreRoot == null)
            return false;

        return collider.transform.IsChildOf(ignoreRoot);
    }

    private Vector3 GetRayDirection(float yawAngle)
    {
        Vector3 baseForward = transform.forward;
        Vector3 yawAxis = transform.up;

        if (keepRaysHorizontal)
        {
            baseForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            if (baseForward.sqrMagnitude < 0.0001f)
                baseForward = Vector3.forward;

            baseForward.Normalize();
            yawAxis = Vector3.up;
        }

        return Quaternion.AngleAxis(yawAngle, yawAxis) * baseForward;
    }

    private void AutoAssignIgnoreRoot()
    {
        if (rootToIgnore != null)
            return;

        Rigidbody parentRb = GetComponentInParent<Rigidbody>();

        if (parentRb != null)
            rootToIgnore = parentRb.transform;
    }

    private void EnsureBuffer()
    {
        if (hitBuffer == null || hitBuffer.Length != maxHitsPerRay)
        {
            hitBuffer = new RaycastHit[maxHitsPerRay];
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        EnsureBuffer();

        if (!Application.isPlaying && scanInEditMode)
        {
            Scan();
        }

        Vector3 origin = transform.position;

        DrawSensorRays(origin);
        DrawDistanceMarkers(origin);
        DrawHitMarker();

#if UNITY_EDITOR
        if (drawLabels)
        {
            DrawEditorLabel(origin);
        }
#endif
    }

    private void DrawSensorRays(Vector3 origin)
    {
        int rays = Mathf.Max(1, rayCount);

        for (int i = 0; i < rays; i++)
        {
            float t = rays == 1 ? 0.5f : i / (float)(rays - 1);
            float angle = Mathf.Lerp(-horizontalFOV * 0.5f, horizontalFOV * 0.5f, t);

            Vector3 direction = GetRayDirection(angle);
            Vector3 endPoint = origin + direction * range;

            if (TryCast(origin, direction, out RaycastHit hit))
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, hit.point);

                Gizmos.color = new Color(1f, 0.8f, 0f, 0.5f);
                Gizmos.DrawLine(hit.point, endPoint);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(origin, endPoint);
            }
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, Mathf.Max(0.03f, sphereRadius));
    }

    private void DrawDistanceMarkers(Vector3 origin)
    {
#if UNITY_EDITOR
        Vector3 forward = GetRayDirection(0f);
        Vector3 axis = keepRaysHorizontal ? Vector3.up : transform.up;

        Vector3 leftBoundary = Quaternion.AngleAxis(-horizontalFOV * 0.5f, axis) * forward;

        Handles.color = new Color(1f, 0f, 0f, 0.4f);
        Handles.DrawWireArc(origin, axis, leftBoundary, horizontalFOV, stopDistance);

        Handles.color = new Color(1f, 0.75f, 0f, 0.35f);
        Handles.DrawWireArc(origin, axis, leftBoundary, horizontalFOV, warningDistance);

        Handles.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Handles.DrawWireArc(origin, axis, leftBoundary, horizontalFOV, range);
#endif
    }

    private void DrawHitMarker()
    {
        if (!Reading.hasHit)
            return;

        Gizmos.color = Reading.blocked ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(Reading.closestPoint, Mathf.Max(0.06f, sphereRadius));

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(
            Reading.closestPoint,
            Reading.closestPoint + Reading.closestNormal * 0.25f
        );
    }

#if UNITY_EDITOR
    private void DrawEditorLabel(Vector3 origin)
    {
        string stateText;

        if (!Reading.hasHit)
        {
            stateText = $"{zone}\nCLEAR\n{range:0.00}m";
        }
        else if (Reading.blocked)
        {
            stateText = $"{zone}\nBLOCKED\n{Reading.closestDistance:0.00}m";
        }
        else if (Reading.warning)
        {
            stateText = $"{zone}\nWARNING\n{Reading.closestDistance:0.00}m";
        }
        else
        {
            stateText = $"{zone}\nDETECTED\n{Reading.closestDistance:0.00}m";
        }

        Handles.Label(origin + Vector3.up * 0.25f, stateText);
    }
#endif
}