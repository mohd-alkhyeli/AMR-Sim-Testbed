using UnityEngine;

public enum AMRAutonomousState
{
    Idle,
    DrivingToWaypoint,
    SlowingForWaypoint,
    AvoidingObstacle,
    Blocked,
    Arrived
}

[DisallowMultipleComponent]
public class AMRAutonomousWaypointDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The physical vehicle controller.")]
    public ThreeWheelDrive vehicle;

    [Tooltip("The sensor rig containing front, side, and rear sensors.")]
    public AMRSensorRig sensorRig;

    [Tooltip("Optional waypoint holder. If assigned, the robot drives toward its Target.")]
    public AMRWaypoint waypoint;

    [Tooltip("Direct target transform. Used if no waypoint holder is assigned.")]
    public Transform directTarget;

    [Header("State")]
    public AMRAutonomousState currentState = AMRAutonomousState.Idle;

    [Tooltip("Start driving automatically when the scene begins.")]
    public bool startOnAwake = true;

    [Header("Movement")]
    [Tooltip("Normal forward throttle while driving.")]
    [Range(0f, 1f)]
    public float cruiseThrottle = 0.55f;

    [Tooltip("Reduced throttle when near target, turning sharply, or avoiding obstacles.")]
    [Range(0f, 1f)]
    public float slowThrottle = 0.25f;

    [Tooltip("Throttle used during obstacle avoidance.")]
    [Range(0f, 1f)]
    public float avoidanceThrottle = 0.22f;

    [Tooltip("How strongly the robot steers toward the waypoint.")]
    public float steeringGain = 1.4f;

    [Tooltip("Angle error where steering reaches full input. Lower = more aggressive steering.")]
    public float fullSteerAngleError = 45f;

    [Tooltip("If target is behind this angle, the robot stops instead of trying to drive forward.")]
    public float targetBehindAngle = 115f;

    [Header("Waypoint Arrival")]
    [Tooltip("Distance from target where the robot starts slowing down.")]
    public float slowDownDistance = 1.2f;

    [Tooltip("Distance from target where the robot counts as arrived.")]
    public float arrivalDistance = 0.25f;

    [Tooltip("Brake input applied when arrived.")]
    [Range(0f, 1f)]
    public float arrivedBrake = 1f;

    [Header("Obstacle Avoidance")]
    [Tooltip("How strongly sensor avoidance affects steering.")]
    public float avoidanceSteeringGain = 1.2f;

    [Tooltip("How much waypoint steering remains during avoidance. 0 = pure obstacle avoidance, 1 = still strongly chase waypoint.")]
    [Range(0f, 1f)]
    public float waypointSteeringWeightDuringAvoidance = 0.35f;

    [Tooltip("Brake input when the front path is blocked.")]
    [Range(0f, 1f)]
    public float blockedBrake = 1f;

    [Tooltip("If true, the robot can crawl while avoiding warning-level obstacles. If false, it only slows.")]
    public bool allowCrawlingAvoidance = true;

    [Header("Smoothing")]
    [Tooltip("Higher = faster throttle changes.")]
    public float throttleChangeSpeed = 1.5f;

    [Tooltip("Higher = faster steering changes.")]
    public float steeringChangeSpeed = 3.5f;

    [Tooltip("Higher = faster brake changes.")]
    public float brakeChangeSpeed = 6f;

    [Header("Debug Gizmos")]
    public bool drawGizmos = true;
    public bool drawStateLabel = true;

    private float smoothedThrottle;
    private float smoothedSteering;
    private float smoothedBrake;

    private Vector3 TargetPosition
    {
        get
        {
            Transform target = GetTargetTransform();
            return target != null ? target.position : transform.position;
        }
    }

    private void Awake()
    {
        if (vehicle == null)
            vehicle = GetComponent<ThreeWheelDrive>();

        if (sensorRig == null)
            sensorRig = GetComponent<AMRSensorRig>();
    }

    private void Start()
    {
        currentState = startOnAwake ? AMRAutonomousState.DrivingToWaypoint : AMRAutonomousState.Idle;
    }

    private void FixedUpdate()
    {
        if (sensorRig != null)
            sensorRig.ScanAll();

        UpdateState();
        DriveState();
    }

    public void SetTarget(Transform newTarget)
    {
        directTarget = newTarget;
        waypoint = null;
        currentState = AMRAutonomousState.DrivingToWaypoint;
    }

    public void SetWaypoint(AMRWaypoint newWaypoint)
    {
        waypoint = newWaypoint;
        directTarget = null;
        currentState = AMRAutonomousState.DrivingToWaypoint;
    }

    public void StopAutonomous()
    {
        currentState = AMRAutonomousState.Idle;
        SendInputs(0f, 0f, 1f);
    }

    public void ResumeAutonomous()
    {
        if (GetTargetTransform() != null)
            currentState = AMRAutonomousState.DrivingToWaypoint;
    }

    private void UpdateState()
    {
        if (currentState == AMRAutonomousState.Idle)
            return;

        Transform target = GetTargetTransform();

        if (target == null)
        {
            currentState = AMRAutonomousState.Idle;
            return;
        }

        float distance = GetFlatDistanceToTarget();
        float angleToTarget = Mathf.Abs(GetSignedAngleToTarget());

        if (distance <= arrivalDistance)
        {
            currentState = AMRAutonomousState.Arrived;
            return;
        }

        if (sensorRig != null && sensorRig.FrontBlocked)
        {
            currentState = AMRAutonomousState.Blocked;
            return;
        }

        if (angleToTarget >= targetBehindAngle)
        {
            currentState = AMRAutonomousState.Blocked;
            return;
        }

        if (sensorRig != null && sensorRig.FrontWarning)
        {
            currentState = AMRAutonomousState.AvoidingObstacle;
            return;
        }

        if (distance <= slowDownDistance)
        {
            currentState = AMRAutonomousState.SlowingForWaypoint;
            return;
        }

        currentState = AMRAutonomousState.DrivingToWaypoint;
    }

    private void DriveState()
    {
        switch (currentState)
        {
            case AMRAutonomousState.Idle:
                SendInputs(0f, 0f, 1f);
                break;

            case AMRAutonomousState.DrivingToWaypoint:
                DriveTowardWaypoint(cruiseThrottle, 0f);
                break;

            case AMRAutonomousState.SlowingForWaypoint:
                DriveTowardWaypoint(GetApproachThrottle(), 0f);
                break;

            case AMRAutonomousState.AvoidingObstacle:
                DriveAvoidingObstacle();
                break;

            case AMRAutonomousState.Blocked:
                SendInputs(0f, GetBestBlockedSteeringDirection(), blockedBrake);
                break;

            case AMRAutonomousState.Arrived:
                SendInputs(0f, 0f, arrivedBrake);
                break;
        }
    }

    private void DriveTowardWaypoint(float throttle, float brake)
    {
        float steering = GetWaypointSteering();

        // Slow down if the steering angle is large.
        float turnSlowdown = Mathf.InverseLerp(0.2f, 1f, Mathf.Abs(steering));
        float adjustedThrottle = Mathf.Lerp(throttle, slowThrottle, turnSlowdown);

        SendInputs(adjustedThrottle, steering, brake);
    }

    private void DriveAvoidingObstacle()
    {
        float waypointSteering = GetWaypointSteering();
        float avoidanceSteering = sensorRig != null ? sensorRig.AvoidanceSteering : 0f;

        float finalSteering = Mathf.Clamp(
            waypointSteering * waypointSteeringWeightDuringAvoidance +
            avoidanceSteering * avoidanceSteeringGain,
            -1f,
            1f
        );

        float throttle = allowCrawlingAvoidance ? avoidanceThrottle : 0f;
        float brake = allowCrawlingAvoidance ? 0f : 0.5f;

        SendInputs(throttle, finalSteering, brake);
    }

    private float GetWaypointSteering()
    {
        float signedAngle = GetSignedAngleToTarget();

        float normalizedAngle = Mathf.Clamp(
            signedAngle / Mathf.Max(1f, fullSteerAngleError),
            -1f,
            1f
        );

        return Mathf.Clamp(normalizedAngle * steeringGain, -1f, 1f);
    }

    private float GetBestBlockedSteeringDirection()
    {
        if (sensorRig == null)
            return 0f;

        float avoidance = sensorRig.AvoidanceSteering;

        if (Mathf.Abs(avoidance) > 0.05f)
            return avoidance;

        return GetWaypointSteering();
    }

    private float GetApproachThrottle()
    {
        float distance = GetFlatDistanceToTarget();

        float t = Mathf.InverseLerp(arrivalDistance, slowDownDistance, distance);

        return Mathf.Lerp(0.08f, slowThrottle, t);
    }

    private float GetFlatDistanceToTarget()
    {
        Vector3 toTarget = TargetPosition - transform.position;
        toTarget.y = 0f;
        return toTarget.magnitude;
    }

    private float GetSignedAngleToTarget()
    {
        Vector3 toTarget = TargetPosition - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
            return 0f;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        return Vector3.SignedAngle(forward, toTarget.normalized, Vector3.up);
    }

    private Transform GetTargetTransform()
    {
        if (waypoint != null)
            return waypoint.Target;

        return directTarget;
    }

    private void SendInputs(float throttle, float steering, float brake)
    {
        smoothedThrottle = Mathf.MoveTowards(
            smoothedThrottle,
            throttle,
            throttleChangeSpeed * Time.fixedDeltaTime
        );

        smoothedSteering = Mathf.MoveTowards(
            smoothedSteering,
            steering,
            steeringChangeSpeed * Time.fixedDeltaTime
        );

        smoothedBrake = Mathf.MoveTowards(
            smoothedBrake,
            brake,
            brakeChangeSpeed * Time.fixedDeltaTime
        );

        if (vehicle != null)
        {
            vehicle.SetInputs(smoothedThrottle, smoothedSteering, smoothedBrake);
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        Transform target = GetTargetTransform();

        if (target == null)
            return;

        Vector3 robotPos = transform.position;
        Vector3 targetPos = target.position;

        Vector3 flatRobotPos = new Vector3(robotPos.x, robotPos.y + 0.1f, robotPos.z);
        Vector3 flatTargetPos = new Vector3(targetPos.x, robotPos.y + 0.1f, targetPos.z);

        // Line to target.
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(flatRobotPos, flatTargetPos);

        // Arrival radius.
        Gizmos.color = Color.green;
        DrawFlatCircle(targetPos, arrivalDistance);

        // Slowdown radius.
        Gizmos.color = Color.yellow;
        DrawFlatCircle(targetPos, slowDownDistance);

        // Robot forward direction.
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(
            robotPos + Vector3.up * 0.2f,
            robotPos + Vector3.up * 0.2f + transform.forward * 1f
        );

        // Desired direction to target.
        Vector3 toTarget = targetPos - robotPos;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                robotPos + Vector3.up * 0.35f,
                robotPos + Vector3.up * 0.35f + toTarget.normalized * 1f
            );
        }

#if UNITY_EDITOR
        if (drawStateLabel)
        {
            UnityEditor.Handles.Label(
                robotPos + Vector3.up * 0.8f,
                $"State: {currentState}\nDistance: {GetFlatDistanceToTarget():0.00}m\nAngle: {GetSignedAngleToTarget():0.0}°"
            );
        }
#endif
    }

    private void DrawFlatCircle(Vector3 center, float radius)
    {
        const int segments = 48;

        Vector3 previousPoint = center + new Vector3(radius, 0.05f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 nextPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0.05f,
                Mathf.Sin(angle) * radius
            );

            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
}