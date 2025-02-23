using DilmerGames.Core.Singletons;
using Unity.Cinemachine;
using UnityEngine;

public class PlayerCameraFollow : Singleton<PlayerCameraFollow>
{
    [SerializeField]
    private float amplitudeGain = 0.5f;

    [SerializeField]
    private float frequencyGain = 0.5f;

    private CinemachineCamera cinemachineVirtualCamera;

    private void Awake()
    {
        cinemachineVirtualCamera = GetComponent<CinemachineCamera>();
    }

    public void FollowPlayer(Transform transform)
    {
        // not all scenes have a cinemachine virtual camera so return in that's the case
        if (cinemachineVirtualCamera == null) return;

        cinemachineVirtualCamera.Follow = transform;
    }
}
