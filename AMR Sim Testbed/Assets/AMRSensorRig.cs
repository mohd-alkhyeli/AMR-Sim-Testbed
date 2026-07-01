using UnityEngine;

public class AMRSensorRig : MonoBehaviour
{
    [Header("Forward Path Sensors")]
    public AMRSensor front;
    public AMRSensor frontLeft;
    public AMRSensor frontRight;

    [Header("Side Clearance Sensors")]
    public AMRSensor left;
    public AMRSensor right;

    [Header("Rear Sensors")]
    public AMRSensor rear;
    public AMRSensor rearLeft;
    public AMRSensor rearRight;

    public bool FrontBlocked =>
        IsBlocked(front) ||
        IsBlocked(frontLeft) ||
        IsBlocked(frontRight);

    public bool FrontWarning =>
        IsWarning(front) ||
        IsWarning(frontLeft) ||
        IsWarning(frontRight);

    public bool RearBlocked =>
        IsBlocked(rear) ||
        IsBlocked(rearLeft) ||
        IsBlocked(rearRight);

    public bool LeftBlocked =>
        IsBlocked(left) ||
        IsBlocked(frontLeft) ||
        IsBlocked(rearLeft);

    public bool RightBlocked =>
        IsBlocked(right) ||
        IsBlocked(frontRight) ||
        IsBlocked(rearRight);

    public float FrontClearance =>
        MinClearance(front, frontLeft, frontRight);

    public float RearClearance =>
        MinClearance(rear, rearLeft, rearRight);

    /// <summary>
    /// Positive = steer right.
    /// Negative = steer left.
    /// Works with the vehicle controller we made earlier where positive steering means right.
    /// </summary>
    public float AvoidanceSteering
    {
        get
        {
            float leftDanger = Mathf.Max(
                Danger(frontLeft),
                Danger(left),
                Danger(rearLeft)
            );

            float rightDanger = Mathf.Max(
                Danger(frontRight),
                Danger(right),
                Danger(rearRight)
            );

            // Obstacle on left => steer right.
            // Obstacle on right => steer left.
            return Mathf.Clamp(leftDanger - rightDanger, -1f, 1f);
        }
    }

    public void ScanAll()
    {
        Scan(front);
        Scan(frontLeft);
        Scan(frontRight);

        Scan(left);
        Scan(right);

        Scan(rear);
        Scan(rearLeft);
        Scan(rearRight);
    }

    private void Scan(AMRSensor sensor)
    {
        if (sensor != null)
            sensor.Scan();
    }

    private bool IsBlocked(AMRSensor sensor)
    {
        return sensor != null && sensor.IsBlocked;
    }

    private bool IsWarning(AMRSensor sensor)
    {
        return sensor != null && sensor.IsWarning;
    }

    private float Danger(AMRSensor sensor)
    {
        return sensor != null ? sensor.Danger01 : 0f;
    }

    private float Clearance(AMRSensor sensor)
    {
        return sensor != null ? sensor.ClearDistance : float.PositiveInfinity;
    }

    private float MinClearance(params AMRSensor[] sensors)
    {
        float min = float.PositiveInfinity;

        foreach (AMRSensor sensor in sensors)
        {
            if (sensor == null)
                continue;

            min = Mathf.Min(min, sensor.ClearDistance);
        }

        return min;
    }
}
