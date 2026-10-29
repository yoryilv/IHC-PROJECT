using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Trigger de la compuerta: al entrar la silla abre las hojas y muestra el pop-up de
/// selección de escenario, centrado frente a la vista del usuario y mirando hacia él.
/// Requiere: BoxCollider (isTrigger) en este GameObject y el Tag "Player" (o un CharacterController)
/// en la silla.
/// </summary>
public class DoorSelectionTrigger : MonoBehaviour
{
    public const string HotelSceneName = "Hotel_Lobby_Scene";
    public const string CafeteriaSceneName = "Cafeteria_Scene";

    [Header("Referencias de Puerta")]
    public Transform leftDoor;
    public Transform rightDoor;
    public float openDistance = 1.2f;
    public float openSpeed = 2f;

    [Header("Pop-Up de Selección")]
    public GameObject selectionCanvas;
    [Tooltip("Distancia frente a la cámara (m)")]
    public float popupDistance = 1.6f;
    [Tooltip("Escala world-space del canvas (1881x1249 px * 0.0009 ≈ 1.7 x 1.1 m)")]
    public float popupScale = 0.0009f;
    [Tooltip("Offset vertical respecto a la altura de la vista (m)")]
    public float popupHeightOffset = 0f;

    private bool isOpening;
    private bool popupShown;
    private Vector3 leftClosedPos, rightClosedPos, leftOpenPos, rightOpenPos;

    private void Start()
    {
        if (leftDoor != null)
        {
            leftClosedPos = leftDoor.localPosition;
            leftOpenPos = leftClosedPos + new Vector3(-openDistance, 0, 0);
        }
        // Si ambas referencias apuntan al mismo Transform no se anima dos veces
        if (rightDoor != null && rightDoor != leftDoor)
        {
            rightClosedPos = rightDoor.localPosition;
            rightOpenPos = rightClosedPos + new Vector3(openDistance, 0, 0);
        }

        if (selectionCanvas != null)
        {
            WireButtons();
            selectionCanvas.SetActive(false);
        }
    }

    private void Update()
    {
        if (isOpening)
        {
            float t = Time.deltaTime * openSpeed;
            if (leftDoor != null)
                leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, leftOpenPos, t);
            if (rightDoor != null && rightDoor != leftDoor)
                rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, rightOpenPos, t);
        }

        // Atajo con mando: A = Hotel, B = Cafetería (mientras el pop-up está visible)
        if (popupShown)
        {
            if (OVRInput.GetDown(OVRInput.Button.One)) LoadHotelLobby();
            else if (OVRInput.GetDown(OVRInput.Button.Two)) LoadCafeteria();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isOpening) return;
        if (!(other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)) return;

        isOpening = true;
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (selectionCanvas == null) return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 flatFwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (flatFwd.sqrMagnitude < 0.001f) flatFwd = cam.transform.up;
            flatFwd.Normalize();

            Vector3 pos = cam.transform.position + flatFwd * popupDistance;
            pos.y = cam.transform.position.y + popupHeightOffset;

            Transform t = selectionCanvas.transform;
            t.position = pos;
            // El canvas se lee desde el lado -Z: su forward debe apuntar alejándose del usuario
            t.rotation = Quaternion.LookRotation(flatFwd, Vector3.up);
            t.localScale = Vector3.one * popupScale;
        }

        selectionCanvas.SetActive(true);
        popupShown = true;
    }

    /// <summary>Asigna etiquetas y listeners a los dos botones (1º Hotel, 2º Cafetería).</summary>
    private void WireButtons()
    {
        Button[] buttons = selectionCanvas.GetComponentsInChildren<Button>(true);
        if (buttons.Length < 2)
        {
            Debug.LogWarning("[DoorSelectionTrigger] El canvas necesita 2 Buttons (Hotel / Cafetería).", this);
            return;
        }

        Setup(buttons[0], "Hotel Lobby", LoadHotelLobby);
        Setup(buttons[1], "Cafetería", LoadCafeteria);
    }

    private static void Setup(Button b, string label, UnityEngine.Events.UnityAction action)
    {
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(action);
        TMP_Text tmp = b.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = label;
    }

    // --- Métodos para los botones del Pop-Up ---
    public void LoadHotelLobby() => LoadScene(HotelSceneName);
    public void LoadCafeteria() => LoadScene(CafeteriaSceneName);

    private static void LoadScene(string sceneName)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[DoorSelectionTrigger] La escena '{sceneName}' no está en Build Settings (File > Build Profiles > Scene List).");
            return;
        }
        SceneManager.LoadScene(sceneName);
    }
}
