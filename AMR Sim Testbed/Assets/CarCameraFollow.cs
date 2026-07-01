using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class SimpleCarCameraFollow : MonoBehaviour
{
    [Header("Target")]

    [Tooltip("The car object the camera follows. Usually this should be the car root object, not the visual mesh.")]
    public Transform target;


    [Header("Camera Position")]

    [Tooltip(
        "Camera position relative to the car.\n\n" +
        "X: left/right offset. Higher = camera moves to the car's right. Lower/negative = camera moves to the car's left.\n" +
        "Y: height. Higher = camera sits higher above the car. Lower = camera sits closer to the ground.\n" +
        "Z: distance behind/in front. More negative = farther behind the car. Closer to 0 = closer to the car. Positive = in front of the car."
    )]
    public Vector3 followOffset = new Vector3(0f, 3.2f, -7.5f);

    [Tooltip(
        "How high above the car the camera looks.\n\n" +
        "Higher = camera looks more toward the roof/sky, showing more of the road ahead.\n" +
        "Lower = camera looks more toward the car body/ground."
    )]
    public float lookAtHeight = 1.2f;


    [Header("Smoothing")]

    [Tooltip(
        "How quickly the camera position catches up to the car.\n\n" +
        "Higher = tighter and more responsive, but can feel less smooth.\n" +
        "Lower = smoother and floatier, but camera lags behind more."
    )]
    [Range(1f, 30f)]
    public float positionSharpness = 10f;

    [Tooltip(
        "How quickly the camera rotates to look at the car.\n\n" +
        "Higher = camera turns quickly and stays locked on.\n" +
        "Lower = camera rotates slowly and feels heavier/cinematic."
    )]
    [Range(1f, 30f)]
    public float rotationSharpness = 12f;


    [Header("Look Ahead")]

    [Tooltip(
        "If enabled, the camera looks slightly ahead of the car based on its speed.\n\n" +
        "Enabled = better sense of speed and direction.\n" +
        "Disabled = camera always looks directly at the car."
    )]
    public bool useLookAhead = true;

    [Tooltip(
        "Maximum distance the camera looks ahead of the car.\n\n" +
        "Higher = camera shows more road ahead at speed.\n" +
        "Lower = camera stays focused closer to the car."
    )]
    public float maxLookAheadDistance = 4f;

    [Tooltip(
        "The speed where look-ahead reaches its maximum value.\n\n" +
        "Higher = car must go faster before the camera looks far ahead.\n" +
        "Lower = look-ahead happens even at slower speeds."
    )]
    public float speedForMaxLookAhead = 35f;

    [Tooltip(
        "How quickly the look-ahead point moves.\n\n" +
        "Higher = look-ahead reacts faster to speed changes.\n" +
        "Lower = look-ahead moves more smoothly but feels delayed."
    )]
    [Range(1f, 30f)]
    public float lookAheadSharpness = 8f;


    [Header("Field of View")]

    [Tooltip(
        "If enabled, the camera FOV increases as the car gets faster.\n\n" +
        "Enabled = stronger speed feeling.\n" +
        "Disabled = fixed camera FOV."
    )]
    public bool useSpeedFov = true;

    [Tooltip(
        "Camera FOV when the car is slow or stopped.\n\n" +
        "Higher = wider view, more arcade-like.\n" +
        "Lower = narrower view, more realistic/zoomed-in."
    )]
    public float normalFov = 60f;

    [Tooltip(
        "Camera FOV when the car is moving fast.\n\n" +
        "Higher = stronger speed effect and wider vision.\n" +
        "Lower = less speed effect."
    )]
    public float fastFov = 75f;

    [Tooltip(
        "The speed where the camera reaches Fast FOV.\n\n" +
        "Higher = FOV changes only at higher speeds.\n" +
        "Lower = FOV widens earlier."
    )]
    public float speedForFastFov = 40f;

    [Tooltip(
        "How quickly the FOV changes.\n\n" +
        "Higher = FOV reacts quickly to acceleration/braking.\n" +
        "Lower = FOV changes more smoothly and slowly."
    )]
    [Range(1f, 30f)]
    public float fovSharpness = 6f;


    private Camera cam;

    private Vector3 previousTargetPosition;
    private bool hasPreviousPosition;

    private Vector3 currentLookAhead;

    private void Awake()
    {
        cam = GetComponent<Camera>();

        if (target != null)
        {
            previousTargetPosition = target.position;
            hasPreviousPosition = true;
        }

        cam.fieldOfView = normalFov;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        Vector3 velocity = GetEstimatedVelocity(deltaTime);
        float speed = velocity.magnitude;

        Vector3 flatForward = GetFlatForward();

        Quaternion yawRotation = Quaternion.LookRotation(flatForward, Vector3.up);

        Vector3 desiredCameraPosition = target.position + yawRotation * followOffset;

        Vector3 lookAtPoint = target.position + Vector3.up * lookAtHeight;

        if (useLookAhead)
        {
            float speedPercent = Mathf.InverseLerp(0f, speedForMaxLookAhead, speed);
            Vector3 desiredLookAhead = flatForward * maxLookAheadDistance * speedPercent;

            currentLookAhead = SmoothValue(
                currentLookAhead,
                desiredLookAhead,
                lookAheadSharpness,
                deltaTime
            );

            lookAtPoint += currentLookAhead;
        }
        else
        {
            currentLookAhead = Vector3.zero;
        }

        transform.position = SmoothValue(
            transform.position,
            desiredCameraPosition,
            positionSharpness,
            deltaTime
        );

        Vector3 directionToLookPoint = lookAtPoint - transform.position;

        if (directionToLookPoint.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(directionToLookPoint.normalized, Vector3.up);

            transform.rotation = SmoothRotation(
                transform.rotation,
                desiredRotation,
                rotationSharpness,
                deltaTime
            );
        }

        UpdateFov(speed, deltaTime);
    }

    private Vector3 GetEstimatedVelocity(float deltaTime)
    {
        if (!hasPreviousPosition)
        {
            previousTargetPosition = target.position;
            hasPreviousPosition = true;
            return Vector3.zero;
        }

        Vector3 velocity = (target.position - previousTargetPosition) / deltaTime;
        previousTargetPosition = target.position;

        return velocity;
    }

    private Vector3 GetFlatForward()
    {
        Vector3 flatForward = target.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;

        return flatForward.normalized;
    }

    private void UpdateFov(float speed, float deltaTime)
    {
        if (!useSpeedFov)
        {
            cam.fieldOfView = SmoothFloat(
                cam.fieldOfView,
                normalFov,
                fovSharpness,
                deltaTime
            );

            return;
        }

        float speedPercent = Mathf.InverseLerp(0f, speedForFastFov, speed);
        float desiredFov = Mathf.Lerp(normalFov, fastFov, speedPercent);

        cam.fieldOfView = SmoothFloat(
            cam.fieldOfView,
            desiredFov,
            fovSharpness,
            deltaTime
        );
    }

    private Vector3 SmoothValue(Vector3 current, Vector3 targetValue, float sharpness, float deltaTime)
    {
        float smoothingAmount = 1f - Mathf.Exp(-sharpness * deltaTime);
        return Vector3.Lerp(current, targetValue, smoothingAmount);
    }

    private float SmoothFloat(float current, float targetValue, float sharpness, float deltaTime)
    {
        float smoothingAmount = 1f - Mathf.Exp(-sharpness * deltaTime);
        return Mathf.Lerp(current, targetValue, smoothingAmount);
    }

    private Quaternion SmoothRotation(Quaternion current, Quaternion targetValue, float sharpness, float deltaTime)
    {
        float smoothingAmount = 1f - Mathf.Exp(-sharpness * deltaTime);
        return Quaternion.Slerp(current, targetValue, smoothingAmount);
    }
}