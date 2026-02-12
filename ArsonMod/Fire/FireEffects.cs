using UnityEngine;

namespace ArsonMod.Fire
{
    /// <summary>
    /// Manages fire visual and audio effects for a burning room.
    /// Spawns particle systems, adjusts lighting, and plays audio.
    /// </summary>
    public class FireEffects
    {
        private GameObject _fireRoot;
        private ParticleSystem _flameParticles;
        private ParticleSystem _smokeParticles;
        private Light _fireLight;
        private AudioSource _audioSource;
        private string _roomId;

        // Cached references to the game's existing fire particle systems
        // Cloned from TrashBin objects which already have fire/smoke particles
        private static ParticleSystem _templateFlames;
        private static ParticleSystem _templateSmoke;

        /// <summary>
        /// Caches references to the game's existing fire particle systems from TrashBin objects.
        /// Call once at game start. No custom assets needed.
        /// </summary>
        public static void CacheGameParticles()
        {
            var trashBins = Object.FindObjectsOfType<Il2CppProps.TrashBin.TrashBin>();
            if (trashBins != null && trashBins.Length > 0)
            {
                var bin = trashBins[0];
                _templateFlames = bin.firePs;
                _templateSmoke = bin.smokePs;
            }
        }

        /// <summary>Spawns fire effects in a room and returns the controller.</summary>
        public static FireEffects SpawnInRoom(string roomId)
        {
            var effects = new FireEffects();
            effects._roomId = roomId;
            effects.Spawn();
            return effects;
        }

        private void Spawn()
        {
            // Find the room's transform in the scene
            var roomObj = GameObject.Find(_roomId);
            if (roomObj == null) return;

            _fireRoot = new GameObject($"FireEffects_{_roomId}");
            _fireRoot.transform.SetParent(roomObj.transform);
            _fireRoot.transform.localPosition = Vector3.zero;

            SpawnFlames();
            SpawnSmoke();
            SpawnLight();
            SpawnAudio();
        }

        private void SpawnFlames()
        {
            // Try to clone the game's existing fire particles from TrashBin
            if (_templateFlames != null)
            {
                var flameObj = Object.Instantiate(_templateFlames.gameObject, _fireRoot.transform);
                _flameParticles = flameObj.GetComponent<ParticleSystem>();
                _flameParticles.Play();
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[ArsonMod] No cached flame particles from TrashBin. Fire visuals unavailable.");
            }
        }

        private void SpawnSmoke()
        {
            // Try to clone the game's existing smoke particles from TrashBin
            if (_templateSmoke != null)
            {
                var smokeObj = Object.Instantiate(_templateSmoke.gameObject, _fireRoot.transform);
                _smokeParticles = smokeObj.GetComponent<ParticleSystem>();
                _smokeParticles.Play();
            }
            else
            {
                MelonLoader.MelonLogger.Warning("[ArsonMod] No cached smoke particles from TrashBin. Smoke visuals unavailable.");
            }
        }

        private void SpawnLight()
        {
            var lightObj = new GameObject("FireLight");
            lightObj.transform.SetParent(_fireRoot.transform);
            lightObj.transform.localPosition = Vector3.up * 1f;

            _fireLight = lightObj.AddComponent<Light>();
            _fireLight.type = LightType.Point;
            _fireLight.color = new Color(1f, 0.6f, 0.1f);
            _fireLight.intensity = 2f;
            _fireLight.range = 8f;

            // Flickering is handled by a simple script
            lightObj.AddComponent<FireLightFlicker>();
        }

        private void SpawnAudio()
        {
            _audioSource = _fireRoot.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f; // 3D audio
            _audioSource.loop = true;
            _audioSource.volume = 0.6f;
            _audioSource.maxDistance = 15f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;

            // Try to borrow audio from a game TrashBin's fire audio
            var trashBins = Object.FindObjectsOfType<Il2CppProps.TrashBin.TrashBin>();
            if (trashBins != null && trashBins.Length > 0)
            {
                var binAudio = trashBins[0].audioSource;
                if (binAudio != null && binAudio.clip != null)
                {
                    _audioSource.clip = binAudio.clip;
                    _audioSource.Play();
                }
            }
        }

        public void Destroy()
        {
            if (_fireRoot != null)
            {
                Object.Destroy(_fireRoot);
                _fireRoot = null;
            }
        }
    }

    /// <summary>Simple light flickering component for fire ambiance.</summary>
    public class FireLightFlicker : MonoBehaviour
    {
        private Light _light;
        private float _baseIntensity;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
        }

        private void Update()
        {
            // Perlin noise-based flicker for natural-looking fire light
            float noise = Mathf.PerlinNoise(Time.time * 8f, 0f);
            _light.intensity = _baseIntensity * (0.7f + noise * 0.6f);
        }
    }
}
