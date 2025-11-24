// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.DWP2.SailController
{
    /// <summary>
    /// Visual indicator that rotates to show wind direction.
    /// Displays apparent wind when attached as a child of a SailController.
    /// Displays true wind from the WindGenerator when not under a SailController.
    /// Useful for debugging sail setup and helping players understand wind conditions.
    /// The transform's forward direction will point into the wind.
    /// </summary>
    public class WindIndicator : MonoBehaviour
    {
        private SailController _sailController;


        private void Awake()
        {
            _sailController = GetComponentInParent<SailController>();
        }


        private void Update()
        {
            if (_sailController != null)
            {
                // Show apparent wind.
                transform.rotation = Quaternion.LookRotation(_sailController.ApparentWind.normalized,
                                                             transform.parent.up);
            }
            else if (WindGenerator.Instance != null)
            {
                // Show true wind.
                transform.rotation = Quaternion.LookRotation(WindGenerator.Instance.CurrentWind.normalized,
                                                             transform.parent.up);
            }
        }
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.SailController
{
    [CustomEditor(typeof(WindIndicator))]
    [CanEditMultipleObjects]
    public class WindIndicatorEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.Info("Shows apparent wind if placed as a child of the SailController, " +
                        "or true wind otherwise.");

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif