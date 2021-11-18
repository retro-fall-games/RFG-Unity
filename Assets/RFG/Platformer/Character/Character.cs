using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RFG
{
  using SceneGraph;

  public enum CharacterType { Player, AI }
  [AddComponentMenu("RFG/Platformer/Character/Character")]
  public class Character : MonoBehaviour, IPooledObject
  {
    [Header("Type")]
    public CharacterType CharacterType = CharacterType.Player;

    [Header("Location")]
    public Transform SpawnAt;

    [Header("Settings")]
    public InputPack InputPack;
    public SettingsPack SettingsPack;

    [Header("Character State")]
    public RFG.StateMachine CharacterState;

    [Header("Movement State")]
    public RFG.StateMachine MovementState;

    [field: SerializeField] public bool IsReady { get; set; } = false;

    [HideInInspector]
    public StateCharacterContext Context => _characterContext;
    public CharacterController2D Controller => _controller;
    private StateCharacterContext _characterContext = new StateCharacterContext();
    private CharacterController2D _controller;
    private List<Component> _abilities;
    private List<SceneDoor> _sceneDoors;

    #region Unity Methods
    private void Awake()
    {
      InitContext();
      InitAbilities();
      IsReady = true;
    }

    private void Update()
    {
      CharacterState.Update();
      MovementState.Update();
    }

    private void OnEnable()
    {
      EnableAllInput(true);
      EnableInput(InputPack.PauseInput, true);
    }

    private void OnDisable()
    {
      EnableAllInput(false);
    }
    #endregion

    #region Init
    private void InitContext()
    {
      _characterContext = new StateCharacterContext();
      _controller = GetComponent<CharacterController2D>();
      _characterContext.transform = transform;
      _characterContext.animator = GetComponent<Animator>();
      _characterContext.character = this;
      _characterContext.controller = _controller;
      _characterContext.inputPack = InputPack;
      _characterContext.DefaultSettingsPack = SettingsPack;
      _characterContext.healthBehaviour = GetComponent<HealthBehaviour>();

      // Bind the character context to the state context
      CharacterState.Init();
      CharacterState.Bind(_characterContext);

      MovementState.Init();
      MovementState.Bind(_characterContext);
    }

    public void OnObjectSpawn(params object[] objects)
    {
      CharacterState.ResetToDefaultState();
      MovementState.ResetToDefaultState();
    }
    #endregion

    #region Kill
    public void Kill()
    {
      CharacterState.ChangeState(typeof(DeathState));
    }
    #endregion

    #region Spawning
    public void Respawn()
    {
      StartCoroutine(RespawnCo());
    }

    public IEnumerator RespawnCo()
    {
      yield return new WaitForSecondsRealtime(1f);
      CharacterState.ChangeState(typeof(SpawnState));
    }

    public void SetSpawnPosition()
    {
      // Default to the postion they are currently at
      SpawnAt = transform;

      if (CharacterType == CharacterType.Player)
      {
        // If the character is a player and there is a current checkpoint
        if (Checkpoint.currentCheckpoint != null)
        {
          SpawnAt.position = Checkpoint.currentCheckpoint.transform.position;
        }
        else
        {
          // Else if there is no current checkpoint check to see if the came from a scene door
          // If they have spawn at that scene door
          SceneDoor toSceneDoor = SceneGraphManager.Instance.FindToSceneDoor();
          if (toSceneDoor != null)
          {
            SpawnAt.position = toSceneDoor.transform.position + toSceneDoor.spawnOffset;
          }
        }
      }
    }
    #endregion

    #region Abilities
    private void InitAbilities()
    {
      Component[] abilities = GetComponents(typeof(IAbility)) as Component[];
      if (abilities.Length > 0)
      {
        _abilities = new List<Component>(abilities);
      }
    }

    public void EnableAllAbilities(bool enabled, Behaviour except = null)
    {
      if (_abilities != null)
      {
        foreach (Behaviour ability in _abilities)
        {
          if (ability.Equals(except))
          {
            continue;
          }
          ability.enabled = enabled;
        }
      }
    }
    #endregion

    #region Input
    public void EnableAllInput(bool enabled)
    {
      EnableInput(InputPack?.Movement, enabled);
      EnableInput(InputPack?.RunInput, enabled);
      EnableInput(InputPack?.CrouchInput, enabled);
      EnableInput(InputPack?.JumpInput, enabled);
      EnableInput(InputPack?.DashInput, enabled);
      EnableInput(InputPack?.PrimaryAttackInput, enabled);
      EnableInput(InputPack?.SecondaryAttackInput, enabled);
      EnableInput(InputPack?.SmashDownInput, enabled);
      EnableInput(InputPack?.SlideInput, enabled);
      EnableInput(InputPack?.UseInput, enabled);
    }

    public void EnableInput(InputActionReference reference, bool enabled)
    {
      if (enabled)
      {
        reference?.action.Enable();
      }
      else
      {
        reference?.action.Disable();
      }
    }

    public void OverrideInputPack(InputPack input)
    {
    }

    public void ResetInputPack()
    {
    }
    #endregion

    #region Settings
    public void OverrideSettingsPack(SettingsPack settings)
    {
      _characterContext.OverrideSettingsPack(settings);
    }

    public void ResetSettingsPack()
    {
      _characterContext.ResetSettingsPack();
    }
    #endregion

    #region State Helpers
    public void FreezeCharacterState()
    {
      CharacterState.Frozen = true;
    }

    public void UnFreezeCharacterState()
    {
      CharacterState.Frozen = false;
    }

    public void FreezeMovementState()
    {
      MovementState.Frozen = true;
    }

    public void UnFreezeMovementState()
    {
      MovementState.Frozen = false;
    }
    #endregion

    #region Character State Helpers
    public bool IsAlive => CharacterState.CurrentStateType == typeof(AliveState);
    #endregion

    #region Movement State Helpers

    public void SetMovementStatePack(RFG.StatePack statePack)
    {
      MovementState.SetStatePack(statePack);
    }

    public void RestoreDefaultMovementStatePack()
    {
      MovementState.RestoreDefaultStatePack();
    }

    public bool IsGrounded => _controller.State.IsGrounded || _controller.State.JustGotGrounded;

    public bool IsInGroundMovementState =>
    (
      IsGrounded ||
      MovementState.IsInState(
        typeof(IdleState),
        typeof(LandedState),
        typeof(WalkingState),
        typeof(RunningState),
        typeof(WalkingUpSlopeState),
        typeof(RunningUpSlopeState),
        typeof(WalkingDownSlopeState),
        typeof(RunningDownSlopeState)
      )
    );


    public bool IsInSlopeMovementState =>
      MovementState.IsInState(
        typeof(WalkingUpSlopeState),
        typeof(RunningUpSlopeState),
        typeof(WalkingDownSlopeState),
        typeof(RunningDownSlopeState)
      );

    public bool IsJumping => (
      MovementState.IsInState(
        typeof(JumpingState),
        typeof(JumpingFlipState),
        typeof(DoubleJumpState)
      )
    );

    public bool IsInAirMovementState => (
      !IsGrounded &&
      MovementState.IsInState(
        typeof(FallingState),
        typeof(JumpingState),
        typeof(JumpingFlipState),
        typeof(DoubleJumpState)
      )
    );

    public bool IsInCrouchMovementState => MovementState.CurrentStateType == typeof(CrouchWalkingState);
    public bool IsIdle => MovementState.CurrentStateType == typeof(IdleState);
    public bool IsDashing => MovementState.CurrentStateType == typeof(DashingState);
    public bool IsWallClinging => MovementState.CurrentStateType == typeof(WallClingingState);
    public bool IsDangling => MovementState.CurrentStateType == typeof(DanglingState);
    public bool IsSliding => MovementState.CurrentStateType == typeof(SlidingState);
    public bool IsLedgeGrabbing => MovementState.CurrentStateType == typeof(LedgeGrabState);
    public bool IsSwimming => MovementState.CurrentStateType == typeof(SwimmingState);
    public bool IsPushing => MovementState.CurrentStateType == typeof(PushingState);
    public bool IsLadderCliming => MovementState.IsInState(typeof(LadderClimbingState), typeof(LadderIdleState));
    public bool IsAnyPrimaryAttack => MovementState.IsInState(typeof(PrimaryAttackStartedState), typeof(PrimaryAttackCanceledState), typeof(PrimaryAttackPerformedState));
    public bool IsAnySecondaryAttack => MovementState.IsInState(typeof(SecondaryAttackStartedState), typeof(SecondaryAttackCanceledState), typeof(SecondaryAttackPerformedState));
    public bool IsAnySmashingDown => MovementState.IsInState(typeof(SmashDownStartedState), typeof(SmashDownCollidedState), typeof(SmashDownPerformedState));
    #endregion
  }
}