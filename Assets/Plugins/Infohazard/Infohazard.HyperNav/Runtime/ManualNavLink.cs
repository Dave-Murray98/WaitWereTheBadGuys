// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Infohazard.HyperNav {
    [ExecuteAlways]
    public class ManualNavLink : MonoBehaviour {
        private static readonly List<ManualNavLink> InternalLinks = new();

        /// <summary>
        /// A list of all loaded ManualNavLinks.
        /// </summary>
        public static IReadOnlyList<ManualNavLink> AllLinks => InternalLinks;

        private static NativeHashMap<long, bool> _linksEnabled;

        /// <summary>
        /// A map of link instance IDs to whether they are enabled.
        /// </summary>
        public static NativeHashMap<long, bool> LinksEnabled =>
            _linksEnabled.IsCreated ? _linksEnabled : _linksEnabledNone;

        private static NativeHashMap<long, bool> _linksEnabledNone = default;

        static ManualNavLink() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

            FieldInfo field =
                typeof(NativeHashMap<long, bool>).GetField("m_Safety", BindingFlags.NonPublic | BindingFlags.Instance)!;
            field.SetValueDirect(__makeref(_linksEnabledNone), AtomicSafetyHandle.Create());

#endif
        }

        /// <summary>
        /// The unique ID for this link to identify it in serialized data.
        /// </summary>
        [SerializeField]
        private long _instanceID;

        /// <summary>
        /// Point where the link starts, in local space.
        /// </summary>
        [SerializeField]
        private Vector3 _localStartPoint = new(0, 0, -1);

        /// <summary>
        /// Point where the link ends, in local space.
        /// </summary>
        [SerializeField]
        private Vector3 _localEndPoint = new(0, 0, 1);

        /// <summary>
        /// The area types that the link can start from.
        /// </summary>
        [SerializeField]
        private NavAreaTypes _startTypes = NavAreaTypes.All;

        /// <summary>
        /// The area types that the link can end at.
        /// </summary>
        [SerializeField]
        private NavAreaTypes _endTypes = NavAreaTypes.All;

        /// <summary>
        /// The radius of the area sample at the link start and end points.
        /// </summary>
        [SerializeField]
        private float _sampleRadius = 0.5f;

        /// <summary>
        /// Whether the link is bidirectional.
        /// </summary>
        [SerializeField]
        private bool _isBidirectional = true;

        /// <summary>
        /// Use to control whether the link is enabled.
        /// </summary>
        /// <remarks>
        /// Use this instead of Behavior.enabled to avoid unregistering the link.
        /// </remarks>
        [SerializeField]
        private bool _isLinkEnabled = true;

        private bool _hasCheckedId;

        /// <summary>
        /// The unique ID for this link to identify it in serialized data.
        /// </summary>
        public long InstanceID => _instanceID;

        /// <summary>
        /// Point where the link starts, in local space.
        /// </summary>
        public Vector3 LocalStartPoint {
            get => _localStartPoint;
            set => _localStartPoint = value;
        }

        /// <summary>
        /// Point where the link ends, in local space.
        /// </summary>
        public Vector3 LocalEndPoint {
            get => _localEndPoint;
            set => _localEndPoint = value;
        }

        /// <summary>
        /// Point where the link starts, in world space.
        /// </summary>
        public Vector3 WorldStartPoint {
            get => transform.TransformPoint(_localStartPoint);
            set => _localStartPoint = transform.InverseTransformPoint(value);
        }

        /// <summary>
        /// Point where the link ends, in world space.
        /// </summary>
        public Vector3 WorldEndPoint {
            get => transform.TransformPoint(_localEndPoint);
            set => _localEndPoint = transform.InverseTransformPoint(value);
        }

        /// <summary>
        /// The area types that the link can start from.
        /// </summary>
        public NavAreaTypes StartTypes {
            get => _startTypes;
            set => _startTypes = value;
        }

        /// <summary>
        /// The area types that the link can end at.
        /// </summary>
        public NavAreaTypes EndTypes {
            get => _endTypes;
            set => _endTypes = value;
        }

        /// <summary>
        /// The radius of the area sample at the link start and end points.
        /// </summary>
        public float SampleRadius {
            get => _sampleRadius;
            set => _sampleRadius = value;
        }

        /// <summary>
        /// Whether the link is bidirectional.
        /// </summary>
        public bool IsBidirectional {
            get => _isBidirectional;
            set => _isBidirectional = value;
        }

        /// <summary>
        /// Use to control whether the link is enabled.
        /// </summary>
        /// <remarks>
        /// Use this instead of Behavior.enabled to avoid unregistering the link.
        /// </remarks>
        public bool IsLinkEnabled {
            get => _isLinkEnabled;
            set {
                _isLinkEnabled = value;
                if (enabled && _linksEnabled.IsCreated) {
                    using ChangeNavAreaData change = ChangeNavAreaData.Instance();
                    _linksEnabled[_instanceID] = value;
                }
            }
        }

        private void OnEnable() {
            if (!InternalLinks.Contains(this)) {
                InternalLinks.Add(this);
            }

            using ChangeNavAreaData change = ChangeNavAreaData.Instance();
            if (!_linksEnabled.IsCreated) {
                _linksEnabled = new NativeHashMap<long, bool>(64, Allocator.Persistent);
            }

            _linksEnabled[_instanceID] = _isLinkEnabled;
        }

        private void OnDisable() {
            InternalLinks.Remove(this);
            
            if (!_linksEnabled.IsCreated) return;

            using ChangeNavAreaData change = ChangeNavAreaData.Instance();
            _linksEnabled.Remove(_instanceID);

            if (_linksEnabled is { IsCreated: true, Count: 0 }) {
                _linksEnabled.Dispose();
                _linksEnabled = default;
            }
        }

#if UNITY_EDITOR

        private void Update() {
            if (!Application.isPlaying) {
                if (_instanceID == 0 || !_hasCheckedId) {
                    UpdateUniqueID();
                }
            }
        }

        private void OnDrawGizmos() {
            if (Selection.Contains(gameObject)) return;

            Color c = Gizmos.color;
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(WorldStartPoint, WorldEndPoint);
            Gizmos.color = c;
        }

        /// <summary>
        /// Update the unique ID of the volume based on its object ID, and ensure its data is named correctly.
        /// </summary>
        public virtual void UpdateUniqueID() {
            // Get the object identifier of this component.
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(this);

            ulong assetGUID = (ulong) id.assetGUID.GetHashCode();
            if (assetGUID == 0) {
                PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);
                if (stage != null) {
                    assetGUID = (ulong) AssetDatabase.GUIDFromAssetPath(stage.assetPath).GetHashCode();
                }
            }

            long newID = (long) (id.targetObjectId ^ id.targetPrefabId ^ assetGUID);
            newID = Math.Abs(newID);

            // Mark it as checked so this is only done once per script load.
            _hasCheckedId = true;

            // Allow undo and mark the scene/prefab as dirty.
            Undo.RecordObject(this, "Set Unique ID");
            _instanceID = newID;
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

#endif
    }
}
