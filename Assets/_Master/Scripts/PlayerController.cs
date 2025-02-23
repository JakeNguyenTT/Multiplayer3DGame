using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerController : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private float m_WalkSpeed = 3.5f;

    [SerializeField] private float m_RunSpeedOffset = 2.0f;

    [SerializeField] private float m_RotationSpeed = 3.5f;

    [SerializeField] private float m_JumpHeight = 1.2f;

    [SerializeField] private float m_Gravity = -15.00f;

    [SerializeField] private Vector2 m_DefaultInitialPositionOnPlane = new Vector2(-4, 4);

    [Header("Grounded")]
    [SerializeField] private float m_GroundedOffset = -0.14f;
    [SerializeField] private float m_GroundedRadius = 0.28f;
    [SerializeField] private LayerMask m_GroundedLayers;

    [Header("Network Variables")]
    [SerializeField] private NetworkVariable<Vector3> m_NetworkPositionDirection = new NetworkVariable<Vector3>();

    [SerializeField] private NetworkVariable<Vector3> m_NetworkRotationDirection = new NetworkVariable<Vector3>();

    [SerializeField] private NetworkVariable<PlayerState> m_NetworkPlayerState = new NetworkVariable<PlayerState>();

    [SerializeField] private NetworkVariable<float> m_NetworkPlayerHealth = new NetworkVariable<float>(1000);

    [SerializeField] private NetworkVariable<float> m_NetworkPlayerPunchBlend = new NetworkVariable<float>();

    [Header("Punch")]
    [SerializeField] private GameObject m_LeftHand;

    [SerializeField] private GameObject m_RightHand;

    [SerializeField] private float m_MinPunchDistance = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioClip m_LandingAudioClip;
    [SerializeField] private AudioClip[] m_FootstepAudioClips;
    [SerializeField] private float m_FootstepAudioVolume = 0.5f;



    [Header("Read Only")]

    [SerializeField] private bool m_IsGrounded = false;
    [SerializeField] private float m_VerticalVelocity = 0;
    [SerializeField] private PlayerState m_OldPlayerState = PlayerState.Idle;
    // client caches positions
    [SerializeField] private Vector3 m_OldInputPosition = Vector3.zero;
    [SerializeField] private Vector3 m_OldInputRotation = Vector3.zero;

    private CharacterController m_CharacterController;

    private Animator m_Animator;


    private void Awake()
    {
        m_CharacterController = GetComponent<CharacterController>();
        m_Animator = GetComponent<Animator>();
    }

    void Start()
    {
        if (IsClient && IsOwner)
        {
            transform.position = new Vector3(Random.Range(m_DefaultInitialPositionOnPlane.x, m_DefaultInitialPositionOnPlane.y), 0,
                   Random.Range(m_DefaultInitialPositionOnPlane.x, m_DefaultInitialPositionOnPlane.y));

            PlayerCameraFollow.Instance.FollowPlayer(transform.Find("PlayerCameraRoot"));
        }
    }

    void Update()
    {
        if (IsClient && IsOwner)
        {
            GroundedCheck();
            ClientInput();
        }
        ClientMoveAndRotate();
        ClientVisuals();
    }

    private void FixedUpdate()
    {
        if (IsClient && IsOwner)
        {
            if (m_NetworkPlayerState.Value == PlayerState.Punch && ActivePunchActionKey())
            {
                CheckPunch(m_LeftHand.transform, Vector3.up);
                CheckPunch(m_RightHand.transform, Vector3.down);
            }
        }
    }

    private void CheckPunch(Transform hand, Vector3 aimDirection)
    {
        RaycastHit hit;

        int layerMask = LayerMask.GetMask("Player");

        if (Physics.Raycast(hand.position, hand.transform.TransformDirection(aimDirection), out hit, m_MinPunchDistance, layerMask))
        {
            Debug.DrawRay(hand.position, hand.transform.TransformDirection(aimDirection) * m_MinPunchDistance, Color.yellow);

            var playerHit = hit.transform.GetComponent<NetworkObject>();
            if (playerHit != null)
            {
                UpdateHealthServerRpc(1, playerHit.OwnerClientId);
            }
        }
        else
        {
            Debug.DrawRay(hand.position, hand.transform.TransformDirection(aimDirection) * m_MinPunchDistance, Color.red);
        }
    }


    private void ClientMoveAndRotate()
    {
        if (m_NetworkPositionDirection.Value != Vector3.zero)
        {
            m_CharacterController.Move(m_NetworkPositionDirection.Value);
        }
        if (m_NetworkRotationDirection.Value != Vector3.zero)
        {
            transform.Rotate(m_NetworkRotationDirection.Value, Space.World);
        }
    }

    private void ClientVisuals()
    {
        if (m_OldPlayerState != m_NetworkPlayerState.Value)
        {
            m_OldPlayerState = m_NetworkPlayerState.Value;
            m_Animator.SetTrigger($"{m_NetworkPlayerState.Value}");
            if (m_NetworkPlayerState.Value == PlayerState.Punch)
            {
                m_Animator.SetFloat($"{m_NetworkPlayerState.Value}Blend", m_NetworkPlayerPunchBlend.Value);
            }
        }
    }

    private void ClientInput()
    {
        // left & right rotation
        Vector3 inputRotation = new Vector3(0, Input.GetAxis("Horizontal"), 0);

        // forward & backward direction
        Vector3 direction = transform.TransformDirection(Vector3.forward);
        float forwardInput = Input.GetAxis("Vertical");
        Vector3 inputPosition = direction * forwardInput;

        if (m_IsGrounded)
        {
            if (ActiveJumpActionKey())
            {
                m_VerticalVelocity = Mathf.Sqrt(m_JumpHeight * -2f * m_Gravity);
                //UpdatePlayerStateServerRpc(PlayerState.Jump);
            }
            if (m_VerticalVelocity < 0.0f)
            {
                m_VerticalVelocity = -2f;
            }
        }
        else
        {
            m_VerticalVelocity += m_Gravity * Time.deltaTime;
        }

        // change fighting states
        if (ActivePunchActionKey() && forwardInput == 0)
        {
            UpdatePlayerStateServerRpc(PlayerState.Punch);
            return;
        }

        // change motion states
        if (forwardInput == 0)
            UpdatePlayerStateServerRpc(PlayerState.Idle);
        else if (!ActiveRunningActionKey() && forwardInput > 0 && forwardInput <= 1)
            UpdatePlayerStateServerRpc(PlayerState.Walk);
        else if (ActiveRunningActionKey() && forwardInput > 0 && forwardInput <= 1)
        {
            inputPosition = direction * m_RunSpeedOffset;
            UpdatePlayerStateServerRpc(PlayerState.Run);
        }
        else if (forwardInput < 0)
            UpdatePlayerStateServerRpc(PlayerState.ReverseWalk);

        // let server know about position and rotation client changes
        if (m_OldInputPosition != inputPosition ||
            m_OldInputRotation != inputRotation ||
            m_VerticalVelocity != 0)
        {
            m_OldInputPosition = inputPosition;
            m_OldInputRotation = inputRotation;
            UpdateClientPositionAndRotationServerRpc((inputPosition * m_WalkSpeed + Vector3.up * m_VerticalVelocity) * Time.deltaTime, inputRotation * m_RotationSpeed);
        }
    }

    private static bool ActiveRunningActionKey()
    {
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private static bool ActivePunchActionKey()
    {
        return Input.GetKey(KeyCode.C);
    }

    private static bool ActiveJumpActionKey()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    [ServerRpc]
    public void UpdateClientPositionAndRotationServerRpc(Vector3 newPosition, Vector3 newRotation)
    {
        m_NetworkPositionDirection.Value = newPosition;
        m_NetworkRotationDirection.Value = newRotation;
    }

    [ServerRpc]
    public void UpdateHealthServerRpc(int takeAwayPoint, ulong clientId)
    {
        var clientWithDamaged = NetworkManager.Singleton.ConnectedClients[clientId]
            .PlayerObject.GetComponent<PlayerController>();

        if (clientWithDamaged != null && clientWithDamaged.m_NetworkPlayerHealth.Value > 0)
        {
            clientWithDamaged.m_NetworkPlayerHealth.Value -= takeAwayPoint;
        }

        // execute method on a client getting punch
        NotifyHealthChangedClientRpc(takeAwayPoint, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        });
    }

    [ClientRpc]
    public void NotifyHealthChangedClientRpc(int takeAwayPoint, ClientRpcParams clientRpcParams = default)
    {
        if (IsOwner) return;

        Logger.Instance.LogInfo($"Client got punch {takeAwayPoint}");
    }

    [ServerRpc]
    public void UpdatePlayerStateServerRpc(PlayerState state)
    {
        m_NetworkPlayerState.Value = state;
        if (state == PlayerState.Punch)
        {
            m_NetworkPlayerPunchBlend.Value = Random.Range(0.0f, 1.0f);
        }
    }

    private void GroundedCheck()
    {
        // set sphere position, with offset
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - m_GroundedOffset, transform.position.z);
        m_IsGrounded = Physics.CheckSphere(spherePosition, m_GroundedRadius, m_GroundedLayers, QueryTriggerInteraction.Ignore);

        // update animator if using character
        if (m_Animator)
        {
            m_Animator.SetBool("Grounded", m_IsGrounded);
        }
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            if (m_FootstepAudioClips.Length > 0)
            {
                var index = Random.Range(0, m_FootstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(m_FootstepAudioClips[index], transform.TransformPoint(m_CharacterController.center), m_FootstepAudioVolume);
            }
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            AudioSource.PlayClipAtPoint(m_LandingAudioClip, transform.TransformPoint(m_CharacterController.center), m_FootstepAudioVolume);
        }
    }
}
