using Unity.Cinemachine;
using UnityEngine;

public class CinemachineRoot : MonoBehaviour
{
    [SerializeField] private Transform m_PlayerCameraRoot;
    private CinemachineCamera m_CinemachineCamera;

    void Start()
    {
        m_CinemachineCamera = Camera.main.GetComponent<CinemachineCamera>();
        if (m_CinemachineCamera != null)
        {
            m_CinemachineCamera.Follow = m_PlayerCameraRoot;
        }
        else
        {
            Debug.LogError("CinemachineCamera not found");
        }
    }
}
