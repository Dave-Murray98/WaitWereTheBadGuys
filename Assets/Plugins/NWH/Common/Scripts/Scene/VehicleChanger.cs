// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System.Collections.Generic;
using NWH.Common.Input;
using NWH.Common.Vehicles;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

#endregion

namespace NWH.Common.SceneManagement
{
    /// <summary>
    /// Manages switching between multiple vehicles in a scene with support for both instant
    /// and character-based (enter/exit) modes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// VehicleChanger supports two modes:
    /// - Instant switching: Press a button to cycle through vehicles immediately
    /// - Character-based: Player must walk to a vehicle and enter/exit at designated points
    /// </para>
    /// <para>
    /// In character-based mode, the player can only enter vehicles when near an EnterExitPoint
    /// and the vehicle is moving slowly enough. This creates more realistic vehicle switching
    /// similar to GTA-style games.
    /// </para>
    /// <para>
    /// Inactive vehicles can optionally be put to sleep to improve performance when managing
    /// many vehicles in a scene.
    /// </para>
    /// </remarks>
    [DefaultExecutionOrder(500)]
    public class VehicleChanger : MonoBehaviour
    {
        /// <summary>
        /// Represents the player's spatial relationship to vehicles.
        /// </summary>
        public enum CharacterLocation
        {
            /// <summary>
            /// Player is too far from any vehicle to interact.
            /// </summary>
            OutOfRange,

            /// <summary>
            /// Player is close enough to enter a vehicle.
            /// </summary>
            Near,

            /// <summary>
            /// Player is currently inside a vehicle.
            /// </summary>
            Inside,
        }

        /// <summary>
        /// Index of the current vehicle in vehicles list.
        /// </summary>
        [Tooltip("    Index of the current vehicle in vehicles list.")]
        public int activeVehicleIndex;

        /// <summary>
        /// Is vehicle changing character based? When true changing vehicles will require getting close to them
        /// to be able to enter, opposed to pressing a button to switch between vehicles.
        /// </summary>
        [Tooltip(
            "Is vehicle changing character based? When true changing vehicles will require getting close to them\r\nto be able to enter, opposed to pressing a button to switch between vehicles.")]
        public bool characterBased;

        /// <summary>
        /// Game object representing a character. Can also be another vehicle.
        /// </summary>
        [Tooltip("    Game object representing a character. Can also be another vehicle.")]
        public GameObject characterObject;

        /// <summary>
        /// Maximum distance at which the character will be able to enter the vehicle.
        /// </summary>
        [Range(0.2f, 3f)]
        [Tooltip("    Maximum distance at which the character will be able to enter the vehicle.")]
        public float enterDistance = 2f;

        /// <summary>
        /// Tag of the object representing the point from which the enter distance will be measured. Useful if you want to
        /// enable you character to enter only when near the door.
        /// </summary>
        [Tooltip(
            "Tag of the object representing the point from which the enter distance will be measured. Useful if you want to enable you character to enter only when near the door.")]
        public string enterExitTag = "EnterExitPoint";

        /// <summary>
        /// When the location is Near, the player can enter the vehicle.
        /// </summary>
        [Tooltip("When the location is Near, the player can enter the vehicle.")]
        public CharacterLocation location = CharacterLocation.OutOfRange;

        /// <summary>
        /// Maximum speed at which the character will be able to enter / exit the vehicle.
        /// </summary>
        [Tooltip("    Maximum speed at which the character will be able to enter / exit the vehicle.")]
        public float maxEnterExitVehicleSpeed = 2f;

        /// <summary>
        /// Event invoked when all vehicles are deactivated (e.g., when exiting in character-based mode).
        /// </summary>
        public UnityEvent onDeactivateAll = new();

        /// <summary>
        /// Event invoked whenever the active vehicle changes.
        /// </summary>
        public UnityEvent onVehicleChanged = new();

        /// <summary>
        /// Should the vehicles that the player is currently not using be put to sleep to improve performance?
        /// </summary>
        [Tooltip(
            "    Should the vehicles that the player is currently not using be put to sleep to improve performance?")]
        public bool putOtherVehiclesToSleep = true;

        /// <summary>
        /// Should the player start inside the vehicle?
        /// </summary>
        [Tooltip("Should the player start inside the vehicle?")]
        public bool startInVehicle;

        /// <summary>
        /// List of all of the vehicles that can be selected and driven in the scene.
        /// </summary>
        [Tooltip("List of all of the vehicles that can be selected and driven in the scene.")]
        public List<Vehicle> vehicles = new();

        private GameObject[] _enterExitPoints;
        private bool         _enterExitPointsDirty = true;
        private GameObject   _nearestEnterExitPoint;

        /// <summary>
        /// The vehicle the player is nearest to, or in case the player is inside the vehicle, the vehicle the player is inside
        /// of.
        /// </summary>
        private Vehicle _nearestVehicle;

        private Vector3 _relativeEnterPosition;

        public static VehicleChanger Instance { get; private set; }

        private static Vehicle ActiveVehicle
        {
            get
            {
                if (Instance == null)
                {
                    return null;
                }

                return Instance.activeVehicleIndex < 0 || Instance.activeVehicleIndex >= Instance.vehicles.Count
                           ? null
                           : Instance.vehicles[Instance.activeVehicleIndex];
            }
        }


        private void Awake()
        {
            Instance = this;

            // Remove null vehicles from the vehicles list
            for (int i = vehicles.Count - 1; i >= 0; i--)
            {
                if (vehicles[i] == null)
                {
                    Debug.LogWarning("There is a null reference in the vehicles list. Removing. Make sure that" +
                                     " vehicles list does not contain any null references.");
                    vehicles.RemoveAt(i);
                }
            }
        }


        private void OnEnable()
        {
            _enterExitPointsDirty = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }


        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }


        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _enterExitPointsDirty = true;
        }


        private void Start()
        {
            if (characterBased && !startInVehicle)
            {
                DeactivateAllIncludingActive();
            }
            else
            {
                DeactivateAllExceptActive();
            }

            if (startInVehicle && ActiveVehicle != null)
            {
                EnterVehicle(ActiveVehicle);
                _relativeEnterPosition = new Vector3(-2.5f, 1f, 0.5f); // There was no enter/exit point, make a guess
            }
        }


        private void Update()
        {
            if (!characterBased)
            {
                bool changeVehicleInput = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.ChangeVehicle());

                if (changeVehicleInput)
                {
                    NextVehicle();
                }
            }
            else if (characterObject != null)
            {
                if (location != CharacterLocation.Inside)
                {
                    location = CharacterLocation.OutOfRange;

                    if (!characterObject.activeSelf)
                    {
                        characterObject.SetActive(true);
                    }

                    // Cache enter/exit points until scene changes
                    if (_enterExitPointsDirty)
                    {
                        _enterExitPoints = GameObject.FindGameObjectsWithTag(enterExitTag);
                        _enterExitPointsDirty = false;
                    }

                    _nearestEnterExitPoint = null;
                    float nearestSqrDist = Mathf.Infinity;

                    foreach (GameObject eep in _enterExitPoints)
                    {
                        float sqrDist =
                            Vector3.SqrMagnitude(characterObject.transform.position - eep.transform.position);
                        if (sqrDist < nearestSqrDist)
                        {
                            nearestSqrDist         = sqrDist;
                            _nearestEnterExitPoint = eep;
                        }
                    }

                    if (_nearestEnterExitPoint == null)
                    {
                        return;
                    }

                    if (Vector3.Magnitude(Vector3.ProjectOnPlane(
                                              _nearestEnterExitPoint.transform.position -
                                              characterObject.transform.position,
                                              Vector3.up)) < enterDistance)
                    {
                        location        = CharacterLocation.Near;
                        _nearestVehicle = _nearestEnterExitPoint.GetComponentInParent<Vehicle>();
                    }
                }

                bool changeVehiclePressed = InputProvider.CombinedInput<SceneInputProviderBase>(i => i.ChangeVehicle());
                if (InputProvider.Instances.Count > 0 && changeVehiclePressed)
                {
                    // Enter vehicle
                    if (location == CharacterLocation.Near && _nearestVehicle.Speed < maxEnterExitVehicleSpeed)
                    {
                        EnterVehicle(_nearestVehicle);
                    }

                    // Exit vehicle
                    else if (location == CharacterLocation.Inside && _nearestVehicle.Speed < maxEnterExitVehicleSpeed)
                    {
                        ExitVehicle(_nearestVehicle);
                    }
                }
            }
        }


        /// <summary>
        /// Puts the player inside the specified vehicle and activates it.
        /// In character-based mode, stores the entry position for later exit.
        /// </summary>
        /// <param name="v">Vehicle to enter.</param>
        public void EnterVehicle(Vehicle v)
        {
            _nearestVehicle = v;
            if (characterBased)
            {
                characterObject.SetActive(false);
                _relativeEnterPosition = v.transform.InverseTransformPoint(characterObject.transform.position);
                location               = CharacterLocation.Inside;
            }

            Instance.ChangeVehicle(v);
        }


        /// <summary>
        /// Removes the player from the vehicle and spawns the character object nearby.
        /// Character is positioned at the stored entry location.
        /// </summary>
        /// <param name="v">Vehicle to exit.</param>
        public void ExitVehicle(Vehicle v)
        {
            // Call deactivate all to deactivate on the same frame, preventing 2 audio listeners warning.
            Instance.DeactivateAllIncludingActive();
            location = CharacterLocation.OutOfRange;
            if (characterBased)
            {
                characterObject.transform.position = v.transform.TransformPoint(_relativeEnterPosition);
                characterObject.transform.forward  = v.transform.right;
                characterObject.transform.up       = Vector3.up;
                characterObject.SetActive(true);
            }
        }


        /// <summary>
        /// Adds a vehicle to the managed vehicles list if not already present.
        /// Newly registered vehicles are automatically disabled unless they are the active vehicle.
        /// </summary>
        /// <param name="v">Vehicle to register.</param>
        public void RegisterVehicle(Vehicle v)
        {
            if (!vehicles.Contains(v))
            {
                vehicles.Add(v);
                if (activeVehicleIndex != vehicles.Count - 1)
                {
                    v.enabled = false;
                }
            }
        }


        /// <summary>
        /// Removes a vehicle from the managed vehicles list.
        /// If the vehicle was active, automatically switches to the next vehicle.
        /// </summary>
        /// <param name="v">Vehicle to deregister.</param>
        public void DeregisterVehicle(Vehicle v)
        {
            if (ActiveVehicle == v)
            {
                NextVehicle();
            }

            vehicles.Remove(v);
        }


        /// <summary>
        /// Changes vehicle to requested vehicle.
        /// </summary>
        /// <param name="index"> Index of a vehicle in Vehicles list. </param>
        public void ChangeVehicle(int index)
        {
            if (vehicles.Count == 0)
            {
                return;
            }

            activeVehicleIndex = index;
            if (activeVehicleIndex >= vehicles.Count)
            {
                activeVehicleIndex = 0;
            }

            DeactivateAllExceptActive();

            onVehicleChanged.Invoke();
        }


        /// <summary>
        /// Switches to the specified vehicle if it exists in the vehicles list.
        /// </summary>
        /// <param name="ac">Vehicle reference to switch to.</param>
        public void ChangeVehicle(Vehicle ac)
        {
            int vehicleIndex = vehicles.IndexOf(ac);

            if (vehicleIndex >= 0)
            {
                ChangeVehicle(vehicleIndex);
            }
        }


        /// <summary>
        /// Switches to the next vehicle in the list, wrapping to the first vehicle when reaching the end.
        /// </summary>
        public void NextVehicle()
        {
            if (vehicles.Count == 1)
            {
                return;
            }

            ChangeVehicle(activeVehicleIndex + 1);
        }


        /// <summary>
        /// Switches to the previous vehicle in the list, wrapping to the last vehicle when at the beginning.
        /// </summary>
        public void PreviousVehicle()
        {
            if (vehicles.Count == 1)
            {
                return;
            }

            int previousIndex = activeVehicleIndex == 0 ? vehicles.Count - 1 : activeVehicleIndex - 1;

            ChangeVehicle(previousIndex);
        }


        /// <summary>
        /// Enables the current active vehicle and optionally disables all others based on putOtherVehiclesToSleep setting.
        /// </summary>
        public void DeactivateAllExceptActive()
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (i == activeVehicleIndex)
                {
                    vehicles[i].enabled = true;
                }
                else if (putOtherVehiclesToSleep)
                {
                    vehicles[i].enabled = false;
                }
            }
        }


        /// <summary>
        /// Disables all managed vehicles including the currently active one.
        /// Used when exiting vehicles in character-based mode.
        /// </summary>
        public void DeactivateAllIncludingActive()
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                vehicles[i].enabled = false;
            }

            onDeactivateAll.Invoke();
        }
    }
}

#if UNITY_EDITOR

namespace NWH.Common.SceneManagement
{
    [CustomEditor(typeof(VehicleChanger))]
    [CanEditMultipleObjects]
    public class VehicleChangerEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            VehicleChanger sc = drawer.GetObject<VehicleChanger>();

            drawer.BeginSubsection("Vehicles");
            drawer.ReorderableList("vehicles");

            //drawer.Field("deactivateAll");
            drawer.Field("putOtherVehiclesToSleep");
            drawer.Field("activeVehicleIndex");
            if (Application.isPlaying)
            {
                drawer.Label("Active Vehicle: " +
                             (Vehicle.ActiveVehicle == null ? "None" : Vehicle.ActiveVehicle.name));
            }

            drawer.EndSubsection();

            drawer.BeginSubsection("Character-based Switching");
            if (drawer.Field("characterBased").boolValue)
            {
                drawer.Field("characterObject");
                drawer.Field("enterDistance", true, "m");
                drawer.Field("startInVehicle");
                drawer.Field("enterExitTag");
                drawer.Field("maxEnterExitVehicleSpeed", true, "m/s");
                drawer.Field("location",                 false);
            }

            drawer.EndSubsection();

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