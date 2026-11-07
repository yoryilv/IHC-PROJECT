using UnityEngine;

/// <summary>
/// Controlador de locomoción de silla de ruedas basado en cinemática diferencial.
/// Incluye modo pivote para giros cerrados con una sola mano y proyección en pendientes.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WheelchairController : MonoBehaviour
{
    [Header("Ruedas Físicas")]
    public WheelInteraction leftWheel;
    public WheelInteraction rightWheel;

    [Header("Resistencia al Suelo")]
    [Tooltip("Fricción continua contra el piso. Mayor valor = el suelo frena más el avance (1.5 a 3.5)")]
    public float groundResistance = 3.5f;

    [Tooltip("Multiplicador de esfuerzo al subir rampas")]
    public float uphillResistance = 1.6f;

    [Header("Dimensiones Silla")]
    [Tooltip("Distancia entre ambas ruedas en metros")]
    public float trackWidth = 0.65f;

    [Header("Límites de Movimiento")]
    [Tooltip("Velocidad lineal crucero máxima")]
    public float maxSpeed = 1.3f;           // m/s

    [Tooltip("Velocidad angular máxima (subir a 110-140 para giros ágiles)")]
    public float maxTurnRate = 120.0f;      // grados/s

    public float gravity = -9.81f;

    [Header("Modo Simulación (Sin Visor)")]
    public bool enableKeyboardFallback = false;
    public float keyboardDriveSpeed = 1.2f;

    private CharacterController controller;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1. Simulación por teclado
        if (enableKeyboardFallback)
        {
            float v = Input.GetAxis("Vertical");
            float h = Input.GetAxis("Horizontal");

            if (Mathf.Abs(v) > 0.05f || Mathf.Abs(h) > 0.05f)
            {
                float leftTarget = (v - h * 0.5f) * keyboardDriveSpeed;
                float rightTarget = (v + h * 0.5f) * keyboardDriveSpeed;

                if (leftWheel != null) leftWheel.InjectVelocity(leftTarget);
                if (rightWheel != null) rightWheel.InjectVelocity(rightTarget);
            }
        }

        // 2. Leer velocidades lineales de cada rueda
        float vLeft = leftWheel != null ? leftWheel.LinearVelocity : 0f;
        float vRight = rightWheel != null ? rightWheel.LinearVelocity : 0f;

        // 3. Cinemática diferencial
        float linearSpeed = (vLeft + vRight) * 0.5f;
        float wheelDifference = vRight - vLeft;
        float angularVelocity = wheelDifference / trackWidth;

        // --- MODO PIVOTE CON UNA SOLA MANO VS AVANCE RECTO ---
        bool singleWheelPush = (Mathf.Abs(vLeft) > 0.05f && Mathf.Abs(vRight) <= 0.05f) ||
                               (Mathf.Abs(vRight) > 0.05f && Mathf.Abs(vLeft) <= 0.05f);

        if (singleWheelPush)
        {
            // Al empujar con una sola mano, suprimimos el 85% del avance frontal
            // para que la silla pivote en su lugar en vez de desplazarse hacia adelante
            linearSpeed *= 0.15f;
            angularVelocity *= 1.3f;
        }
        else
        {
            // Asistencia de avance recto cuando ambas manos empujan juntas
            bool bothPushingForward = vLeft > 0.05f && vRight > 0.05f;
            bool bothPushingBackward = vLeft < -0.05f && vRight < -0.05f;

            if (bothPushingForward || bothPushingBackward)
            {
                if (Mathf.Abs(wheelDifference) < 0.35f)
                {
                    angularVelocity = 0f; // Bloquear micro-giros involuntarios
                }
                else
                {
                    angularVelocity *= 0.4f; // Giro suave en traslación
                }
            }
        }

        float turnDegrees = angularVelocity * Mathf.Rad2Deg * Time.deltaTime;
        turnDegrees = Mathf.Clamp(turnDegrees, -maxTurnRate * Time.deltaTime, maxTurnRate * Time.deltaTime);

        // 4. Aplicar rotación sobre el eje Y
        transform.Rotate(0f, turnDegrees, 0f);

        // 5. Detección de suelo y pendientes
        Vector3 groundNormal = Vector3.up;
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * (controller != null ? controller.center.y : 0.5f);

        bool hitGround = Physics.Raycast(rayOrigin, Vector3.down, out hit, 1.8f, ~0, QueryTriggerInteraction.Ignore);
        bool isSmoothFloor = false;

        if (hitGround)
        {
            groundNormal = hit.normal;
            if (hit.collider.CompareTag("PisoLiso"))
            {
                isSmoothFloor = true;
            }
        }

        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
        Vector3 slopeForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;

        if (controller.isGrounded || hitGround)
        {
            Vector3 downhillDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            float downhillAlignment = Vector3.Dot(transform.forward, downhillDir);

            // Resistencia cuesta arriba
            if (downhillAlignment < -0.1f && linearSpeed > 0f)
            {
                float slopeResistanceFactor = (slopeAngle / 45.0f) * 1.5f;
                linearSpeed = Mathf.Max(0.2f, linearSpeed - (linearSpeed * slopeResistanceFactor));
            }

            // Fricción del suelo
            float currentResistance = isSmoothFloor ? 0.35f : groundResistance;
            linearSpeed = Mathf.MoveTowards(linearSpeed, 0f, currentResistance * Time.deltaTime);

            float currentMaxSpeed = isSmoothFloor ? (maxSpeed * 1.35f) : maxSpeed;
            linearSpeed = Mathf.Clamp(linearSpeed, -currentMaxSpeed * 0.7f, currentMaxSpeed);

            Vector3 move = slopeForward * linearSpeed;

            // Gravedad en pendiente pronunciada
            if (slopeAngle > 5.0f)
            {
                Vector3 rollGravity = slopeForward * (downhillAlignment * Mathf.Abs(gravity) * 0.28f);
                move += rollGravity;
            }

            move.y -= 2.0f; // Mantener adherido al piso
            verticalVelocity = 0f;

            controller.Move(move * Time.deltaTime);
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
            Vector3 airMove = transform.forward * linearSpeed;
            airMove.y = verticalVelocity;
            controller.Move(airMove * Time.deltaTime);
        }
    }
}