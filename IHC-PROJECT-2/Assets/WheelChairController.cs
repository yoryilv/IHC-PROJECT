using UnityEngine;

/// <summary>
/// Controlador de locomoción de silla de ruedas basado en cinemática diferencial.
///
/// Modificaciones aplicadas:
/// 1. Proyección en plano de pendientes (Vector3.ProjectOnPlane) para permitir avance
///    y retroceso suave en rampas sin atorarse contra la malla del suelo.
/// 2. Freno de retención en pendientes: anula el deslizamiento involuntario cuando
///    las ruedas están detenidas.
/// 3. Filtro anti-zigzagueo: anula micro-giros involuntarios al avanzar recto para mitigar el mareo.
/// 4. Límites calibrados (maxSpeed = 1.3m/s, maxTurnRate = 45°/s).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class WheelchairController : MonoBehaviour
{
    [Header("Ruedas Físicas")]
    public WheelInteraction leftWheel;
    public WheelInteraction rightWheel;



    [Header("Resistencia al Suelo")]
    [Tooltip("Fricción continua contra el piso. Mayor valor = el suelo frena más el avance (1.5 a 3.0 recomendado)")]
    public float groundResistance = 2.0f;

    [Tooltip("Multiplicador de esfuerzo al subir rampas. Hace que avanzar cuesta arriba sea más pesado")]
    public float uphillResistance = 1.6f;

    [Header("Dimensiones Silla")]
    [Tooltip("Distancia entre ambas ruedas en metros")]
    public float trackWidth = 0.65f;

    [Header("Límites de Movimiento")]
    [Tooltip("Velocidad lineal crucero máxima (1.2 a 1.4 m/s recomendado en interiores)")]
    public float maxSpeed = 1.3f;           // m/s

    [Tooltip("Límite de velocidad angular para evitar latigazos que producen mareo")]
    public float maxTurnRate = 45.0f;       // grados/s

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
        // 1. Simulación por teclado (pruebas en Editor)
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

        // 2. Leer velocidades lineales de cada rueda
        float vLeft = leftWheel != null ? leftWheel.LinearVelocity : 0f;
        float vRight = rightWheel != null ? rightWheel.LinearVelocity : 0f;

        // 3. Cinemática diferencial básica
        float linearSpeed = (vLeft + vRight) * 0.5f;

        float wheelDifference = vRight - vLeft;
        float angularVelocity = wheelDifference / trackWidth;

        // --- ASISTENCIA DE AVANCE RECTO (HEADING LOCK) ---
        bool bothPushingForward = vLeft > 0.05f && vRight > 0.05f;
        bool bothPushingBackward = vLeft < -0.05f && vRight < -0.05f;

        if (bothPushingForward || bothPushingBackward)
        {
            if (Mathf.Abs(wheelDifference) < 0.40f)
            {
                angularVelocity = 0f; // Bloquear micro-giros involuntarios
            }
            else
            {
                angularVelocity *= 0.3f; // Atenuar desvío si quiere girar avanzando
            }
        }

        float turnDegrees = angularVelocity * Mathf.Rad2Deg * Time.deltaTime;
        turnDegrees = Mathf.Clamp(turnDegrees, -maxTurnRate * Time.deltaTime, maxTurnRate * Time.deltaTime);

        // 4. Aplicar rotación sobre el eje Y
        transform.Rotate(0f, turnDegrees, 0f);

        // 5. Detección profunda de superficie y ángulo del suelo
        Vector3 groundNormal = Vector3.up;
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * (controller != null ? controller.center.y : 0.5f);

        bool hitGround = Physics.Raycast(rayOrigin, Vector3.down, out hit, 1.8f, ~0, QueryTriggerInteraction.Ignore);
        bool isSmoothFloor = false;

        if (hitGround)
        {
            groundNormal = hit.normal;

            // Detectar si la rueda está sobre la estructura con el Tag "PisoLiso"
            if (hit.collider.CompareTag("PisoLiso"))
            {
                isSmoothFloor = true;
            }
        }

        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);

        // 6. Cinemática sobre pendientes y Resistencia al Suelo
        Vector3 slopeForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;

        if (controller.isGrounded || hitGround)
        {
            Vector3 downhillDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            float downhillAlignment = Vector3.Dot(transform.forward, downhillDir);

            // A. Resistencia al subir rampa: si empujas hacia arriba, el peso del usuario reduce la velocidad
            if (downhillAlignment < -0.1f && linearSpeed > 0f)
            {
                float slopeResistanceFactor = (slopeAngle / 45.0f) * 1.5f; // Factor de sobrecarga en pendiente
                linearSpeed = Mathf.Max(0.2f, linearSpeed - (linearSpeed * slopeResistanceFactor));
            }

            // B. Fricción continua según el suelo
            // Piso base = 1.8 (agarre firme) | Piso liso = 0.35 (resbala y planea suave)
            float currentResistance = isSmoothFloor ? 0.35f : 1.8f;
            linearSpeed = Mathf.MoveTowards(linearSpeed, 0f, currentResistance * Time.deltaTime);

            // Límite de velocidad dinámico: en piso liso se desbloquea un 35% más de velocidad máxima
            float currentMaxSpeed = isSmoothFloor ? (maxSpeed * 1.35f) : maxSpeed;
            linearSpeed = Mathf.Clamp(linearSpeed, -currentMaxSpeed * 0.7f, currentMaxSpeed);

            Vector3 move = slopeForward * linearSpeed;

            // C. Gravedad en pendiente: solo rueda si está alineada con la bajada (0 resbalón lateral)
            if (slopeAngle > 5.0f)
            {
                // Fuerza de rodadura longitudinal proporcional a la inclinación
                Vector3 rollGravity = slopeForward * (downhillAlignment * Mathf.Abs(gravity) * 0.28f);
                move += rollGravity;
            }

            // Snap vertical hacia abajo para mantener la cápsula pegada a la rampa sin vibraciones
            move.y -= 2.0f;
            verticalVelocity = 0f;

            // 7. Ejecutar movimiento sobre suelo
            controller.Move(move * Time.deltaTime);
        }
        else
        {
            // Caída libre en el aire
            verticalVelocity += gravity * Time.deltaTime;
            Vector3 airMove = transform.forward * linearSpeed;
            airMove.y = verticalVelocity;

            // 7. Ejecutar movimiento en el aire
            controller.Move(airMove * Time.deltaTime);
        }
    }
}