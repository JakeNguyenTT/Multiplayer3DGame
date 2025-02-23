using TMPro;
using Unity.Netcode;

public class PlayerHUD : NetworkBehaviour
{
    private NetworkVariable<NetworkString> m_PlayerName = new NetworkVariable<NetworkString>();
    private TextMeshProUGUI m_LocalPlayerOverlay;
    private bool m_OverlaySet = false;

    void Awake()
    {
        m_LocalPlayerOverlay = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Update()
    {
        if (!m_OverlaySet && !string.IsNullOrEmpty(m_PlayerName.Value))
        {
            SetOverlay();
            m_OverlaySet = true;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            m_PlayerName.Value = $"Player {OwnerClientId}";
        }
    }

    void SetOverlay()
    {
        if (m_LocalPlayerOverlay == null)
        {
            m_LocalPlayerOverlay = GetComponentInChildren<TextMeshProUGUI>();
        }
        m_LocalPlayerOverlay.text = m_PlayerName.Value;
    }
}
