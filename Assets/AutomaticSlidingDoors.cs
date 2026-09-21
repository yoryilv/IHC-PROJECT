using UnityEngine;

public class AutomaticSlidingDoors : MonoBehaviour
{
    [Header("Referencias de las puertas")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Parámetros de Apertura")]
    public float openDistance = 1.3f; // Distancia de apertura hacia cada lado
    public float slideSpeed = 2.5f;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;
    private bool isOpen = false;

    void Start()
    {
        if (leftDoor != null)
        {
            leftClosedPos = leftDoor.localPosition;
            // Desplaza la hoja izquierda hacia su izquierda local
            leftOpenPos = leftClosedPos + new Vector3(-openDistance, 0, 0);
        }

        if (rightDoor != null)
        {
            rightClosedPos = rightDoor.localPosition;
            // Desplaza la hoja derecha hacia su derecha local
            rightOpenPos = rightClosedPos + new Vector3(openDistance, 0, 0);
        }
    }

    void Update()
    {
        if (leftDoor == null || rightDoor == null) return;

        Vector3 targetLeft = isOpen ? leftOpenPos : leftClosedPos;
        Vector3 targetRight = isOpen ? rightOpenPos : rightClosedPos;

        leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, targetLeft, Time.deltaTime * slideSpeed);
        rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, targetRight, Time.deltaTime * slideSpeed);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Detecta al jugador (por Tag o por CharacterController)
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            isOpen = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
        {
            isOpen = false;
        }
    }
}