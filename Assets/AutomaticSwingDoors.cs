using UnityEngine;

public class AutomaticSwingDoors : MonoBehaviour
{
    [Header("Bisagras (Pivots)")]
    public Transform leftDoorHinge;
    public Transform rightDoorHinge;

    [Header("Ángulos de Apertura")]
    public float leftOpenAngle = 90f;   // Si abre hacia afuera, cámbialo a -90
    public float rightOpenAngle = -90f; // Si abre hacia afuera, cámbialo a 90
    public float swingSpeed = 3f;

    [Header("Prueba manual")]
    public bool testOpen = false;

    private Quaternion leftClosedRot;
    private Quaternion rightClosedRot;
    private bool isPlayerInside = false;

    void Start()
    {
        // Guardamos la rotación GLOBAL inicial, ignorando si el FBX viene torcido
        if (leftDoorHinge != null) leftClosedRot = leftDoorHinge.rotation;
        if (rightDoorHinge != null) rightClosedRot = rightDoorHinge.rotation;
    }

    void Update()
    {
        bool shouldOpen = isPlayerInside || testOpen;

        if (leftDoorHinge != null)
        {
            // Al multiplicar Quaternion.AngleAxis por la izquierda con Vector3.up,
            // forzamos la rotación batiente perfecta sobre el eje vertical del mundo.
            Quaternion targetL = Quaternion.AngleAxis(shouldOpen ? leftOpenAngle : 0f, Vector3.up) * leftClosedRot;
            leftDoorHinge.rotation = Quaternion.Slerp(leftDoorHinge.rotation, targetL, Time.deltaTime * swingSpeed);
        }

        if (rightDoorHinge != null)
        {
            Quaternion targetR = Quaternion.AngleAxis(shouldOpen ? rightOpenAngle : 0f, Vector3.up) * rightClosedRot;
            rightDoorHinge.rotation = Quaternion.Slerp(rightDoorHinge.rotation, targetR, Time.deltaTime * swingSpeed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            isPlayerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            isPlayerInside = false;
        }
    }
}Wheel