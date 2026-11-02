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

    [Header("Configuración Física")]
    [Tooltip("Radio del aro en metros")]
    public float wheelRadius = 0.3f;

    [Tooltip("Velocidad angular máxima permitida (rad/s)")]
    public float maxAngularSpeed = 15.0f;

    [Tooltip("Factor de suavizado (mayor = respuesta más rápida)")]
    [Range(5f, 25f)]
    public float smoothingRate = 12.0f;

    [Tooltip("Delta angular mínimo en grados por frame para registrar movimiento")]
    public float angularDeadzone = 0.1f;

    [Header("Inercia e Impulso")]
    [Tooltip("Multiplicador de ganancia del empuje del aro (compensa brazadas cortas)")]
    [SerializeField] public float pushGain = 0.72f;

    [Tooltip("Resistencia de rodadura al soltar (rad/s²). ~4.5 = detención controlada")]
    [SerializeField] public float rollingResistance = 4.5f;

    [Tooltip("Frenado cuando la mano agarra el aro y se queda quieta o va en contra (rad/s²)")]
    public float handBrake = 9.5f;

    [Tooltip("Segundos de mano quieta antes de empezar a frenar")]
    public float brakeDelay = 0.04f;

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
        // SelectingPointsCount detecta agarraderas de mandos o manos libres
        bool currentlyGrabbed = _grabbable != null && _grabbable.SelectingPointsCount > 0;

        // Disparo háptico en el frame exacto de sujeción inicial
        if (currentlyGrabbed && !_wasGrabbedLastFrame)
        {
            TriggerHapticFeedback();
        }
        _wasGrabbedLastFrame = currentlyGrabbed;

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
            _wasGrabbed = true;
            _lastLocalRot = currentLocalRot;
            _graceFrames = 0;
            return;
        }

        _graceFrames++;

        // Grace period para estabilización del transformer
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
            float raw = (signedDeltaDeg * Mathf.Deg2Rad) / Time.deltaTime * pushGain;
            raw = Mathf.Clamp(raw, -maxAngularSpeed, maxAngularSpeed);

            bool sameDir = Mathf.Sign(raw) == Mathf.Sign(_currentAngularVelocity)
                           || Mathf.Abs(_currentAngularVelocity) < 0.01f;

            if (sameDir && Mathf.Abs(raw) >= Mathf.Abs(_currentAngularVelocity))
            {
                // Ataque de empuje
                _currentAngularVelocity = Mathf.Lerp(_currentAngularVelocity, raw,
                    1f - Mathf.Exp(-smoothingRate * 3f * Time.deltaTime));
            }
            else if (sameDir)
            {
                // Desaceleración por rodadura si la mano va más lenta
                _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, 0f,
                    rollingResistance * Time.deltaTime);
            }
            else
            {
                // Mano frenando o empujando en sentido contrario
                _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, raw,
                    handBrake * Time.deltaTime);
            }
        }
        else
        {
            // Mano estática sujetando el aro
            _stillTime += Time.deltaTime;
            float decel = _stillTime > brakeDelay ? handBrake : rollingResistance;
            _currentAngularVelocity = Mathf.MoveTowards(_currentAngularVelocity, 0f,
                decel * Time.deltaTime);
        }

        _lastLocalRot = currentLocalRot;
    }

    /// <summary>
    /// Aplica desaceleración pasiva y restablece la orientación del collider de agarre.
    /// </summary>
    private void HandleReleased()
    {
        if (_wasGrabbed)
        {
            transform.localRotation = _restLocalRotation;
            _wasGrabbed = false;
            _graceFrames = 0;
        }

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
    }
}
