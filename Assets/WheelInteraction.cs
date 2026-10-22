using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Lee el delta angular real del transform del aro (rotado por OneGrabRotateTransformer)
/// y lo convierte en velocidad lineal para la cinemática diferencial de la silla.
///
/// ARQUITECTURA: El SDK de Meta resuelve el arco del agarre internamente.
/// Este script NUNCA rastrea posiciones de mano — solo lee la rotación resultante.
///
/// REQUISITOS en el Inspector para cada WheelGrabbable_L/R:
///   1. Grabbable              → OneGrabTransformer = (este mismo GO con OneGrabRotateTransformer)
///   2. HandGrabInteractable   → (ya existente)
///   3. OneGrabRotateTransformer → Rotation Axis = Right (eje X local)
///                                 Constraints: MinAngle.Constrain = false
///                                              MaxAngle.Constrain = false
///   4. Rigidbody              → Is Kinematic = true, Use Gravity = false
///   5. CapsuleCollider        → Is Trigger = true
///   6. WheelInteraction (este script)
/// </summary>
public class WheelInteraction : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [Tooltip("Objeto con la malla de la rueda para feedback visual de giro (opcional)")]
    public Transform wheelMeshPivot;

    [Header("Configuración Física")]
    [Tooltip("Radio del aro en metros")]
    public float wheelRadius = 0.3f;

    [Tooltip("Desaceleración al soltar (rad/s²)")]
    public float friction = 2.0f;

    [Tooltip("Velocidad angular máxima permitida (rad/s)")]
    public float maxAngularSpeed = 15.0f;

    [Tooltip("Factor de suavizado (mayor = respuesta más rápida)")]
    [Range(5f, 25f)]
    public float smoothingRate = 12.0f;

    [Tooltip("Delta angular mínimo en grados por frame para registrar movimiento")]
    public float angularDeadzone = 0.1f;

    [Header("Inercia / Impulso (nuevo)")]
    [Tooltip("Multiplicador de ganancia del empuje del aro (compensa que una mano empuja en arcos cortos)")]
    [SerializeField] public float pushGain = 0.88f;

    [Tooltip("Resistencia de rodadura al soltar (rad/s²). ~1.0 = coasting corto y realista")]
    [SerializeField] public float rollingResistance = 1.0f;

    [Tooltip("Desaceleración base por rozamiento manual al sujetar el aro (rad/s²)")]
    [SerializeField] public float handBrakeDecel = 5.8f;

    [Tooltip("Rampa del freno: cuánto crece la fuerza por segundo sujeto (x handBrakeDecel). Arranca suave y se intensifica")]
    [SerializeField] public float progressiveBrakeRate = 3.0f;

    private const float MaxBrakeRamp = 4f;
    private float _brakeTime;

    // ── Estado interno ──
    private Grabbable _grabbable;
    private Quaternion _lastLocalRot;
    private float _currentAngularVelocity;   // rad/s (positivo = forward)
    private bool _wasGrabbed;
    private int _graceFrames;

    // Rotación original del interactable (para restaurar al soltar)
    private Quaternion _restLocalRotation;

    /// <summary>
    /// Velocidad lineal de esta rueda en m/s.
    /// Positivo = hacia adelante de la silla.
    /// </summary>
    public float LinearVelocity => _currentAngularVelocity * wheelRadius;

    void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _restLocalRotation = transform.localRotation;
    }

    void Update()
    {
        // SelectingPointsCount cuenta cualquier interactor que sujete el aro:
        // HandGrabInteractor (manos libres), ControllerGrabInteractor / GrabInteractor (mandos, Grip/Trigger).
        bool currentlyGrabbed = _grabbable != null
                             && _grabbable.SelectingPointsCount > 0;

        if (currentlyGrabbed)
        {
            HandleGrabbed();
        }
        else
        {
            HandleReleased();
        }

        UpdateVisualWheel();
    }

    /// <summary>
    /// Mientras el aro está agarrado, lee el delta angular aplicado por
    /// OneGrabRotateTransformer y lo convierte en velocidad angular.
    /// </summary>
    private void HandleGrabbed()
    {
        Quaternion currentLocalRot = transform.localRotation;

        if (!_wasGrabbed)
        {
            // Primer frame de agarre: inicializar sin producir delta
            _wasGrabbed = true;
            _lastLocalRot = currentLocalRot;
            _graceFrames = 0;
            return;
        }

        _graceFrames++;

        // Grace period: dejar que el transformer estabilice el agarre
        if (_graceFrames <= 2)
        {
            _lastLocalRot = currentLocalRot;
            return;
        }

        // Calcular delta angular entre el frame anterior y el actual
        // Quaternion delta = Qcurrent * Inverse(Qlast)
        // Esto nos da la rotación incremental aplicada por el transformer
        Quaternion deltaRot = currentLocalRot * Quaternion.Inverse(_lastLocalRot);

        // Descomponer el delta en ángulo-eje
        deltaRot.ToAngleAxis(out float deltaAngleDeg, out Vector3 axis);

        // Normalizar ángulo a rango [-180, 180]
        if (deltaAngleDeg > 180f) deltaAngleDeg -= 360f;

        // El transformer rota sobre el eje Right (X local).
        // El signo del eje X determina la dirección de propulsión.
        // axis.x > 0 → rotación positiva en X → avance (convención)
        float signedDeltaDeg = deltaAngleDeg * Mathf.Sign(axis.x);

        // Filtro anti-glitch: descartar saltos mayores a 45°/frame
        // (imposible con movimiento humano real a 72+ FPS)
        if (Mathf.Abs(signedDeltaDeg) > 45f)
        {
            _lastLocalRot = currentLocalRot;
            return;
        }

        if (Time.deltaTime > 0f && Mathf.Abs(signedDeltaDeg) > angularDeadzone)
        {
            float raw = (signedDeltaDeg * Mathf.Deg2Rad) / Time.deltaTime * pushGain;
            raw = Mathf.Clamp(raw, -maxAngularSpeed, maxAngularSpeed);

            bool sameDir = Mathf.Sign(raw) == Mathf.Sign(_currentAngularVelocity)
                           || Mathf.Abs(_currentAngularVelocity) < 0.01f;

            if (sameDir && Mathf.Abs(raw) >= Mathf.Abs(_currentAngularVelocity))
            {
                // ATAQUE: el empuje cancela el freno y transfiere tracción
                _brakeTime = 0f;
                _currentAngularVelocity = Mathf.Lerp(_currentAngularVelocity, raw,
                    1f - Mathf.Exp(-smoothingRate * 3f * Time.deltaTime));
            }
            else if (sameDir)
            {
                // Mano más lenta que la silla pero empujando: NO frenar, solo coasting
                _brakeTime = 0f;
                _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, 0f,
                    rollingResistance * Time.deltaTime);
            }
            else
            {
                // Mano en contra: frenado progresivo hacia la velocidad de la mano
                ApplyProgressiveBrake(raw);
            }
        }
        else
        {
            // Mano quieta agarrando: si hay inercia, frenado progresivo; si no, solo rodadura
            if (Mathf.Abs(_currentAngularVelocity) > 0.1f)
            {
                ApplyProgressiveBrake(0f);
            }
            else
            {
                _brakeTime = 0f;
                _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, 0f,
                    rollingResistance * Time.deltaTime);
            }
        }

        _lastLocalRot = currentLocalRot;
    }

    /// <summary>
    /// Al soltar: aplicar fricción pasiva y restaurar rotación del interactable
    /// para que el collider/trigger vuelva a su posición original de agarre.
    /// </summary>
    private void HandleReleased()
    {
        if (_wasGrabbed)
        {
            // Restaurar la rotación local original del interactable
            // para que el trigger vuelva a la posición de agarre óptima
            transform.localRotation = _restLocalRotation;
            _wasGrabbed = false;
            _graceFrames = 0;
        }

        // Resistencia de rodadura: la silla sigue rodando al soltar
        _brakeTime = 0f;
        _currentAngularVelocity = Mathf.MoveTowards(
            _currentAngularVelocity, 0f, rollingResistance * Time.deltaTime
        );
    }

    /// <summary>
    /// Frenado progresivo por rozamiento de mano: la desaceleración arranca suave
    /// (70% de handBrakeDecel) y crece linealmente con el tiempo sujeto
    /// (+progressiveBrakeRate * handBrakeDecel por segundo), hasta detener la rueda.
    /// Con los valores por defecto frena ~40% más rápido que antes.
    /// </summary>
    private void ApplyProgressiveBrake(float targetVelocity)
    {
        _brakeTime += Time.deltaTime;
        float ramp = Mathf.Min(0.7f + progressiveBrakeRate * _brakeTime, MaxBrakeRamp);
        float brakeForce = handBrakeDecel * ramp + rollingResistance;
        _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, targetVelocity,
            brakeForce * Time.deltaTime);
    }

    /// <summary>
    /// Rota la malla visual de la rueda para feedback al usuario.
    /// </summary>
    private void UpdateVisualWheel()
    {
        if (wheelMeshPivot != null)
        {
            float degrees = _currentAngularVelocity * Mathf.Rad2Deg * Time.deltaTime;
            wheelMeshPivot.Rotate(degrees, 0, 0, Space.Self);
        }
    }

    /// <summary>
    /// Permite inyectar velocidad desde teclado (modo simulación sin visor).
    /// </summary>
    public void InjectVelocity(float linearSpeed)
    {
        _currentAngularVelocity = linearSpeed / wheelRadius;
    }
}