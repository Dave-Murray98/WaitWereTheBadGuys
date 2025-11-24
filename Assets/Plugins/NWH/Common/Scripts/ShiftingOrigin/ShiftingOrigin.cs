// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using NWH.NUI;
#endif

#endregion

namespace NWH.Common.ShiftingOrigin
{
    /// <summary>
    /// Prevents floating point precision errors by shifting all scene objects back toward world origin
    /// when the main camera exceeds the distance threshold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As objects move far from world origin [0,0,0], floating point precision degrades causing
    /// physics jitter and rendering artifacts. ShiftingOrigin solves this by periodically moving
    /// all scene content back toward origin, keeping the player near [0,0,0] at all times.
    /// </para>
    /// <para>
    /// The shift is transparent to gameplay - relative positions remain identical. Useful for
    /// open world games, flight simulators, or any scenario with large travel distances.
    /// </para>
    /// <para>
    /// Only affects the current scene. For multi-scene setups, ensure one ShiftingOrigin instance
    /// per loaded scene set.
    /// </para>
    /// </remarks>
    public class ShiftingOrigin : MonoBehaviour
    {
        public static ShiftingOrigin Instance;

        /// <summary>
        /// Distance from world origin in meters that triggers an origin shift.
        /// Default 500m works well for most scenarios.
        /// </summary>
        public float      distanceThreshold = 500f;

        /// <summary>
        /// Event invoked after the origin shift completes and physics is re-synced.
        /// </summary>
        public UnityEvent onAfterJump       = new();

        /// <summary>
        /// Event invoked before the origin shift begins. Rigidbody sleep thresholds are temporarily disabled.
        /// </summary>
        public  UnityEvent                onBeforeJump = new();
        private Camera                    _cameraMain;
        private Vector3                   _cameraPosition;
        private Transform                 _cameraTransform;
        private ParticleSystem.Particle[] _particles;

        // Cached to avoid expensive FindObjectsByType calls
        private List<Rigidbody>     _cachedRigidbodies         = new List<Rigidbody>();
        private List<float>         _originalSleepThresholds   = new List<float>();
        private List<ParticleSystem> _cachedParticleSystems    = new List<ParticleSystem>();
        private int                  _cameraCheckFrameCounter  = 0;

        /// <summary>
        /// Cumulative offset applied to all objects since scene start.
        /// Useful for tracking absolute world position despite origin shifts.
        /// </summary>
        public Vector3 TotalOffset { get; private set; }


        private void Awake()
        {
            Debug.Assert(Instance == null, "Only one ShiftingOrigin script can be present in a scene.");
            Instance = this;

            onBeforeJump.AddListener(BeforeJump);
            onAfterJump.AddListener(AfterJump);
        }


        private void Start()
        {
            _cameraMain = Camera.main;
            if (_cameraMain != null)
            {
                _cameraTransform = _cameraMain.transform;
            }

            RefreshCaches();
        }


        /// <summary>
        /// Refreshes cached references to Rigidbodies and ParticleSystems.
        /// Call this if new physics objects or particle systems are added to the scene at runtime.
        /// </summary>
        public void RefreshCaches()
        {
            _cachedRigidbodies.Clear();
            _originalSleepThresholds.Clear();
            _cachedParticleSystems.Clear();

            // Cache Rigidbodies and preserve their original sleepThreshold values
            var rbs = FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var rb in rbs)
            {
                if (rb != null)
                {
                    _cachedRigidbodies.Add(rb);
                    _originalSleepThresholds.Add(rb.sleepThreshold);
                }
            }

            // Cache ParticleSystems
            var pss = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _cachedParticleSystems.AddRange(pss);
        }


        private void LateUpdate()
        {
            // Check Camera.main every 60 frames to support runtime camera switching
            _cameraCheckFrameCounter++;
            if (_cameraCheckFrameCounter >= 60)
            {
                Camera newCamera = Camera.main;
                if (newCamera != _cameraMain)
                {
                    _cameraMain = newCamera;
                    _cameraTransform = _cameraMain != null ? _cameraMain.transform : null;
                }
                _cameraCheckFrameCounter = 0;
            }

            if (_cameraMain == null)
            {
                return;
            }

            if (_cameraTransform == null)
            {
                return;
            }

            _cameraPosition  = _cameraTransform.position;

            if (_cameraPosition.magnitude > distanceThreshold)
            {
                Jump();
            }
        }


        private static List<T> FindObjects<T>() where T : Object
        {
            return FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList(); // Not the fastest solution
        }


        private void BeforeJump()
        {
            for (int i = 0; i < _cachedRigidbodies.Count; i++)
            {
                if (_cachedRigidbodies[i] != null)
                {
                    _cachedRigidbodies[i].sleepThreshold = float.MaxValue;
                }
            }
        }


        private void AfterJump()
        {
            // Restore original values to preserve custom physics settings
            for (int i = 0; i < _cachedRigidbodies.Count; i++)
            {
                if (_cachedRigidbodies[i] != null && i < _originalSleepThresholds.Count)
                {
                    _cachedRigidbodies[i].sleepThreshold = _originalSleepThresholds[i];
                }
            }

            Physics.SyncTransforms();
        }


        private void Jump()
        {
            onBeforeJump.Invoke();

            TotalOffset += _cameraPosition;

            // Move root transforms
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                foreach (GameObject g in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (g != null && g.transform != null)
                    {
                        g.transform.position -= _cameraPosition;
                    }
                }
            }

            // Move particles
            for (int pi = 0; pi < _cachedParticleSystems.Count; pi++)
            {
                ParticleSystem ps = _cachedParticleSystems[pi];
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;

                if (main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    continue;
                }

                int maxParticles = main.maxParticles;

                if (maxParticles == 0)
                {
                    continue;
                }

                bool wasPaused  = ps.isPaused;
                bool wasPlaying = ps.isPlaying;

                if (!wasPaused)
                {
                    ps.Pause();
                }

                if (_particles == null || _particles.Length < maxParticles)
                {
                    _particles = new ParticleSystem.Particle[maxParticles];
                }

                int num = ps.GetParticles(_particles);

                for (int i = 0; i < num; i++)
                {
                    _particles[i].position -= _cameraPosition;
                }

                ps.SetParticles(_particles, num);

                if (wasPlaying)
                {
                    ps.Play();
                }
            }

            onAfterJump.Invoke();
        }
    }
}

#if UNITY_EDITOR
namespace NWH.Common.ShiftingOrigin
{
    [CustomEditor(typeof(ShiftingOrigin))]
    public class ShiftingOriginEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("distanceThreshold");
            drawer.Field("onBeforeJump");
            drawer.Field("onAfterJump");

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif