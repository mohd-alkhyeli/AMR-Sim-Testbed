using UnityEngine;

public class ThreeWheelDrive : MonoBehaviour
{
    [Header("Wheel Colliders")]
    [Tooltip("The left front wheel collider. This receives motor torque and braking torque.")]
    public WheelCollider frontLeftWheel;

    [Tooltip("The right front wheel collider. This receives motor torque and braking torque.")]
    public WheelCollider frontRightWheel;

    [Tooltip("The single rear wheel collider. This handles steering and can also receive braking torque.")]
    public WheelCollider rearSteerWheel;

    [Header("References")]
    [Tooltip("Vehicle Rigidbody. If left empty, the script will try to find one on the same GameObject.")]
    public Rigidbody rb;

    [Header("AMR Motor Settings")]
    [Tooltip("Maximum motor torque per powered wheel. Higher = stronger acceleration. Lower = slower, smoother AMR movement.")]
    public float maxMotorTorque = 250f;

    [Tooltip("Physical target max forward speed in meters per second. 2 m/s = 7.2 km/h. This does NOT clamp velocity; it fades torque.")]
    public float maxForwardSpeed = 2.0f;

    [Tooltip("Physical target max reverse speed in meters per second. Lower values make reversing safer and slower.")]
    public float maxReverseSpeed = 0.8f;

    [Range(0.1f, 1f)]
    [Tooltip("Where motor torque starts fading as a fraction of max speed. Lower = smoother earlier limiting. Higher = stronger acceleration until near max speed.")]
    public float torqueFadeStartPercent = 0.65f;

    [Header("Braking Settings")]
    [Tooltip("Maximum braking torque when brake input is 1. Higher = shorter stopping distance. Too high may cause harsh stops or instability.")]
    public float maxBrakeTorque = 600f;

    [Tooltip("Brake torque automatically applied when throttle is released. Higher = less coasting, more robot-like behavior.")]
    public float coastBrakeTorque = 120f;

    [Tooltip("Brake torque applied when nearly stopped and no throttle is given. Higher = better hill holding.")]
    public float holdBrakeTorque = 400f;

    [Tooltip("Speed below which the vehicle is considered nearly stopped.")]
    public float holdSpeedThreshold = 0.08f;

    [Tooltip("Brake torque applied when trying to reverse direction while still moving.")]
    public float directionChangeBrakeTorque = 500f;

    [Tooltip("Extra brake torque applied if the vehicle exceeds its physical target speed, such as on a slope.")]
    public float overspeedBrakeTorque = 300f;

    [Tooltip("Small tolerance before overspeed braking activates. Higher = allows more speed drift before braking.")]
    public float overspeedTolerance = 0.1f;

    [Header("Steering")]
    [Tooltip("Maximum steering angle for the rear wheel. Higher = tighter turning but more twitchy. For AMRs, 15-25 is usually safer than 35.")]
    public float maxSteeringAngle = 22f;

    [Tooltip("How quickly steering input changes. Higher = faster steering response. Lower = smoother but slower response.")]
    public float steeringResponse = 6f;

    [Header("Input Smoothing")]
    [Tooltip("How quickly throttle input changes. Higher = snappier acceleration. Lower = smoother AMR movement.")]
    public float throttleResponse = 3f;

    [Tooltip("How quickly brake input changes. Higher = immediate braking. Lower = smoother braking.")]
    public float brakeResponse = 8f;

    private float targetThrottleInput;
    private float targetSteeringInput;
    private float targetBrakeInput;

    private float currentThrottleInput;
    private float currentSteeringInput;
    private float currentBrakeInput;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Basic input method.
    /// Throttle: -1 reverse, 0 idle, 1 forward.
    /// Steering: -1 left, 0 straight, 1 right.
    /// Brake defaults to 0.
    /// </summary>
    public void SetInputs(float throttle, float steering)
    {
        SetInputs(throttle, steering, 0f);
    }

    /// <summary>
    /// AMR input method.
    /// Throttle: -1 reverse, 0 idle, 1 forward.
    /// Steering: -1 left, 0 straight, 1 right.
    /// Brake: 0 no manual brake, 1 full brake.
    /// </summary>
    public void SetInputs(float throttle, float steering, float brake)
    {
        targetThrottleInput = Mathf.Clamp(throttle, -1f, 1f);
        targetSteeringInput = Mathf.Clamp(steering, -1f, 1f);
        targetBrakeInput = Mathf.Clamp01(brake);
    }

    private void FixedUpdate()
    {
        SmoothInputs();

        ApplySteering();
        ApplyMotorAndBrakes();
    }

    private void SmoothInputs()
    {
        currentThrottleInput = Mathf.MoveTowards(
            currentThrottleInput,
            targetThrottleInput,
            throttleResponse * Time.fixedDeltaTime
        );

        currentSteeringInput = Mathf.MoveTowards(
            currentSteeringInput,
            targetSteeringInput,
            steeringResponse * Time.fixedDeltaTime
        );

        currentBrakeInput = Mathf.MoveTowards(
            currentBrakeInput,
            targetBrakeInput,
            brakeResponse * Time.fixedDeltaTime
        );
    }

    private void ApplySteering()
    {
        if (rearSteerWheel == null)
            return;

        // Rear-wheel steering is inverted so that positive input turns right.
        float steerAngle = -currentSteeringInput * maxSteeringAngle;

        rearSteerWheel.steerAngle = steerAngle;
    }

    private void ApplyMotorAndBrakes()
    {
        float forwardSpeed = GetForwardSpeed();

        float brakeTorque = CalculateBrakeTorque(forwardSpeed);
        float motorTorque = CalculateMotorTorque(forwardSpeed, brakeTorque);

        ApplyWheelTorque(frontLeftWheel, motorTorque, brakeTorque);
        ApplyWheelTorque(frontRightWheel, motorTorque, brakeTorque);

        // Rear wheel is not powered, but it can brake.
        if (rearSteerWheel != null)
        {
            rearSteerWheel.motorTorque = 0f;
            rearSteerWheel.brakeTorque = brakeTorque;
        }
    }

    private float CalculateMotorTorque(float forwardSpeed, float brakeTorque)
    {
        if (Mathf.Abs(currentThrottleInput) < 0.01f)
            return 0f;

        // Do not fight the brakes.
        if (brakeTorque > 0.01f)
            return 0f;

        float torqueScale = GetTorqueScaleFromSpeed(forwardSpeed);

        return currentThrottleInput * maxMotorTorque * torqueScale;
    }

    private float CalculateBrakeTorque(float forwardSpeed)
    {
        float brakeTorque = currentBrakeInput * maxBrakeTorque;

        bool throttleNeutral = Mathf.Abs(currentThrottleInput) < 0.01f;
        bool moving = Mathf.Abs(forwardSpeed) > holdSpeedThreshold;

        // Robot-like deceleration when throttle is released.
        if (throttleNeutral && moving)
        {
            brakeTorque = Mathf.Max(brakeTorque, coastBrakeTorque);
        }

        // Hold position when almost stopped.
        if (throttleNeutral && !moving)
        {
            brakeTorque = Mathf.Max(brakeTorque, holdBrakeTorque);
        }

        // If trying to reverse direction while still moving, brake first.
        bool tryingForwardWhileMovingBackward =
            currentThrottleInput > 0.01f && forwardSpeed < -holdSpeedThreshold;

        bool tryingReverseWhileMovingForward =
            currentThrottleInput < -0.01f && forwardSpeed > holdSpeedThreshold;

        if (tryingForwardWhileMovingBackward || tryingReverseWhileMovingForward)
        {
            brakeTorque = Mathf.Max(brakeTorque, directionChangeBrakeTorque);
        }

        // Physical overspeed control using brake torque, not velocity clamping.
        if (forwardSpeed > maxForwardSpeed + overspeedTolerance)
        {
            brakeTorque = Mathf.Max(brakeTorque, overspeedBrakeTorque);
        }
        else if (forwardSpeed < -maxReverseSpeed - overspeedTolerance)
        {
            brakeTorque = Mathf.Max(brakeTorque, overspeedBrakeTorque);
        }

        return brakeTorque;
    }

    private float GetTorqueScaleFromSpeed(float forwardSpeed)
    {
        float commandedDirection = Mathf.Sign(currentThrottleInput);

        float speedInCommandedDirection =
            commandedDirection > 0f ? forwardSpeed : -forwardSpeed;

        // If moving opposite the commanded direction, allow full torque after braking logic clears.
        if (speedInCommandedDirection <= 0f)
            return 1f;

        float speedLimit =
            commandedDirection > 0f ? maxForwardSpeed : maxReverseSpeed;

        if (speedLimit <= 0.01f)
            return 0f;

        float fadeStartSpeed = speedLimit * torqueFadeStartPercent;

        if (speedInCommandedDirection <= fadeStartSpeed)
            return 1f;

        if (speedInCommandedDirection >= speedLimit)
            return 0f;

        float fadeT = Mathf.InverseLerp(
            fadeStartSpeed,
            speedLimit,
            speedInCommandedDirection
        );

        // Smoothly fade torque to zero near max speed.
        return 1f - Mathf.SmoothStep(0f, 1f, fadeT);
    }

    private float GetForwardSpeed()
    {
        if (rb == null)
            return 0f;

        return Vector3.Dot(rb.linearVelocity, transform.forward);
    }

    private void ApplyWheelTorque(WheelCollider wheel, float motorTorque, float brakeTorque)
    {
        if (wheel == null)
            return;

        wheel.motorTorque = motorTorque;
        wheel.brakeTorque = brakeTorque;
    }
}