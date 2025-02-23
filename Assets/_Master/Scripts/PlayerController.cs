using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerController : NetworkBehaviour
{
    [Header("Player")]
    [SerializeField] private float m_WalkSpeed = 2f;

    [SerializeField] private float m_RunSpeed = 5.335f;

    [SerializeField] private float m_RotationSpeed = 3.5f;

    [SerializeField] private float m_JumpHeight = 1.2f;

    [SerializeField] private float m_Gravity = -15.00f;
    [SerializeField][Range(0.0f, 0.3f)] private float RotationSmoothTime = 0.12f;
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

    [Header("Cinemachine")]
    [SerializeField] private GameObject m_CinemachineCameraTarget;

    [SerializeField] private float m_TopClamp = 70.0f;

    [SerializeField] private float m_BottomClamp = -30.0f;

    [SerializeField] private float m_CameraAngleOverride = 0.0f;

    [SerializeField] private bool m_LockCameraPosition = false;

    [Header("Read Only")]
    [SerializeField] private float m_Speed = 0;
    [SerializeField] private bool m_IsGrounded = false;
    [SerializeField] private float m_VerticalVelocity = 0;
    [SerializeField] private float m_RotationVelocity;
    [SerializeField] private PlayerState m_OldPlayerState = PlayerState.Idle;

    [Header("Cinemachine")]
    [SerializeField] private float m_CinemachineTargetYaw;
    [SerializeField] private float m_CinemachineTargetPitch;
    [Header("Client Caches")]
    [SerializeField] private Vector3 m_OldInputPosition = Vector3.zero;
    [SerializeField] private Vector3 m_OldInputRotation = Vector3.zero;

    private CharacterController m_CharacterController;

    private Animator m_Animator;
    private const float m_Threshold = 0.01f;

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
            m_CinemachineTargetYaw = m_CinemachineCameraTarget.transform.rotation.eulerAngles.y;
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

    void LateUpdate()
    {
        CameraRotation();
    }

    private void CameraRotation()
    {
        // if there is an input and camera position is not fixed
        if (Input.mousePosition.sqrMagnitude >= m_Threshold && !m_LockCameraPosition)
        {
            //Don't multiply mouse input by Time.deltaTime;
            float deltaTimeMultiplier = 1.0f;

            m_CinemachineTargetYaw += Input.mousePosition.x * deltaTimeMultiplier;
            m_CinemachineTargetPitch += Input.mousePosition.y * deltaTimeMultiplier;
        }

        // clamp our rotations so our values are limited 360 degrees
        m_CinemachineTargetYaw = ClampAngle(m_CinemachineTargetYaw, float.MinValue, float.MaxValue);
        m_CinemachineTargetPitch = ClampAngle(m_CinemachineTargetPitch, m_BottomClamp, m_TopClamp);

        m_CinemachineCameraTarget.transform.rotation = Quaternion.Euler(m_CinemachineTargetPitch + m_CameraAngleOverride,
            m_CinemachineTargetYaw, 0.0f);
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
            transform.rotation = Quaternion.Euler(m_NetworkRotationDirection.Value);
            // transform.Rotate(m_NetworkRotationDirection.Value, Space.World);
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
        JumpInput();

        var horizontal = Input.GetAxis("Horizontal");
        var vertical = Input.GetAxis("Vertical");

        // change fighting states
        if (ActivePunchActionKey() && vertical == 0 && horizontal == 0)
        {
            UpdatePlayerStateServerRpc(PlayerState.Punch);
            return;
        }

        float targetSpeed = ActiveRunningActionKey() ? m_RunSpeed : m_WalkSpeed;
        if (horizontal == 0 && vertical == 0) targetSpeed = 0.0f;

        float currentSpeed = new Vector3(m_CharacterController.velocity.x, 0.0f, m_CharacterController.velocity.z).magnitude;
        float speedOffset = 0.1f;
        // if (currentSpeed < targetSpeed - speedOffset ||
        //     currentSpeed > targetSpeed + speedOffset)
        // {
        //     m_Speed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 10);
        //     m_Speed = Mathf.Round(currentSpeed * 1000f) / 1000f;
        // }
        // else
        {
            m_Speed = targetSpeed;
        }

        Vector3 inputPosition = Vector3.zero;
        Vector3 inputRotation = Vector3.zero;

        Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

        if (vertical != 0 || horizontal != 0)
        {
            float targetRotation = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + Camera.main.transform.eulerAngles.y;
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref m_RotationVelocity, RotationSmoothTime);

            inputRotation = new Vector3(0.0f, rotation, 0.0f);
            inputPosition = Quaternion.Euler(inputRotation) * Vector3.forward;

            // left & right rotation
            // Vector3 inputRotation = new Vector3(0, horizontal, 0);

            // // forward & backward direction
            // Vector3 direction = transform.TransformDirection(Vector3.forward);
            // Vector3 inputPosition = direction * vertical;

            if (m_IsGrounded)
            {
                if (m_NetworkPlayerState.Value != PlayerState.Walk && !ActiveRunningActionKey())
                {
                    UpdatePlayerStateServerRpc(PlayerState.Walk);
                }
                else if (m_NetworkPlayerState.Value != PlayerState.Run && ActiveRunningActionKey())
                {
                    UpdatePlayerStateServerRpc(PlayerState.Run);
                }
            }
        }
        else
        {
            if (m_IsGrounded)
            {
                UpdatePlayerStateServerRpc(PlayerState.Idle);
            }
        }

        // let server know about position and rotation client changes
        if (m_OldInputPosition != inputPosition ||
            m_OldInputRotation != inputRotation ||
            m_VerticalVelocity != 0)
        {
            m_OldInputPosition = inputPosition;
            m_OldInputRotation = inputRotation;
            UpdateClientPositionAndRotationServerRpc((inputPosition * m_Speed + Vector3.up * m_VerticalVelocity) * Time.deltaTime, inputRotation);
        }

        // change motion states
        // if (vertical == 0)
        //     UpdatePlayerStateServerRpc(PlayerState.Idle);
        // else if (!ActiveRunningActionKey() && vertical > 0 && vertical <= 1)
        //     UpdatePlayerStateServerRpc(PlayerState.Walk);
        // else if (ActiveRunningActionKey() && vertical > 0 && vertical <= 1)
        // {
        //     inputPosition = direction * m_RunSpeedOffset;
        //     UpdatePlayerStateServerRpc(PlayerState.Run);
        // }
        // else if (vertical < 0)
        //     UpdatePlayerStateServerRpc(PlayerState.ReverseWalk);

        // // let server know about position and rotation client changes
        // if (m_OldInputPosition != inputPosition ||
        //     m_OldInputRotation != inputRotation ||
        //     m_VerticalVelocity != 0)
        // {
        //     m_OldInputPosition = inputPosition;
        //     m_OldInputRotation = inputRotation;
        //     UpdateClientPositionAndRotationServerRpc((inputPosition * m_WalkSpeed + Vector3.up * m_VerticalVelocity) * Time.deltaTime, inputRotation * m_RotationSpeed);
        // }
    }

    void JumpInput()
    {
        if (m_IsGrounded)
        {
            m_Animator.ResetTrigger("Jump");
            if (ActiveJumpActionKey())
            {
                m_VerticalVelocity = Mathf.Sqrt(m_JumpHeight * -2f * m_Gravity);
                UpdatePlayerStateServerRpc(PlayerState.Jump);
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
        Debug.Log($"State: <color=red>{state}</color>");
        m_NetworkPlayerState.Value = state;
        if (state == PlayerState.Punch)
        {
            m_NetworkPlayerPunchBlend.Value = Random.Range(0.0f, 1.0f);
        }
    }

    private void GroundedCheck()
    {
        Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - m_GroundedOffset, transform.position.z);
        m_IsGrounded = Physics.CheckSphere(spherePosition, m_GroundedRadius, m_GroundedLayers, QueryTriggerInteraction.Ignore);

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

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
}
