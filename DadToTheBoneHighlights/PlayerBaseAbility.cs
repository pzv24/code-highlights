
using System.Collections.Generic;
using Dads.Actors;
using UnityEngine;
namespace Dads.Abilities.PlayerAbilities
{
    public abstract class PlayerBaseAbility: MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] protected PlayerAbilityDefinition _abilityDef;
        [SerializeField] protected bool _isDebugEnabled;

        [SerializeField] private List<Buff> _buffs = new List<Buff>();

        [SerializeField] protected bool _isAbilityEnabled;
        [SerializeField] private bool _isAbilityUnlocked = true;

        // private variables
        protected int _activeAbilityProjectiles;
        private bool _hasActiveTarget;
        private float _cooldownTimerStart;
        protected List<PlayerReference> _players = new List<PlayerReference>();
        private bool _requierePlayerAlive;

        //delegate methods that stacks the ability activation behaviors
        //check if is cast available in all conditions
        private delegate void IsCastAvailable(ref bool isAvailable);
        //process the update for the ability 
        private delegate void OnProjectileDestroyedDelegate();
        //runs after the cast
        private delegate void AfterCast();

        private IsCastAvailable _isCastAvailableHandler;
        private OnProjectileDestroyedDelegate _projectileDestroyedHandler;
        private AfterCast _afterCastHandler;

        public List<PlayerReference> Players => _players;
        public string Name => _abilityDef.AbilityName;
        public ScalableAttributesEnum ScalableAttributes => _abilityDef.AbilityScalableAttributes;
        public bool IsAbilityShared => _abilityDef.AbilityIsShared;
        public bool IsAbilityUnlocked => _isAbilityUnlocked;

        public List<Buff> EquippedBuffs => _buffs;

        public PlayerAbilityCategoryEnum Category => _abilityDef._category;
        
        protected Actor _player;
        protected Actor _otherPlayer;


        protected virtual void Awake()
        {
            _players.Add(_abilityDef._player1ref);
            _players.Add(_abilityDef._player2ref);
            VerifyIfAttachedToPlayer();
            BuildAbilityConstraints();
            VerifyEquippedBuffs();
            CalculateAllScalableAttributes();
        }

        private void OnValidate()
        {
            //if something is changed on inspector, updates the constraints
            //BuildAbilityConstraints();
            VerifyEquippedBuffs();
            OnBuffed();
        }
        //verifies if the ability is attached to player and determine players
        private void VerifyIfAttachedToPlayer()
        {
            if (TryGetComponent(out Actor attachedActor))
            {
                var p1 = _abilityDef._player1ref.Actor;
                var p2 = _abilityDef._player2ref.Actor;
                var currentIsP1 = attachedActor.IsSamePlayer(p1);
                _player = currentIsP1 ? p1: p2;
                _otherPlayer = currentIsP1 ? p2: p1;
            }
        }
        // looks for all equipped buffed and removes those that do not have fields modifiable by this ability, this is mostly useful for manual in engine tweaking

        protected virtual void VerifyEquippedBuffs()
        {
            List<Buff> buffsToRemove = new List<Buff>();

            // since we dont formally remove the buffs when manually inserting buffs, the list needs to be reset every time a new buff is added to avoid stacking
            ClearInternalBuffLists();

            foreach (Buff buff in _buffs)
            {
                // check if the inserted buff is copatible with current ability
                if (CheckFlag(buff.BuffAttribute))
                {
                    //Debug.Log("Buff " + buff.name + " applied correctly to " + this.name);
                    // if it is, apply it to the correct scalable attribute
                    if (!AttachBuffToAttribute(buff))
                    {
                        buffsToRemove.Add(buff);
                    }
                }
                // remove a uncompatible buff if it was plugged in
                else
                {
                    Debug.LogError("Buff: " + buff.Title + " is not compatible with current ability, removing it from equiped buffs");
                    buffsToRemove.Add(buff);
                }
            }

            // remove uncompatible buffs
            foreach (Buff buff in buffsToRemove)
            {
                _buffs.Remove(buff);
            }
        }

        private void InitVariables()
        {
            _activeAbilityProjectiles= 0;
            _hasActiveTarget= false;
            _cooldownTimerStart = 0;
        }

        private void BuildAbilityConstraints()
        {
            InitVariables();

            _isCastAvailableHandler = IsAbilityEnabled;
            
            foreach (var constraint in _abilityDef.ActivationConstraints)
            {
                switch (constraint)
                {
                    case PlayerAbilityActivationEnum.CooldownOnProjectileDestroyed:
                        {
                            // activation: on cooldown
                            _isCastAvailableHandler += IsAbilityOffCooldown;

                            // cooldown begin: projectileDestroyed
                            _projectileDestroyedHandler += BeginCooldownCount;
                            break;
                        }
                    case PlayerAbilityActivationEnum.CooldownOnCastEnd:
                        {
                            // activation: on cooldown
                            _isCastAvailableHandler += IsAbilityOffCooldown;

                            // cooldown begin: after cast
                            _afterCastHandler += BeginCooldownCount;
                            break;
                        }
                    case PlayerAbilityActivationEnum.NumberOfActiveInstances:
                        {
                            // activation: active instances < number
                            _isCastAvailableHandler += AreMaxProjectilesSpawned;
                            break;
                        }
                    case PlayerAbilityActivationEnum.PlayerIsActive:
                        {
                            _requierePlayerAlive = true;
                            break;
                        }
                }
            }
        }
        
        protected void IsAbilityEnabled(ref bool isAvailable)
        {
            isAvailable = _isAbilityEnabled;
        }

        public void TryCastAbility()
        {
            if (IsAvailable())
            {
                if (_abilityDef.AbilityIsShared)
                {
                    CastAbility();
                }
                else
                {
                    foreach (PlayerReference player in _players)
                    {
                        if (!player.Actor.IsDowned)
                        {
                            CastAbilityOnSpecificPlayer(player.Actor);
                        }
                    }
                }
            }
        }

        public virtual void DoAfterCastHandler()
        {
            _afterCastHandler?.Invoke();
        }

        public void TryCastAbilityOnEach()
        {
            if (_abilityDef.AbilityIsShared || _players == null) return;
            foreach(PlayerReference player in _players)
            {
                if (!player.Actor.IsDowned && IsAvailable())
                {
                    CastAbilityOnSpecificPlayer(player.Actor);
                }
            }
        }


        public bool IsAvailable()
        {
            bool isAvailable = true;
            _isCastAvailableHandler.Invoke(ref isAvailable);
            return isAvailable;
        }


        public virtual void OnProjectileDestroyed()
        {
            _activeAbilityProjectiles--;
            _projectileDestroyedHandler?.Invoke();
        }

        public virtual void OnProjectileSpawned()
        {
            _activeAbilityProjectiles++;
        }

        public void BeginCooldownCount()
        {
            _cooldownTimerStart = Time.time;
        }
        public bool IsOnCooldown()
        {
            //if (_cooldownTimerStart == null) return true;

            var elapsedTime = Time.time - _cooldownTimerStart;
            bool isOnCooldown = elapsedTime < _abilityDef.GetStatValue(ScalableAttributesEnum.Cooldown);
            return isOnCooldown;
        }

        public void AreMaxProjectilesSpawned(ref bool isAvailabe)
        {
            isAvailabe = isAvailabe && _activeAbilityProjectiles < Mathf.RoundToInt(_abilityDef.GetStatValue(ScalableAttributesEnum.ProjectileCount));
        }
        private void IsAbilityOffCooldown(ref bool isAvailable)
        {
            isAvailable = isAvailable && !IsOnCooldown();
        }
        public virtual void CastAbility()
        {
            //to be overriden in each ability
        }
        public virtual void CastAbilityOnSpecificPlayer(Actor player)
        {
            //same, but for abilities with both
        }
        // Upgrade system

        private bool CheckFlag(ScalableAttributesEnum flagCheck)
        {
            if (_abilityDef.AbilityScalableAttributes.HasFlag(flagCheck))
            {
                return true;
            }
            return false;
        }

        protected virtual bool AttachBuffToAttribute(Buff buff)
        {
            foreach(ScalableAttribute attribute in _abilityDef.ScalabeAttributes)
            {
                if (attribute.AttributesEnum.HasFlag(buff.BuffAttribute))
                {
                    attribute.AddBuff(buff);
                    return true;
                }
            }
            Debug.LogError(buff.name + " could not be attached to any attribute in " + _abilityDef.AbilityName + " ability. Check scalable attribute list");
            return false;
        }

        protected virtual void ClearInternalBuffLists()
        {
            foreach(ScalableAttribute attribute in _abilityDef.ScalabeAttributes)
            {
                attribute.ClearBuffs();
            }
        }

        public void AddBuff(Buff buff)
        {
            _buffs.Add(buff);
            VerifyEquippedBuffs();
            OnBuffed();
        }

        protected virtual void OnBuffed()
        {
            // method to be overwritten in each ability, called when a new buff is added
        }

        public virtual void ToggleAllProjectiles(bool isEnabled)
        {
            //case the ability have projectile, should destroy or disable abilities
        }


        public void ToggleAbility()
        {
            SetAbilityEnabled(!_isAbilityEnabled);
        }

        public void SetAbilityEnabled(bool isAbilityEnabled)
        {
            _isAbilityEnabled = isAbilityEnabled;
            ToggleAllProjectiles(isAbilityEnabled);
        }

        public bool IsCategory(PlayerAbilityCategoryEnum category)
        {
            return _abilityDef._category.Equals(category);
        }

        public void CalculateAllScalableAttributes()
        {
            foreach(ScalableAttribute scalableAttribute in _abilityDef.ScalabeAttributes)
            {
                scalableAttribute.CalculateValue();
            }
        }

        public void SetAbilityUnlocked(bool state)
        {
            _isAbilityUnlocked = state;
        }
        public PlayerAbilityDefinition GetAbilityDefinition()
        {
            return _abilityDef;
        }
        
    }
}
