using UnityEngine;

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
    public float gravity = -9.81f;

    [Header("Modo Simulación (Sin Visor)")]
    public bool enableKeyboardFallback = true;
    public float keyboardDriveSpeed = 1.5f;

    private CharacterController controller;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1. Simulación por teclado si no se está usando el agarre físico
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

        // 2. Obtener velocidades lineales de cada rueda
        float vLeft = leftWheel != null ? leftWheel.LinearVelocity : 0f;
        float vRight = rightWheel != null ? rightWheel.LinearVelocity : 0f;

        // 3. Cinemática diferencial
        // Velocidad lineal hacia adelante = promedio de ambas ruedas
        float linearSpeed = (vLeft + vRight) / 2f;
        linearSpeed = Mathf.Clamp(linearSpeed, -maxSpeed, maxSpeed);

        // Velocidad angular de rotación = diferencia entre ruedas / ancho de vía
        float angularVelocity = (vRight - vLeft) / trackWidth; // rad/s
        float turnDegrees = (angularVelocity * Mathf.Rad2Deg) * Time.deltaTime;

        // 4. Aplicar rotación sobre su eje
        transform.Rotate(0, turnDegrees, 0);

        // 5. Aplicar desplazamiento y gravedad
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Adherencia al suelo/rampas
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 move = (transform.forward * linearSpeed);
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
}