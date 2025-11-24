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
    /// Manages sail physics by calculating and applying lift and drag forces based on sail geometry, orientation, and wind conditions.
    /// Uses four corner transforms (a, b, c, d) to define sail shape, enabling dynamic sail configurations including furling and multi-part sails.
    /// Calculates apparent wind from true wind and vessel velocity, then applies aerodynamic forces at the sail's surface-weighted center.
    /// The corner transforms follow a clockwise pattern: a (bottom left/front), b (top left/front), c (top right/rear), d (bottom right/rear).
    /// Corner transforms can be attached to different parents to enable sail furling or complex rigging systems.
    /// Use SailRotator for user-controlled sail rotation.
    /// Requires a WindGenerator in the scene and a SailPreset for aerodynamic coefficients.
    /// </summary>
    public class SailController : MonoBehaviour
    {
        private void Awake()
        {
            _rb = GetComponentInParent<Rigidbody>();
            Debug.Assert(_rb != null, "Rigidbody not found on the sail or any of the parent GameObjects.");
        }


        private void Start()
        {
            if (WindGenerator.Instance == null)
            {
                Debug.LogError("WindGenerator not found in the scene. SailController will not work.");
            }

            if (sailPreset == null)
            {
                Debug.LogError($"SailPreset not assigned to SailController {name}");
            }
        }


        private void FixedUpdate()
        {
            Debug.Assert(a != null, $"{name}: Transform 'a' is not assigned.");
            Debug.Assert(b != null, $"{name}: Transform 'b' is not assigned.");
            Debug.Assert(c != null, $"{name}: Transform 'c' is not assigned.");
            Debug.Assert(d != null, $"{name}: Transform 'd' is not assigned.");

            // Update the sail positions, directions and geometry
            UpdateCachedPositions();
            SailCenter  = CalculateSurfaceWeightedCenter();
            SailArea    = CalculateSailArea();
            SailForward = CalculateSailForward();
            SailUp      = CalculateSailUp();
            SailRight   = CalculateSailRight(SailUp, SailForward);

            // Calculate velocities
            ShipVelocity  = _rb.linearVelocity;
            TrueWind      = WindGenerator.Instance.CurrentWind;
            ApparentWind  = CalculateApparentWind(ShipVelocity, TrueWind);
            AngleOfAttack = CalculateAngleOfAttack(ApparentWind, SailForward);

            // Calculate and apply force
            SailForce = CalculateSailForce();
            _rb.AddForceAtPosition(SailForce, SailCenter);
        }


        private void OnDrawGizmos()
        {
            #if UNITY_EDITOR
            if (a == null || b == null || c == null || d == null)
            {
                return;
            }

            UpdateCachedPositions();

            Vector3 center = CalculateSurfaceWeightedCenter();

            // Draw sail directions
            if (!Application.isPlaying) // Calculate directions if out of play mode
            {
                SailForward = CalculateSailForward();
                SailUp      = CalculateSailUp();
                SailRight   = CalculateSailRight(SailUp, SailForward);
            }

            Gizmos.color = Color.blue;
            Gizmos.DrawRay(center, SailForward);

            Gizmos.color = Color.green;
            Gizmos.DrawRay(center, SailUp);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(center, SailRight);

            // Draw sail shape
            if (a != null && b != null && c != null && d != null)
            {
                Gizmos.color = Color.white;

                Gizmos.DrawLine(_pA, _pB);
                Gizmos.DrawLine(_pB, _pC);
                Gizmos.DrawLine(_pC, _pD);
                Gizmos.DrawLine(_pD, _pA);
                Gizmos.DrawLine(_pA, _pC);
                Gizmos.DrawLine(_pB, _pD);

                Gizmos.DrawSphere(_pA, 0.1f);
                Gizmos.DrawSphere(_pB, 0.1f);
                Gizmos.DrawSphere(_pC, 0.1f);
                Gizmos.DrawSphere(_pD, 0.1f);

                Handles.Label(_pA, "A (bottom left)");
                Handles.Label(_pB, "B (top left)");
                Handles.Label(_pC, "C (top right)");
                Handles.Label(_pD, "D (bottom right");
            }

            // RUNTIME ONLY FROM THIS POINT FORWARD
            if (!Application.isPlaying)
            {
                return;
            }

            // Draw force point (center)
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(center, 0.05f);

            // Draw wind
            Gizmos.color = Color.green;
            Gizmos.DrawRay(center, TrueWind * 0.2f);
            Handles.Label(center + TrueWind * 0.2f, $"True Wind ({TrueWind.magnitude} m/s)");

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(center, ApparentWind * 0.2f);
            Handles.Label(center + ApparentWind * 0.2f, $"Apparent Wind ({ApparentWind.magnitude} m/s)");

            Gizmos.color = Color.red;
            Gizmos.DrawRay(center, SailForce * 0.001f);
            Handles.Label(center + SailForce * 0.001f, $"Force ({SailForce.magnitude} N)");

            Gizmos.color = Color.white;
            Gizmos.DrawRay(center, _liftForceDirection);
            Handles.Label(center + _liftForceDirection, "Lift Force Dir.");

            Gizmos.color = Color.gray;
            Gizmos.DrawRay(center, _dragForceDirection);
            Handles.Label(center + _dragForceDirection, "Drag Force Dir.");

            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(center - Vector3.up * 0.1f, ShipVelocity * 0.1f);
            Handles.Label(center - Vector3.up * 0.1f + ShipVelocity * 0.1f,
                          $"Ship Velocity ({ShipVelocity.magnitude} m/s)");
            #endif
        }


        private void UpdateCachedPositions()
        {
            _pA = a.position;
            _pB = b.position;
            _pC = c.position;
            _pD = d.position;
        }


        private Vector3 CalculateSurfaceWeightedCenter()
        {
            return GeometryUtils.CalculateSurfaceWeightedCenter(_pA, _pB, _pC, _pD);
        }


        private float CalculateSailArea()
        {
            return GeometryUtils.CalculateQuadrilateralArea(_pA, _pB, _pC, _pD);
        }


        private Vector3 CalculateSailForward()
        {
            return (_pA - _pD).normalized;
        }


        private Vector3 CalculateSailUp()
        {
            return ((_pB + _pC) * 0.5f - (_pA + _pD) * 0.5f).normalized;
        }


        private Vector3 CalculateSailRight(Vector3 sailUp, Vector3 sailForward)
        {
            return Vector3.Cross(sailUp, sailForward).normalized;
        }


        private Vector3 CalculateApparentWind(Vector3 boatVelocity, Vector3 trueWind)
        {
            return trueWind - boatVelocity;
        }


        private float CalculateAngleOfAttack(Vector3 apparentWind, Vector3 sailForward)
        {
            return Vector3.SignedAngle(sailForward, apparentWind, Vector3.up);
        }


        private Vector3 CalculateSailForce()
        {
            float apparentWindSpeed = ApparentWind.magnitude;
            float dynamicPressure   = 0.5f * airDensity * apparentWindSpeed * apparentWindSpeed;

            float liftCoefficient = sailPreset.liftCoefficientVsAoACurve.Evaluate(AngleOfAttack) * sailPreset.liftScale;
            _liftForce = liftCoefficient * dynamicPressure * SailArea;

            float dragCoefficient = sailPreset.dragCoefficientVsAoACurve.Evaluate(AngleOfAttack) * sailPreset.dragScale;
            _dragForce = dragCoefficient * dynamicPressure * SailArea;

            _liftForceDirection = SailRight * Mathf.Sign(Vector3.Dot(ApparentWind, SailRight));
            _dragForceDirection = ApparentWind.normalized;

            Vector3 totalForce = _liftForceDirection * _liftForce + _dragForceDirection * _dragForce;

            // Compensate for the lean
            totalForce *= Vector3.Dot(SailUp, Vector3.up);

            return totalForce;
        }


        #region UserSettings

        /// <summary>
        /// Bottom left corner of the sail for square sails, or bottom front corner for triangular sails.
        /// First point in the clockwise corner definition pattern.
        /// Can be attached to any parent transform to enable dynamic sail configurations.
        /// </summary>
        [Tooltip("Bottom left sail corner if square sail.\r\nOtherwise bottom front.")]
        public Transform a;

        /// <summary>
        /// Top left corner of the sail for square sails, or top front corner for triangular sails.
        /// Second point in the clockwise corner definition pattern.
        /// Can be attached to any parent transform to enable dynamic sail configurations.
        /// </summary>
        [Tooltip("Top left sail corner if square sail.\r\nOtherwise top front.")]
        public Transform b;

        /// <summary>
        /// Top right corner of the sail for square sails, or top rear corner for triangular sails.
        /// Third point in the clockwise corner definition pattern.
        /// Can be attached to any parent transform to enable dynamic sail configurations.
        /// </summary>
        [Tooltip("Top right sail corner if square sail.\r\nOtherwise top rear.")]
        public Transform c;

        /// <summary>
        /// Bottom right corner of the sail for square sails, or bottom rear corner for triangular sails.
        /// Fourth point in the clockwise corner definition pattern.
        /// Can be attached to any parent transform to enable dynamic sail configurations.
        /// </summary>
        [Tooltip("Bottom right sail corner if square sail.\r\nOtherwise bottom rear.")]
        public Transform d;

        /// <summary>
        /// Defines the aerodynamic characteristics of the sail through lift and drag coefficient curves.
        /// Contains the relationship between angle of attack and force coefficients.
        /// </summary>
        public SailPreset sailPreset;

        /// <summary>
        /// Air density in kg/m³ used in aerodynamic force calculations.
        /// Standard sea level value is 1.225 kg/m³.
        /// Can be adjusted as a multiplier to scale all sail forces uniformly without modifying the preset.
        /// </summary>
        [Tooltip(
            "The air density. Can also be used\r\nas a force coefficient as this affects both lift and drag forces equally.")]
        public float airDensity = 1.225f;

        #endregion

        #region SailCalculated

        /// <summary>
        /// Surface-weighted center point of the sail in world space.
        /// All aerodynamic forces are applied at this position.
        /// Calculated from the quadrilateral formed by corner points a, b, c, and d.
        /// </summary>
        public Vector3 SailCenter { get; private set; }

        /// <summary>
        /// Total surface area of the sail in square meters.
        /// Calculated from the quadrilateral formed by corner points a, b, c, and d.
        /// Used in aerodynamic force calculations along with dynamic pressure.
        /// </summary>
        public float SailArea { get; private set; }

        /// <summary>
        /// Forward direction vector of the sail in world space.
        /// Determined by the vector from corner point d to corner point a.
        /// Used to calculate the angle of attack relative to the apparent wind.
        /// </summary>
        public Vector3 SailForward { get; private set; }

        /// <summary>
        /// Up direction vector of the sail in world space.
        /// Calculated from the average of the top edge to the average of the bottom edge.
        /// Used to determine sail orientation and compensate for heel angle.
        /// </summary>
        public Vector3 SailUp { get; private set; }

        /// <summary>
        /// Right direction vector of the sail in world space.
        /// Derived from the cross product of SailUp and SailForward.
        /// Defines the direction of lift force generation perpendicular to the sail plane.
        /// </summary>
        public Vector3 SailRight { get; private set; }

        private Vector3 _liftForceDirection;

        private Vector3 _dragForceDirection;

        private float _liftForce;

        private float _dragForce;

        #endregion

        #region WindCalculated

        /// <summary>
        /// True wind vector from the WindGenerator, representing the actual environmental wind.
        /// Does not account for vessel motion.
        /// Measured in meters per second.
        /// </summary>
        public Vector3 TrueWind { get; private set; }

        /// <summary>
        /// Current velocity of the vessel's Rigidbody in world space.
        /// Used to calculate apparent wind by combining with true wind.
        /// Measured in meters per second.
        /// </summary>
        public Vector3 ShipVelocity { get; private set; }

        /// <summary>
        /// Total aerodynamic force vector applied to the vessel at the sail center point.
        /// Combines lift and drag forces based on sail geometry, apparent wind, and aerodynamic coefficients.
        /// Measured in Newtons.
        /// </summary>
        public Vector3 SailForce { get; private set; }

        /// <summary>
        /// Wind experienced by the sail relative to the moving vessel.
        /// Calculated as the vector difference between true wind and vessel velocity.
        /// This is the effective wind that generates aerodynamic forces on the sail.
        /// Measured in meters per second.
        /// </summary>
        public Vector3 ApparentWind { get; private set; }

        /// <summary>
        /// Angle in degrees between the sail's forward direction and the apparent wind direction.
        /// Measured on the horizontal plane using Vector3.up as the reference axis.
        /// Used to determine lift and drag coefficients from the SailPreset curves.
        /// Positive values indicate wind from the starboard side, negative values from port side.
        /// </summary>
        public float AngleOfAttack { get; private set; }

        #endregion

        #region Cached

        private Vector3   _pA;
        private Vector3   _pB;
        private Vector3   _pC;
        private Vector3   _pD;
        private Rigidbody _rb;

        #endregion
    }
}

#if UNITY_EDITOR
namespace NWH.DWP2.SailController
{
    [CustomEditor(typeof(SailController))]
    [CanEditMultipleObjects]
    public class SailControllerEditor : DWP2NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            SailController sailController = (SailController)target;

            if (Application.isPlaying)
            {
                drawer.BeginSubsection("Debug Info");
                drawer.Label($"AoA: {sailController.AngleOfAttack}");
                drawer.Label($"Force Mag.: {sailController.SailForce.magnitude}");
                drawer.Label($"Force: {sailController.SailForce}");
                drawer.EndSubsection();
            }

            drawer.BeginSubsection("Geometry");
            drawer.Field("a");
            drawer.Field("b");
            drawer.Field("c");
            drawer.Field("d");
            drawer.EndSubsection();

            drawer.BeginSubsection("Physics");
            drawer.Field("sailPreset");
            drawer.Field("airDensity");
            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }
    }
}
#endif