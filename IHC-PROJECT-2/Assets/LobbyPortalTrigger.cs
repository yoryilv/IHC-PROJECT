using UnityEngine;

public class LobbyPortalTrigger : MonoBehaviour
{
    [Header("Interfaz UI")]
    [Tooltip("El objeto PopUp_Seleccion")]
    public GameObject popUpSeleccion;

    [Header("Jugador y Destinos")]
    public CharacterController playerController;
    public Transform spawnHotel;
    public Transform spawnCafeteria;

    private void Start()
    {
        if (popUpSeleccion != null)
        {
            popUpSeleccion.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other) && popUpSeleccion != null)
        {
            popUpSeleccion.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other) && popUpSeleccion != null)
        {
            popUpSeleccion.SetActive(false);
        }
    }

    public void TeleportToHotel()
    {
        ExecuteTeleport(spawnHotel, "Hotel");
    }

    public void TeleportToCafeteria()
    {
        ExecuteTeleport(spawnCafeteria, "Cafeteria");
    }

    private void ExecuteTeleport(Transform targetSpawn, string scenarioName)
    {
        if (popUpSeleccion != null)
            popUpSeleccion.SetActive(false);

        if (targetSpawn != null && playerController != null)
        {
            // Desactivar temporalmente para que el motor de físicas permita la nueva coordenada
            playerController.enabled = false;
            playerController.transform.position = targetSpawn.position;
            playerController.transform.rotation = targetSpawn.rotation;
            playerController.enabled = true;
        }

        if (TelemetryManager.Instance != null)
        {
            TelemetryManager.Instance.StartTimer(scenarioName);
        }
    }

    private bool IsPlayer(Collider col)
    {
        return col.CompareTag("Player") 
            || col.GetComponent<CharacterController>() != null 
            || col.GetComponentInParent<CharacterController>() != null;
    }
}