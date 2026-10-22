using UnityEngine;

/// <summary>
/// Controlador de locomoción de silla de ruedas basado en cinemática diferencial.
///
/// Lee la velocidad lineal de cada WheelInteraction (calculada a partir del
/// delta angular del OneGrabRotateTransformer) y aplica movimiento y giro
/// al CharacterController del chasis.
///
/// JERARQUÍA ESPERADA:
///   Player_Wheelchair (este script + CharacterController)
///   ├── OVRCameraRig
///   │   └── TrackingSpace → CenterEyeAnchor, HandAnchors...
///   ├── WhelChair (malla visual estática)
///   └── Interactables
///       ├── WheelGrabbable_L (Grabbable + HandGrabInteractable +
///       │                      OneGrabRotateTransformer + WheelInteraction)
///       └── WheelGrabbable_R (ídem)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WheelchairController : MonoBehaviour
{
    [Header("Ruedas Físicas")]
    public WheelInteraction leftWheel;
    public WheelInteraction rightWheel;

    [Header("Dimensiones Silla")]
    [Tooltip("Distancia entre ambas ruedas en metros")]
    public float trackWidth = 0.65f;

    [Header("Límites de Movimiento")]
    public float maxSpeed = 2.0f;           // m/s
    public float maxTurnRate = 90.0f;       // grados/s — límite de giro para evitar trompo
    public float gravity = -9.81f;

    [Header("Modo Simulación (Sin Visor)")]
    public bool enableKeyboardFallback = false;
    public float keyboardDriveSpeed = 1.5f;

    [Header("Joystick (Thumbstick izquierdo)")]
    public bool enableJoystick = true;
    [Range(0f, 0.3f)] public float stickDeadzone = 0.1f;
    [Tooltip("Aceleración lineal m/s² con joystick")]
    public float joystickAccel = 2.5f;
    [Tooltip("Velocidad lineal máxima con joystick (m/s)")]
    public float joystickMaxSpeed = 2.0f;

    private CharacterController controller;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1. Simulación por teclado (solo para pruebas en Editor sin visor)
        if (enableKeyboardFallback)
        {
            float v = Input.GetAxis("Vertical");   // W / S
            float h = Input.GetAxis("Horizontal"); // A / D

            if (Mathf.Abs(v) > 0.05f || Mathf.Abs(h) > 0.05f)
            {
                float leftTarget = (v - h * 0.5f) * keyboardDriveSpeed;
                float rightTarget = (v + h * 0.5f) * keyboardDriveSpeed;

                if (leftWheel != null) leftWheel.InjectVelocity(leftTarget);
                if (rightWheel != null) rightWheel.InjectVelocity(rightTarget);
            }
        }

        // 1b. Joystick: deadzone radial + reescalado lineal (sin curva que ahogue valores bajos)
        if (enableJoystick)
        {
            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
            float mag = stick.magnitude;
            if (mag > stickDeadzone)
            {
                stick = stick.normalized * Mathf.Clamp01((mag - stickDeadzone) / (1f - stickDeadzone));
                float fwd = stick.y * joystickMaxSpeed;
                float turn = stick.x * joystickMaxSpeed * 0.5f;
                if (leftWheel != null) leftWheel.DriveToward(fwd + turn, joystickAccel);
                if (rightWheel != null) rightWheel.DriveToward(fwd - turn, joystickAccel);
            }
        }

        // 2. Leer velocidades lineales de cada rueda (calculadas por WheelInteraction
        //    a partir del delta angular del OneGrabRotateTransformer)
        float vLeft = leftWheel != null ? leftWheel.LinearVelocity : 0f;
        float vRight = rightWheel != null ? rightWheel.LinearVelocity : 0f;

        // 3. Cinemática diferencial
        // Velocidad lineal = promedio de ambas ruedas
        float linearSpeed = (vLeft + vRight) * 0.5f;
        linearSpeed = Mathf.Clamp(linearSpeed, -maxSpeed, maxSpeed);

        // Velocidad angular = diferencia / ancho de vía (rad/s)
        float angularVelocity = (vRight - vLeft) / trackWidth;
        float turnDegrees = angularVelocity * Mathf.Rad2Deg * Time.deltaTime;

        // Clamp de giro para seguridad anti-trompo
        turnDegrees = Mathf.Clamp(turnDegrees, -maxTurnRate * Time.deltaTime,
                                                 maxTurnRate * Time.deltaTime);

        // 4. Aplicar rotación sobre el eje Y
        transform.Rotate(0f, turnDegrees, 0f);

        // 5. Gravedad
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -0.5f; // adherencia suave (-2 + skin 0.08 pegaba el collider)
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // 6. Desplazamiento final
        Vector3 move = transform.forward * linearSpeed;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }
}