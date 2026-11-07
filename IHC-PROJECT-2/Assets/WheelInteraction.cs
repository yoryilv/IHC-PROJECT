using System.Collections;
using UnityEngine;
using Oculus.Interaction;

/// <summary>
/// Lee el delta angular real del transform del aro (rotado por OneGrabRotateTransformer)
/// y lo convierte en velocidad lineal para la cinemática diferencial de la silla.
///
/// ARQUITECTURA: El SDK de Meta resuelve el arco del agarre internamente.
/// Este script NUNCA rastrea posiciones de mano — solo lee la rotación resultante.
/// </summary>
public class WheelInteraction : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [Tooltip("Objeto con la malla de la rueda para feedback visual de giro (opcional)")]
    public Transform wheelMeshPivot;

    [Tooltip("Luz o indicador visual que se mantendrá encendido mientras se sujete el aro")]
    public GameObject grabIndicator;

    [Header("Configuración Física")]
    [Tooltip("Radio del aro en metros")]
    public float wheelRadius = 0.3f;

    [Tooltip("Velocidad angular máxima permitida (rad/s)")]
    public float maxAngularSpeed = 12.0f;

    [Tooltip("Factor de suavizado (mayor = respuesta más rápida)")]
    [Range(5f, 25f)]
    public float smoothingRate = 12.0f;

    [Tooltip("Delta angular mínimo en grados por frame para registrar movimiento")]
    public float angularDeadzone = 0.15f;

    [Header("Inercia e Impulso")]
    [Tooltip("Multiplicador de ganancia del empuje del aro")]
    [SerializeField] public float pushGain = 0.65f;

    [Tooltip("Resistencia de rodadura al soltar (rad/s²). ~4.5 = detención controlada")]
    [SerializeField] public float rollingResistance = 5.0f;

    [Tooltip("Frenado activo cuando la mano agarra el aro y se queda quieta (rad/s²)")]
    public float handBrakingForce = 28.0f;

    [Tooltip("Segundos de mano quieta antes de empezar a frenar en seco")]
    public float brakeDelay = 0.02f;

    [Header("Contador de Impulsos (Telemetría RF-08)")]
    [Tooltip("Ángulo mínimo en grados que debe rotar el aro en un agarre para contar como brazada")]
    public float minStrokeAngle = 12.0f;

    [Header("Feedback Háptico")]
    [Tooltip("Marca TRUE en la rueda izquierda, FALSE en la rueda derecha")]
    [SerializeField] private bool _isLeftHand = false;

    [Tooltip("Intensidad de vibración (0.1 a 1.0)")]
    [Range(0f, 1f)]
    [SerializeField] private float _hapticStrength = 0.6f;

    [Tooltip("Duración del pulso háptico en segundos")]
    [SerializeField] private float _hapticDuration = 0.12f;

    // ── Estado interno de Háptica ──
    private bool _wasGrabbedLastFrame = false;
    private Coroutine _hapticCoroutine;

    // ── Estado interno de Cinemática ──
    private Grabbable _grabbable;
    private Quaternion _lastLocalRot;
    private float _currentAngularVelocity;   // rad/s (positivo = forward)
    private bool _wasGrabbed;
    private int _graceFrames;
    private float _stillTime;

    // ── Estado interno de Telemetría ──
    private float _accumulatedStrokeAngle = 0f;
    private bool _strokeCounted = false;

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

        if (grabIndicator != null)
        {
            grabIndicator.SetActive(false);
        }
    }

    void Update()
    {
        // SelectingPointsCount detecta agarre activo de mandos o manos de Meta XR
        bool currentlyGrabbed = _grabbable != null && _grabbable.SelectingPointsCount > 0;

        // 1. Feedback visual permanente mientras se sujete el aro
        if (grabIndicator != null && grabIndicator.activeSelf != currentlyGrabbed)
        {
            grabIndicator.SetActive(currentlyGrabbed);
        }

        // 2. Disparo háptico en el frame exacto del contacto inicial
        if (currentlyGrabbed && !_wasGrabbedLastFrame)
        {
            TriggerHapticFeedback();
        }
        _wasGrabbedLastFrame = currentlyGrabbed;

        // 3. Resolución de movimiento / frenado
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

    private void HandleGrabbed()
    {
        Quaternion currentLocalRot = transform.localRotation;

        if (!_wasGrabbed)
        {
            _wasGrabbed = true;
            _lastLocalRot = currentLocalRot;
            _graceFrames = 0;
            _accumulatedStrokeAngle = 0f;
            _strokeCounted = false;
            return;
        }

        _graceFrames++;

        if (_graceFrames <= 2)
        {
            _lastLocalRot = currentLocalRot;
            return;
        }

        Quaternion deltaRot = currentLocalRot * Quaternion.Inverse(_lastLocalRot);
        deltaRot.ToAngleAxis(out float deltaAngleDeg, out Vector3 axis);

        if (deltaAngleDeg > 180f) deltaAngleDeg -= 360f;

        float signedDeltaDeg = deltaAngleDeg * Mathf.Sign(axis.x);

        // Filtro anti-glitch para descartar saltos mayores a 45° por frame
        if (Mathf.Abs(signedDeltaDeg) > 45f)
        {
            _lastLocalRot = currentLocalRot;
            return;
        }

        if (Time.deltaTime > 0f && Mathf.Abs(signedDeltaDeg) > angularDeadzone)
        {
            _stillTime = 0f;

            // Registro de telemetría: acumular arco de giro para validar brazada única
            _accumulatedStrokeAngle += Mathf.Abs(signedDeltaDeg);
            if (!_strokeCounted && _accumulatedStrokeAngle >= minStrokeAngle)
            {
                _strokeCounted = true;
                if (TelemetryManager.Instance != null)
                {
                    TelemetryManager.Instance.AddPush();
                }
            }

            float raw = (signedDeltaDeg * Mathf.Deg2Rad) / Time.deltaTime * pushGain;
            raw = Mathf.Clamp(raw, -maxAngularSpeed, maxAngularSpeed);

            bool sameDir = Mathf.Sign(raw) == Mathf.Sign(_currentAngularVelocity)
                           || Mathf.Abs(_currentAngularVelocity) < 0.01f;

            if (sameDir && Mathf.Abs(raw) >= Mathf.Abs(_currentAngularVelocity))
            {
                _currentAngularVelocity = Mathf.Lerp(_currentAngularVelocity, raw,
                    1f - Mathf.Exp(-smoothingRate * 3f * Time.deltaTime));
            }
            else if (sameDir)
            {
                _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, 0f,
                    rollingResistance * Time.deltaTime);
            }
            else
            {
                // Mano tirando en contra: freno inmediato de disco
                _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, 0f,
                    handBrakingForce * Time.deltaTime);
            }
        }
        else
        {
            // Mano quieta sujetando el aro: actúa como freno de mano real
            _stillTime += Time.deltaTime;
            float decel = _stillTime > brakeDelay ? handBrakingForce : rollingResistance;
            _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, 0f, decel * Time.deltaTime);
        }

        _lastLocalRot = currentLocalRot;
    }

    private void HandleReleased()
    {
        if (_wasGrabbed)
        {
            transform.localRotation = _restLocalRotation;
            _wasGrabbed = false;
            _graceFrames = 0;
            _accumulatedStrokeAngle = 0f;
            _strokeCounted = false;
        }

        // Desaceleración por fricción del piso al soltar
        _currentAngularVelocity = Mathf.MoveTowards(
            _currentAngularVelocity, 0f, rollingResistance * Time.deltaTime
        );
    }

    private void UpdateVisualWheel()
    {
        if (wheelMeshPivot != null)
        {
            float degrees = _currentAngularVelocity * Mathf.Rad2Deg * Time.deltaTime;
            wheelMeshPivot.Rotate(degrees, 0, 0, Space.Self);
        }
    }

    public void InjectVelocity(float linearSpeed)
    {
        _currentAngularVelocity = linearSpeed / wheelRadius;
    }

    // ── Subsistema Háptico ──

    private void TriggerHapticFeedback()
    {
        OVRInput.Controller targetController = _isLeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        if (_hapticCoroutine != null)
        {
            StopCoroutine(_hapticCoroutine);
        }

        _hapticCoroutine = StartCoroutine(HapticPulseRoutine(targetController, _hapticDuration, _hapticStrength));
    }

    private IEnumerator HapticPulseRoutine(OVRInput.Controller controller, float duration, float strength)
    {
        OVRInput.SetControllerVibration(0.8f, strength, controller);
        yield return new WaitForSeconds(duration);
        OVRInput.SetControllerVibration(0f, 0f, controller);
    }

    private void OnDisable()
    {
        OVRInput.Controller targetController = _isLeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        OVRInput.SetControllerVibration(0f, 0f, targetController);

        if (grabIndicator != null)
        {
            grabIndicator.SetActive(false);
        }
    }
}