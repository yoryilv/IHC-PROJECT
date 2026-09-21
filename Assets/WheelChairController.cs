using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class WheelchairController : MonoBehaviour
{
    [Header("Velocidad")]
    public float moveSpeed = 1.5f;       // ~1.5 m/s (velocidad realista en silla de ruedas)
    public float turnSpeed = 65f;        // Grados por segundo de giro

    [Header("Físicas")]
    public float gravity = -9.81f;

    private CharacterController controller;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // 1. Entrada de control (compatible con mandos Quest y teclado para el Simulador)
        Vector2 leftStick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 rightStick = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);

        float forwardInput = Input.GetAxis("Vertical");
        float turnInput = Input.GetAxis("Horizontal");

        // Priorizar joystick si se detecta movimiento en el mando
        if (Mathf.Abs(leftStick.y) > 0.1f) forwardInput = leftStick.y;
        if (Mathf.Abs(rightStick.x) > 0.1f) turnInput = rightStick.x;

        // 2. Rotación sobre su propio eje (giro de la silla)
        transform.Rotate(0, turnInput * turnSpeed * Time.deltaTime, 0);

        // 3. Desplazamiento frontal
        Vector3 move = transform.forward * (forwardInput * moveSpeed);

        // 4. Gravedad y adherencia a rampas
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // Fuerza descendente leve para no flotar al bajar rampas
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        move.y = verticalVelocity;

        // 5. Aplicar traslación física
        controller.Move(move * Time.deltaTime);
    }
}