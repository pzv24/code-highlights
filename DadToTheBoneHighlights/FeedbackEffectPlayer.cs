using Dads.Levels;
using UnityEngine;
using UnityEngine.VFX;
using Sirenix.OdinInspector;
using GameEvents;

namespace Dads.Effects
{
    public class FeedbackEffectPlayer : LevelDependentSpawner
    {
        private const string EffectDurationParam = "EffectDuration";
        
        [Title("Event Name")]
        [SerializeField, ReadOnly] protected string _eventName;
        [SerializeField] protected bool _debugEnabled;
        [SerializeField] private float _cooldown;
        [SerializeField] protected bool _restrictByCooldown;
        
        [Title("VFX")]
        [Tooltip("If using VXGraph System")]
        [SerializeField] protected VisualEffect _visualEffect;
        [Tooltip("If using Shuriken System")]
        [SerializeField] protected ParticleSystem _particleSystem;

        [Header("Transform settings")]
        [SerializeField] protected Vector3 _systemSpawnOffset = Vector3.zero;
        [SerializeField] protected Vector3 _systemRotation = Vector3.zero;
        [SerializeField] protected bool _spawnSystemParented;
        [SerializeField] protected bool _offsetEffectFromCameraPosition;
        [SerializeField] private float _cameraOffset = 0;

        [SerializeField] protected bool _offsetEffectFromObjectsForwardDirection;
        [SerializeField] private float _forwardOffset = 0;

        [Title("SFX")]
        [SerializeField] protected FMODUnity.EventReference _soundEffect;
        [SerializeField] protected bool _playAttached;
        [ShowIf("_playAttached"),InfoBox("if the option play attached is enabled, the sfx needs to be destroyed to finish the sfx execution ")]
        [SerializeField] protected bool _instantiateEmitter;
        [ShowIf("_playAttached")]
        [SerializeField] private GameObject _sfxEmitterPrefab;

        [Title("Event Listener")]
        [SerializeField] protected GameObjectEventAsset _onEventTrigger;

        private VisualEffect _instantiatedEffect;
        private ParticleSystem _instantiatedParticleSystem;
        private GameObject _gameObject;
        private float _lastTimePlayed;
        
        protected override void OnValidate()
        {
            base.OnValidate();
            _eventName = _onEventTrigger.name;
        }
        
        protected virtual void OnEnable()
        {
            _onEventTrigger.OnInvoked.AddListener(PlayEffects);
        }
        protected virtual void OnDisable()
        {
            _onEventTrigger.OnInvoked.RemoveListener(PlayEffects);
        }

        protected virtual void PlayEffects(GameObject targetObject)
        {
            if (_restrictByCooldown)
            {
                if (_lastTimePlayed > float.Epsilon && Time.time - _lastTimePlayed < _cooldown) return;
                _lastTimePlayed = Time.time;
            }
            
            _gameObject = targetObject;
            // instantiate the visual effect
            // for vfx graph effects
            if(_visualEffect != null)
            {
                if (_debugEnabled) Debug.Log($"{_eventName} played a visual effect on {_gameObject.name}");
                if (_spawnSystemParented)
                {
                    _instantiatedEffect = Instantiate(_visualEffect, targetObject.transform.position, Quaternion.Euler(_systemRotation), targetObject.transform);
                    _instantiatedEffect.transform.localPosition = _systemSpawnOffset;
                    if (_offsetEffectFromCameraPosition)
                    {
                        if (Camera.main != null) _instantiatedEffect.transform.position = Vector3.MoveTowards(_instantiatedEffect.transform.position, Camera.main.transform.position, _cameraOffset);
                    }
                    if (_offsetEffectFromObjectsForwardDirection)
                    {
                        Vector3 direction = targetObject.transform.localRotation * Vector3.forward;
                        Debug.Log(direction);
                        _instantiatedEffect.transform.localPosition = _instantiatedEffect.transform.localPosition + direction * _forwardOffset;
                    }
                }
                else
                {
                    _instantiatedEffect = Instantiate(_visualEffect);
                    MoveToPivotScene(_instantiatedEffect.gameObject);

                    var position = _gameObject.transform.position;
                    _instantiatedEffect.transform.position = new Vector3(position.x + _systemSpawnOffset.x, position.y + _systemSpawnOffset.y, position.z + _systemSpawnOffset.z);
                    if (_offsetEffectFromCameraPosition)
                    {
                        if (Camera.main != null) _instantiatedEffect.transform.position = Vector3.MoveTowards(_instantiatedEffect.transform.position, Camera.main.transform.position, _cameraOffset);
                    }
                    if (_offsetEffectFromObjectsForwardDirection)
                    {
                        Vector3 direction = targetObject.transform.localRotation * Vector3.forward;
                        _instantiatedEffect.transform.localPosition = _instantiatedEffect.transform.localPosition + direction * _forwardOffset;
                    }
                }
                _instantiatedEffect.Play();
                if (_instantiatedEffect.HasFloat(EffectDurationParam))
                {
                    var duration = _visualEffect.GetFloat(EffectDurationParam);
                    if(duration > 0)
                    {
                        ParticleDestroyer destroyer = _instantiatedEffect.gameObject.AddComponent<ParticleDestroyer>();
                        destroyer.QueueDestroy(duration);
                    }    
                }
            }

            // for shuriken particle systems
            if(_particleSystem != null)
            {
                if (_debugEnabled) Debug.Log($"{_eventName} played a visual effect (shuriken) on {_gameObject.name}");
                if (_spawnSystemParented)
                {
                    _instantiatedParticleSystem = Instantiate(_particleSystem, targetObject.transform.position, Quaternion.Euler(_systemRotation), targetObject.transform);
                    MoveToPivotScene(_instantiatedParticleSystem.gameObject);
                    _instantiatedParticleSystem.transform.localPosition = _systemSpawnOffset;
                    _instantiatedParticleSystem.Play();
                }
                else
                {
                    _instantiatedParticleSystem = Instantiate(_particleSystem, targetObject.transform.position, Quaternion.Euler(_systemRotation), targetObject.transform);
                    MoveToPivotScene(_instantiatedParticleSystem.gameObject);
                    _instantiatedParticleSystem.transform.localPosition = _systemSpawnOffset;
                    _instantiatedParticleSystem.Play();
                    _instantiatedParticleSystem.transform.parent = null;
                }

                float duration = _particleSystem.main.duration;
                if (duration > 0)
                {
                    ParticleDestroyer destroyer = _instantiatedParticleSystem.gameObject.AddComponent<ParticleDestroyer>();
                    destroyer.QueueDestroy(duration);
                }
            }
            if (_soundEffect.IsNull) return;
            // play the sound effect, if it has one
            if (_playAttached)
            {
                // play the fmod attached
                if (_debugEnabled) Debug.Log($"{_eventName} played a SFX attached on {_gameObject.name}");
                FMODUnity.StudioEventEmitter fmodEmitter = null;
                if (_instantiateEmitter)
                {
                    Transform targetPositionTransform = targetObject.transform;
                    var emitter = Instantiate(_sfxEmitterPrefab, targetPositionTransform.position, Quaternion.identity, targetPositionTransform);
                    fmodEmitter = emitter.AddComponent<FMODUnity.StudioEventEmitter>();
                }
                else
                {
                    fmodEmitter = targetObject.AddComponent<FMODUnity.StudioEventEmitter>();
                }
                fmodEmitter.EventReference = _soundEffect;
                fmodEmitter.StopEvent = FMODUnity.EmitterGameEvent.ObjectDestroy;
                fmodEmitter.Play();
            }
            else
            {
                // play the fmod sound once
                if (_debugEnabled) Debug.Log($"{_eventName} played a SFX on location on {_gameObject.name}");
                FMODUnity.RuntimeManager.PlayOneShot(_soundEffect, targetObject.transform.position);
            }
        }

    }

}
