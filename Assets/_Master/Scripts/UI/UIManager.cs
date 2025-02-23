using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Button m_StartHostButton;
    [SerializeField] private Button m_StartServerButton;
    [SerializeField] private Button m_StartClientButton;
    [SerializeField] private TextMeshProUGUI m_PlayerInGameText;

    void Awake()
    {
        Cursor.visible = true;
    }

    void Update()
    {
        m_PlayerInGameText.text = $"Players in game: {PlayerManager.Instance.PlayersInGame}";
    }

    void Start()
    {
        m_StartHostButton.onClick.AddListener(StartHost);
        m_StartServerButton.onClick.AddListener(StartServer);
        m_StartClientButton.onClick.AddListener(StartClient);
    }

    void StartHost()
    {
        if (NetworkManager.Singleton.StartHost())
        {
            Logger.Instance.LogInfo("Host started...");
        }
        else
        {
            Logger.Instance.LogError("Host could not be started...");
        }
    }

    void StartServer()
    {
        if (NetworkManager.Singleton.StartServer())
        {
            Logger.Instance.LogInfo("Server started...");
        }
        else
        {
            Logger.Instance.LogError("Server could not be started...");
        }
    }

    void StartClient()
    {
        if (NetworkManager.Singleton.StartClient())
        {
            Logger.Instance.LogInfo("Client started...");
        }
        else
        {
            Logger.Instance.LogError("Client could not be started...");
        }
    }
}
