using UnityEngine;

public class TelemetryManager : MonoBehaviour
{
    public static TelemetryManager Instance;

    [Header("Métricas de Carrera")]
    public bool isTiming = false;
    public float elapsedTime = 0f;
    public int totalPushes = 0;
    public int collisionCount = 0;

    [Header("Identificador de Ruta")]
    public string currentScenario = "Hotel";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (isTiming)
        {
            elapsedTime += Time.deltaTime;
        }
    }

    public void StartTimer(string scenarioName)
    {
        currentScenario = scenarioName;
        elapsedTime = 0f;
        totalPushes = 0;
        collisionCount = 0;
        isTiming = true;
        Debug.Log($"[TELEMETRÍA] Cronómetro iniciado para: {scenarioName}");
    }

    public void StopTimer()
    {
        if (!isTiming) return;
        isTiming = false;
        Debug.Log($"[TELEMETRÍA] Finalizado. Tiempo: {elapsedTime:F2}s | Impulsos: {totalPushes} | Colisiones: {collisionCount}");
    }

    public void AddPush()
    {
        if (isTiming)
        {
            totalPushes++;
        }
    }

    public void AddCollision()
    {
        if (isTiming)
        {
            collisionCount++;
        }
    }
}