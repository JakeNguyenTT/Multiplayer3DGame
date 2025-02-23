using Unity.Netcode;
using UnityEngine;

public class PlayerManager : Singleton<PlayerManager>
{
    private NetworkVariable<int> m_PlayersInGame = new NetworkVariable<int>();
    public int PlayersInGame => m_PlayersInGame.Value;
    void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
        {
            if (IsServer)
            {
                Logger.Instance.Log($"<color=green>{id} just connected...</color> ");
                m_PlayersInGame.Value++;
            }
        };
        NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
        {
            if (IsServer)
            {
                Logger.Instance.Log($"<color=red>{id} just disconnected...</color> ");
                m_PlayersInGame.Value--;
            }
        };
    }

    void Update()
    {

    }
}
