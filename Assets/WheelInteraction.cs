using UnityEngine;
using Oculus.Interaction;

public class WheelInteraction : MonoBehaviour
{
    [Header("Referencias Visuales")]
    [Tooltip("El objeto que contiene la malla de la rueda para hacerla girar")]
    public Transform wheelMeshPivot;

    [Header("Configuración")]
    public float wheelRadius = 0.3f;        // Radio aproximado del aro en metros
    public float friction = 2.0f;           // Desaceleración al soltar la rueda

    private Grabbable grabbable;
    private float currentAngularVelocity;   // Velocidad de giro actual (rad/s)
    private Vector3 lastGrabberPosition;
    private bool isGrabbed;

    public float LinearVelocity => currentAngularVelocity * wheelRadius;

    void Awake()
    {
        grabbable = GetComponent<Grabbable>();
    }

    void Update()
    {
        // 1. Detectar si el SDK tiene agarrado este objeto
        if (grabbable != null && grabbable.SelectingPointsCount > 0)
        {
            // Propiedad 'position' en minúscula
            Vector3 currentGrabberPosition = grabbable.SelectingPoints[0].position;

            if (!isGrabbed)
            {
                isGrabbed = true;
                lastGrabberPosition = currentGrabberPosition;
            }
            else
            {
                // Medir desplazamiento relativo al frente de la silla
                Vector3 delta = currentGrabberPosition - lastGrabberPosition;
                float forwardDelta = Vector3.Dot(delta, transform.forward);

                // Calcular velocidad angular basada en el arrastre
                if (Time.deltaTime > 0)
                {
                    currentAngularVelocity = (forwardDelta / wheelRadius) / Time.deltaTime;
                }

                lastGrabberPosition = currentGrabberPosition;
            }
        }
        else
        {
            isGrabbed = false;
            // Aplicar fricción para frenado gradual cuando no se sujeta
            currentAngularVelocity = Mathf.MoveTowards(currentAngularVelocity, 0f, friction * Time.deltaTime);
        }

        // 2. Rotar la malla visual de la rueda sobre su eje X local
        if (wheelMeshPivot != null)
        {
            float degrees = (currentAngularVelocity * Mathf.Rad2Deg) * Time.deltaTime;
            wheelMeshPivot.Rotate(degrees, 0, 0, Space.Self);
        }
    }

    // Permite inyectar velocidad desde el teclado cuando no hay visor conectado
    public void InjectVelocity(float linearSpeed)
    {
        currentAngularVelocity = linearSpeed / wheelRadius;
    }
}