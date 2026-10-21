using UnityEngine;
using UnityEngine.SceneManagement;

public class DoorSelectionTrigger : MonoBehaviour
{
    [Header("Referencias de Puerta")]
    public Transform leftDoor;
    public Transform rightDoor;
    public float openDistance = 1.2f;
    public float openSpeed = 2f;

    [Header("Pop-Up de Selección")]
    public GameObject selectionCanvas;

    private bool isOpening = false;
    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private void Start()
    {
        if (leftDoor != null && rightDoor != null)
        {
            leftClosedPos = leftDoor.localPosition;
            rightClosedPos = rightDoor.localPosition;

            // Se abren hacia los lados en su eje X local
            leftOpenPos = leftClosedPos + new Vector3(-openDistance, 0, 0);
            rightOpenPos = rightClosedPos + new Vector3(openDistance, 0, 0);
        }

        if (selectionCanvas != null)
        {
            selectionCanvas.SetActive(false);
        }
    }

    private void Update()
    {
        if (isOpening && leftDoor != null && rightDoor != null)
        {
            leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftOpenPos, Time.deltaTime * openSpeed);
            rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightOpenPos, Time.deltaTime * openSpeed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Detecta a la silla o al jugador
        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            if (!isOpening)
            {
                isOpening = true;

                // Muestra el pop-up en frente
                if (selectionCanvas != null)
                {
                    selectionCanvas.SetActive(true);
                }
            }
        }
    }

    // --- Métodos para los botones del Pop-Up ---
    public void LoadHotelLobby()
    {
        SceneManager.LoadScene("Hotel_Lobby_Scene");
    }

    public void LoadCafeteria()
    {
        SceneManager.LoadScene("Cafeteria_Scene");
    }
}
