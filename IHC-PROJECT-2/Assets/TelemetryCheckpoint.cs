using UnityEngine;

public class TelemetryCheckpoint : MonoBehaviour
{
    public enum CheckpointType { StartPoint, FinishPoint }
    
    [Header("Configuración")]
    public CheckpointType checkpointType = CheckpointType.StartPoint;
    public string scenarioName = "Hotel";

    private void OnTriggerEnter(Collider other)
    {
        // Detecta el CharacterController de la silla o cualquier colisionador del Player
        if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || other.GetComponentInParent<CharacterController>() != null)
        {
            if (checkpointType == CheckpointType.StartPoint)
            {
                TelemetryManager.Instance.StartTimer(scenarioName);
            }
            else if (checkpointType == CheckpointType.FinishPoint)
            {
                TelemetryManager.Instance.StopTimer();
            }
        }
    }
}