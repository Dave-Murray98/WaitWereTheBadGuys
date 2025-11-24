// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.SailController
{
    /// <summary>
    /// Generates procedural wind with randomized gusts for sail simulation.
    /// Creates realistic wind conditions by varying speed and direction over time.
    /// Provides a singleton instance accessible to all sail and wind-related components.
    /// Wind direction uses world coordinates where 0 degrees points along the positive Z-axis.
    /// Only one WindGenerator should exist per scene.
    /// </summary>
    public class WindGenerator : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance accessible globally.
        /// Used by SailController and HullWindApplicator to get current wind conditions.
        /// </summary>
        public static WindGenerator Instance;

        /// <summary>
        /// Primary wind direction in degrees on the horizontal plane.
        /// 0 degrees corresponds to positive Z-axis (north in Unity coordinates).
        /// Rotation follows the right-hand rule around the Y-axis.
        /// Wind will vary around this base direction within the limits of maxDirectionVariation.
        /// </summary>
        [Tooltip("Base wind direction in degrees with 0 degrees indicating Z-forward ('north').")]
        public float baseDirection;

        /// <summary>
        /// Average wind speed in meters per second.
        /// Wind will vary around this base speed within the limits of maxSpeedVariation.
        /// Typical sailing winds range from 5 m/s (light breeze) to 20 m/s (strong wind).
        /// </summary>
        [Tooltip("Base wind speed in m/s.")]
        public float baseSpeed = 10.0f;

        /// <summary>
        /// Maximum angular deviation from baseDirection during wind gusts.
        /// Measured in degrees.
        /// Higher values create more unpredictable wind shifts.
        /// Realistic values range from 15 to 45 degrees.
        /// </summary>
        [Tooltip("Maximum possible variation of the direction in degrees from the\r\nbaseDirection.")]
        public float maxDirectionVariation = 30f;

        /// <summary>
        /// Maximum speed deviation from baseSpeed during wind gusts.
        /// Measured in meters per second.
        /// Higher values create stronger variations between lulls and gusts.
        /// Can be positive or negative relative to base speed.
        /// </summary>
        [Tooltip("Maximum possible variation/deviation of the wind from the baseSpeed.")]
        public float maxSpeedVariation = 5f;

        /// <summary>
        /// Longest possible duration between wind condition changes.
        /// Measured in seconds.
        /// New wind conditions are selected randomly between minVariationInterval and this value.
        /// </summary>
        [Tooltip("Maximum interval between the wind variations / changes.")]
        public float maxVariationInterval = 6.0f;

        /// <summary>
        /// Shortest possible duration between wind condition changes.
        /// Measured in seconds.
        /// New wind conditions are selected randomly between this value and maxVariationInterval.
        /// </summary>
        [Tooltip("Minimum interval between the wind variations / changes.")]
        public float minVariationInterval = 2.0f;

        private float _currentInterval = 1f;
        private float _smoothingDirectionVelocity;
        private float _smoothingSpeedVelocity;
        private float _targetDirection;
        private float _targetSpeed;

        /// <summary>
        /// Current wind vector in world space.
        /// Combines CurrentDirection and CurrentSpeed into a directional velocity vector.
        /// Updated every FixedUpdate with smooth damping between gust transitions.
        /// Measured in meters per second.
        /// </summary>
        public Vector3 CurrentWind { get; private set; }

        /// <summary>
        /// Current wind direction in degrees on the horizontal plane.
        /// Smoothly interpolates between randomized target directions.
        /// 0 degrees corresponds to positive Z-axis.
        /// </summary>
        public float CurrentDirection { get; private set; }

        /// <summary>
        /// Current wind speed magnitude in meters per second.
        /// Smoothly interpolates between randomized target speeds.
        /// </summary>
        public float CurrentSpeed { get; private set; }


        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("The scene has more than one WindGenerator. The previous one(s) will be ignored.");
            }

            Instance = this;
        }


        private void Start()
        {
            StartCoroutine(GustCoroutine());
        }


        private void FixedUpdate()
        {
            CurrentDirection = Mathf.SmoothDamp(CurrentDirection, _targetDirection,
                                                ref _smoothingDirectionVelocity, _currentInterval);
            CurrentSpeed = Mathf.SmoothDamp(CurrentSpeed, _targetSpeed,
                                            ref _smoothingSpeedVelocity, _currentInterval);

            CurrentWind = Quaternion.AngleAxis(CurrentDirection, Vector3.up) * Vector3.forward * CurrentSpeed;
        }


        private IEnumerator GustCoroutine()
        {
            while (true)
            {
                _targetSpeed     = baseSpeed + Random.Range(-maxSpeedVariation,         maxSpeedVariation);
                _targetDirection = baseDirection + Random.Range(-maxDirectionVariation, maxDirectionVariation);
                _currentInterval = Random.Range(minVariationInterval, maxVariationInterval);
                yield return new WaitForSeconds(_currentInterval);
            }
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.SailController
{
    [CustomEditor(typeof(WindGenerator))]
    [CanEditMultipleObjects]
    public class WindGeneratorEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            WindGenerator windGenerator = (WindGenerator)target;

            if (Application.isPlaying)
            {
                drawer.Label($"Current Wind: {windGenerator.CurrentSpeed:0.0} from {windGenerator.CurrentDirection:0}");
            }

            drawer.BeginSubsection("Base");
            drawer.Field("baseSpeed");
            drawer.Field("baseDirection");
            drawer.EndSubsection();

            drawer.BeginSubsection("Variation");
            drawer.Field("maxSpeedVariation");
            drawer.Field("maxDirectionVariation");
            drawer.Field("minVariationInterval");
            drawer.Field("maxVariationInterval");
            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif