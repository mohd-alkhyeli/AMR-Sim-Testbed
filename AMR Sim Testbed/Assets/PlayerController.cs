using UnityEngine;

[RequireComponent(typeof(ThreeWheelDrive))]
public class PlayerController : MonoBehaviour
{
    private ThreeWheelDrive vehicleDrivetrain;

    [Header("Input Customization")]
    [Tooltip("Check this if you want to use raw inputs (no smoothing/snapping for instant keyboard response).")]
    public bool useRawInput = false;

    private void Awake()
    {
        // Automatically fetch the drivetrain script attached to this GameObject
        vehicleDrivetrain = GetComponent<ThreeWheelDrive>();
    }

    private void Update()
    {
        if (vehicleDrivetrain == null) return;

        float throttle = 0f;
        float steering = 0f;

        if (useRawInput)
        {
            // Instantly returns -1, 0, or 1 (Good for high-precision or arcade feel)
            throttle = Input.GetAxisRaw("Vertical");
            steering = Input.GetAxisRaw("Horizontal");
        }
        else
        {
            // Smooths the values slightly between 0 and 1 (Feels more like a real car accelerating/turning)
            throttle = Input.GetAxis("Vertical");
            steering = Input.GetAxis("Horizontal");
        }

        // Send the keyboard values directly to the independent movement script
        vehicleDrivetrain.SetInputs(throttle, steering);
    }
}