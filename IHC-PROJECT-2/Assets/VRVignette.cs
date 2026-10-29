using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crea un viñeteado dinámico procedural frente a los ojos del usuario en VR.
/// Se oscurece en la periferia al girar o avanzar para eliminar el mareo (motion sickness).
/// </summary>
public class VRVignette : MonoBehaviour
{
    [Header("Sensibilidad")]
    [Tooltip("Intensidad máxima de oscuridad en los bordes (0.7 a 0.95 recomendado)")]
    [Range(0.4f, 1.0f)]
    public float maxDarkness = 0.85f;

    [Tooltip("Velocidad angular (grados/segundo) a la que el viñeteado alcanza su punto máximo")]
    public float turnSpeedThreshold = 35.0f;

    [Tooltip("Suavizado de entrada y salida del efecto")]
    public float transitionSpeed = 8.0f;

    private Image _vignetteImage;
    private float _targetAlpha;
    private float _currentAlpha;
    private float _lastYRotation;

    void Awake()
    {
        SetupVignetteUI();
        _lastYRotation = transform.eulerAngles.y;
    }

    void Update()
    {
        // 1. Calcular la velocidad de giro real de la cabeza / silla
        float currentY = transform.eulerAngles.y;
        float deltaAngle = Mathf.DeltaAngle(_lastYRotation, currentY);
        float currentTurnSpeed = Mathf.Abs(deltaAngle) / Mathf.Max(Time.deltaTime, 0.0001f);
        _lastYRotation = currentY;

        // 2. Calcular qué tan oscuro debe ser el viñeteado
        if (currentTurnSpeed > 5.0f) // Pequeña zona muerta para no activarse con micromovimientos
        {
            float intensity = Mathf.Clamp01(currentTurnSpeed / turnSpeedThreshold);
            _targetAlpha = intensity * maxDarkness;
        }
        else
        {
            _targetAlpha = 0f;
        }

        // 3. Interpolar suavemente el alpha
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, transitionSpeed * Time.deltaTime);

        if (_vignetteImage != null)
        {
            _vignetteImage.color = new Color(0f, 0f, 0f, _currentAlpha);
        }
    }

    /// <summary>
    /// Construye proceduralmente el Canvas y la textura de degradado circular en memoria.
    /// </summary>
    private void SetupVignetteUI()
    {
        // Buscar o anclar a CenterEyeAnchor
        Transform centerEye = transform.Find("OVRCameraRig/TrackingSpace/CenterEyeAnchor");
        if (centerEye == null) centerEye = Camera.main != null ? Camera.main.transform : transform;

        // Crear Canvas en WorldSpace anclado a la cámara central
        GameObject canvasGO = new GameObject("VR_Dynamic_Vignette_Canvas");
        canvasGO.transform.SetParent(centerEye, false);
        canvasGO.transform.localPosition = new Vector3(0, 0, 0.25f); // 25 cm frente a los ojos
        canvasGO.transform.localRotation = Quaternion.identity;
        canvasGO.transform.localScale = Vector3.one * 0.0005f;

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30000;

        RectTransform rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1600, 1200);

        // Crear Imagen con textura procedural radial
        GameObject imageGO = new GameObject("VignetteMask");
        imageGO.transform.SetParent(canvasGO.transform, false);

        _vignetteImage = imageGO.AddComponent<Image>();
        _vignetteImage.rectTransform.sizeDelta = new Vector2(1600, 1200);
        _vignetteImage.sprite = CreateVignetteSprite();
        _vignetteImage.color = new Color(0, 0, 0, 0);
    }

    private Sprite CreateVignetteSprite()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxRadius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                // El centro (0 a 0.45) es 100% transparente. De 0.45 a 1.0 se difumina a negro.
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.95f, dist));
                tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}