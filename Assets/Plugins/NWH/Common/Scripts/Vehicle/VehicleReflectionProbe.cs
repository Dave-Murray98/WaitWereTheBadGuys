// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using NWH.NUI;
#endif

#endregion

namespace NWH.Common.Vehicles
{
    /// <summary>
    /// Manages vehicle reflection probe settings, switching between baked and realtime modes
    /// based on vehicle activity to optimize performance.
    /// </summary>
    /// <remarks>
    /// Realtime reflection probes are expensive. This component switches to cheaper baked probes
    /// when the vehicle is inactive, maintaining visual quality while improving performance.
    /// The probe is automatically re-baked when the vehicle becomes inactive to capture the
    /// current environment state.
    /// </remarks>
    [RequireComponent(typeof(ReflectionProbe))]
    [DefaultExecutionOrder(19)]
    public partial class VehicleReflectionProbe : MonoBehaviour
    {
        /// <summary>
        /// Type of reflection probe to use.
        /// </summary>
        public enum ProbeType
        {
            /// <summary>
            /// Pre-rendered cubemap, low cost but static.
            /// </summary>
            Baked,

            /// <summary>
            /// Updates every frame, high cost but accurate.
            /// </summary>
            Realtime,
        }

        /// <summary>
        /// Probe type to use when vehicle is inactive/sleeping. Default is Baked for performance.
        /// </summary>
        public ProbeType asleepProbeType = ProbeType.Baked;

        /// <summary>
        /// Probe type to use when vehicle is active. Default is Realtime for accurate reflections.
        /// </summary>
        public ProbeType awakeProbeType = ProbeType.Realtime;

        /// <summary>
        /// Automatically bake the probe when vehicle becomes inactive to capture environment state.
        /// </summary>
        public bool      bakeOnSleep    = true;

        /// <summary>
        /// Bake the probe once on start for initial environment capture.
        /// </summary>
        public bool      bakeOnStart    = true;

        private ReflectionProbe _reflectionProbe;
        private Vehicle         _vc;


        private void OnEnable()
        {
            _vc = GetComponentInParent<Vehicle>();
            if (_vc == null)
            {
                Debug.LogError("VehicleController not found.");
            }

            _reflectionProbe = GetComponent<ReflectionProbe>();
            _vc.onEnable.AddListener(OnVehicleEnable);
            _vc.onDisable.AddListener(OnVehicleDisable);

            _reflectionProbe.refreshMode = ReflectionProbeRefreshMode.EveryFrame;

            if (bakeOnStart)
            {
                _reflectionProbe.RenderProbe();
            }
        }


        private void OnVehicleEnable()
        {
            _reflectionProbe.mode = awakeProbeType == ProbeType.Baked
                                        ? _reflectionProbe.mode = ReflectionProbeMode.Baked
                                        : ReflectionProbeMode.Realtime;
        }


        private void OnVehicleDisable()
        {
            _reflectionProbe.mode = asleepProbeType == ProbeType.Baked
                                        ? _reflectionProbe.mode = ReflectionProbeMode.Baked
                                        : ReflectionProbeMode.Realtime;

            if (bakeOnSleep && _reflectionProbe.isActiveAndEnabled)
            {
                _reflectionProbe.RenderProbe();
            }
        }
    }
}

#if UNITY_EDITOR
namespace NWH.Common.Vehicles
{
    [CustomEditor(typeof(VehicleReflectionProbe))]
    [CanEditMultipleObjects]
    public partial class VehicleReflectionProbeEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Field("awakeProbeType");
            drawer.Field("asleepProbeType");
            drawer.Field("bakeOnStart");
            drawer.Field("bakeOnSleep");

            drawer.EndEditor(this);
            return true;
        }


        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif