// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Collections.Generic;
using System.Linq;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Infohazard.HyperNav.Settings;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using Random = System.Random;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Infohazard.HyperNav {
    /// <summary>
    /// Base class for areas in which HyperNav pathfinding can occur.
    /// </summary>
    public abstract class NavAreaBase : MonoBehaviour, INavArea {
        protected static readonly Random IDRandomizer = new();

        protected static readonly List<string> ScenePaths = new();

        #region Serialized Fields

        // Runtime Settings

        [SerializeField]
        [Tooltip("The boundaries of the area.")]
        [HelpBox("The boundaries of the area in local space of the GameObject. " +
                 "The data will only be generated within these bounds, and it is not relevant for navigation outside.")]
        private Bounds _bounds = new(Vector3.zero, Vector3.one);

        [SerializeField]
        [Tooltip("The baked data for the area.")]
        protected ScriptableObject _data;

        [SerializeField]
        [NonReorderable]
        [Tooltip("External links for this area.")]
        protected NavExternalLinkData[] _externalLinks;

        [SerializeField]
        [NonReorderable]
        [Tooltip("Ranges of external links for this area (one element per region).")]
        protected SerializableRange[] _externalLinkRanges;

        [SerializeField]
        private ulong _dataVersionForExternalLinks;

        [SerializeField]
        [Tooltip("The unique ID for this area to identify it in pathfinding jobs and serialized data.")]
        protected long _instanceID;

        [SerializeField]
        [Tooltip("Whether to generate a random instance ID on awake.")]
        [HelpBox("Enables the area to get a random ID upon being initialized. This is necessary if you will " +
                 "spawn multiple areas from a prefab, since they would otherwise share the same ID.")]
        private bool _randomInstanceID;

        [SerializeField, Tooltip("Whether to automatically update native data if the area moves.")]
        [HelpBox("Generally, NavAreas do not move and this is not needed. However, if your area does move, " +
                 "for example due to a floating origin system, you can enable this setting to automatically " +
                 "detect this movement and update the native data. However, if your area is moving every frame " +
                 "for some reason (not recommended) you should not use this setting, as it will prevent pathfinding " +
                 "from ever completing.")]
        private bool _autoDetectMovement = false;

        [SerializeField]
        [Tooltip("Whether only regions connected to certain locations are considered valid.")]
        [HelpBox("Enables defining specific points from which navigation is possible. If a significant part of the " +
                 "area is not actually reachable, this can be used to avoid paying the extra baking cost.")]
        private bool _useStartLocations = false;

        [SerializeField]
        [NonReorderable]
        [Tooltip("Which start locations to use.")]
        [HelpBox(
            "If Use Start Locations is enabled, only regions connected to these locations will be considered valid. " +
            "These can be edited by dragging the gizmos in the scene view.")]
        private Vector3[] _startLocations;

        // Visualization Settings

        [SerializeField]
        [Tooltip("If set, only this region will be included in the visualization mesh.")]
        [HelpBox("If set, only this region will be included in the visualization mesh. " +
                 "This can be used to isolate a single region for debugging.")]
        private int _visualizationSoloRegion = -1;

        [SerializeField]
        [Tooltip("Whether to show the connections of a selected region.")]
        [HelpBox("Whether to show the connections of a selected region in the scene view. " +
                 "This can be used to debug issues with pathing between regions.")]
        private bool _visualizeNeighbors;

        [SerializeField]
        [Tooltip("Which region to visualize the neighbors of.")]
        [HelpBox("If Visualize Neighbors is enabled, this is the region to visualize.")]
        private int _visualizeNeighborsRegion;

        [SerializeField]
        [Tooltip("Whether to show the external links.")]
        [HelpBox("This shows the external links to other areas. Each link is drawn as a green arrow." +
                 "this can make the scene view quite busy and reduce performance, so it's generally best to keep this " +
                 "off most of the time.")]
        private bool _visualizeExternalLinks;

        [SerializeField]
        [Tooltip("Whether to show the vertex numbers of the preview mesh in the scene view.")]
        [HelpBox("Shows the index of each vertex and face in the scene view. " +
                 "Quite useful for debugging but terrible for performance.")]
        private bool _showVertexNumbers;

        [SerializeField]
        [Tooltip("Max distance from the camera at which vertex numbers will be shown.")]
        private float _showVertexNumbersRange = 2;

        [SerializeField]
        [Tooltip("Whether to visualize the bounds of each region in the scene view.")]
        [HelpBox("Used to visualize the bound planes which are used to determine if a point is inside a region.")]
        private bool _visualizeRegionBounds;

        [SerializeField, Tooltip("Whether to visualize the queries that are performed for a voxel when baking.")]
        [HelpBox("Used to visualize the queries that are performed for a voxel when baking.")]
        private bool _visualizeVoxelQueries;

        /// <summary>
        /// This is used to refer to the names of private fields in this class from a custom Editor.
        /// </summary>
        public static class PropNames {
            public const string Bounds = nameof(_bounds);
            public const string Data = nameof(_data);
            public const string ExternalLinks = nameof(_externalLinks);
            public const string ExternalLinkRanges = nameof(_externalLinkRanges);
            public const string SharedSettings = "_sharedSettings";
            public const string InstanceSettings = "_instanceSettings";
            public const string InstanceID = nameof(_instanceID);
            public const string RandomInstanceID = nameof(_randomInstanceID);
            public const string AutoDetectMovement = nameof(_autoDetectMovement);
            public const string UseStartLocations = nameof(_useStartLocations);
            public const string StartLocations = nameof(_startLocations);
            public const string VisualizationSoloRegion = nameof(_visualizationSoloRegion);
            public const string VisualizeNeighbors = nameof(_visualizeNeighbors);
            public const string VisualizeNeighborsRegion = nameof(_visualizeNeighborsRegion);
            public const string VisualizeExternalLinks = nameof(_visualizeExternalLinks);
            public const string ShowVertexNumbers = nameof(_showVertexNumbers);
            public const string ShowVertexNumbersRange = nameof(_showVertexNumbersRange);
            public const string VisualizeRegionBounds = nameof(_visualizeRegionBounds);
            public const string VisualizeVoxelQueries = nameof(_visualizeVoxelQueries);
            public const string IsMigratedToInstanceSettings = nameof(_isMigratedToInstanceSettings);
        }

        #endregion

        #region Serialized Fields - Deprecated

        // These fields are maintained for migration from previous versions.
        // They should not be used directly as they have no effect.
        [SerializeField, HideInInspector, UsedImplicitly]
        private LayerMask _blockingLayers = 1;

        [SerializeField, HideInInspector, UsedImplicitly]
        private bool _staticOnly = true;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _maxAgentRadius = 0.5f;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _maxExternalLinkDistanceToVolume = 1;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _maxExternalLinkDistanceToSurface = 1;

        [SerializeField, HideInInspector, UsedImplicitly]
        private float _voxelSize = 1;

        [SerializeField, HideInInspector]
        private bool _isMigratedToInstanceSettings = false;

        #endregion

        #region Private Fields

        protected Vector3 LastPosition;
        protected Quaternion LastRotation;
        protected bool IsAddedToDataMap;

        private bool _hasCheckedId;
        private bool _isDataDuplicated;

        private Mesh _previewMesh;

        #endregion

        #region Serialized Field Accessor Properties

        /// <summary>
        /// The boundaries of the area.
        /// </summary>
        /// <remarks>
        /// This cannot be set while the game is running.
        /// </remarks>
        public Bounds Bounds {
            get => _bounds;
            set {
                if (DebugUtility.CheckPlaying(true)) return;
                _bounds = value;
            }
        }

        /// <summary>
        /// The layer this area exists on.
        /// </summary>
        public abstract NavLayer Layer { get; set; }

        /// <inheritdoc />
        INavAreaData INavArea.Data => _data ? (INavAreaData) _data : null;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <summary>
        /// The external links for this area.
        /// </summary>
        public IReadOnlyList<NavExternalLinkData> ExternalLinks => _externalLinks;

        /// <summary>
        /// The ranges of external links for this area (one element per region).
        /// </summary>
        public IReadOnlyList<SerializableRange> ExternalLinkRanges => _externalLinkRanges;

        /// <summary>
        /// The version of the data when the external links were last updated.
        /// </summary>
        public ulong DataVersionForExternalLinks {
            get => _dataVersionForExternalLinks;
            protected set => _dataVersionForExternalLinks = value;
        }

        /// <summary>
        /// The unique ID for this area to identify it in pathfinding jobs and serialized data.
        /// </summary>
        public long InstanceID => _instanceID;

        /// <summary>
        /// Whether to generate a random instance ID on awake (use if instantiating dynamically).
        /// </summary>
        public bool RandomInstanceID {
            get => _randomInstanceID;
            set => _randomInstanceID = value;
        }

        /// <summary>
        /// Whether to automatically update native data if the area moves.
        /// </summary>
        /// <remarks>
        /// Note that if this is true and the area moves every frame, pathfinding will never be able to occur.
        /// </remarks>
        public bool AutoDetectMovement {
            get => _autoDetectMovement;
            set => _autoDetectMovement = value;
        }

        /// <summary>
        /// Base settings for the area, whether shared or instance.
        /// The returned object, while mutable, should not be modified directly as it may reference a ScriptableObject.
        /// </summary>
        public abstract NavAreaBaseSettings BaseSettings { get; }

        /// <summary>
        /// Type of settings asset for this area (should inherit from NavAreaBaseSettings).
        /// </summary>
        public abstract Type SettingsAssetType { get; }

        /// <summary>
        /// Whether only regions connected to certain locations are considered valid.
        /// </summary>
        /// <remarks>
        /// This can be used to exclude certain regions from an area, such as regions that are outside reachable area.
        /// </remarks>
        public bool UseStartLocations {
            get => _useStartLocations;
            set => _useStartLocations = value;
        }

        /// <summary>
        /// If <see cref="_useStartLocations"/> is true, which start locations to use.
        /// </summary>
        public IReadOnlyList<Vector3> StartLocations {
            get => _startLocations;
            set => _startLocations = value as Vector3[] ?? value.ToArray();
        }

        /// <summary>
        /// If set, only this region will be included in the visualization mesh.
        /// </summary>
        public int VisualizationSoloRegion {
            get => _visualizationSoloRegion;
            set => _visualizationSoloRegion = value;
        }

        /// <summary>
        /// Whether to show the connections of a selected region in the scene view.
        /// </summary>
        public bool VisualizeNeighbors {
            get => _visualizeNeighbors;
            set => _visualizeNeighbors = value;
        }

        /// <summary>
        /// If <see cref="_visualizeNeighbors"/> is true, which region to visualize in the scene view.
        /// </summary>
        public int VisualizeNeighborsRegion {
            get => _visualizeNeighborsRegion;
            set => _visualizeNeighborsRegion = value;
        }

        /// <summary>
        /// Whether to show the external links.
        /// </summary>
        public bool VisualizeExternalLinks {
            get => _visualizeExternalLinks;
            set => _visualizeExternalLinks = value;
        }

        /// <summary>
        /// Whether to show the vertex numbers of the preview mesh in the scene view (for debugging).
        /// </summary>
        public bool ShowVertexNumbers {
            get => _showVertexNumbers;
            set => _showVertexNumbers = value;
        }

        /// <summary>
        /// Max distance from the camera at which vertex numbers will be shown.
        /// </summary>
        public float ShowVertexNumbersRange {
            get => _showVertexNumbersRange;
            set => _showVertexNumbersRange = value;
        }

        /// <summary>
        /// Whether to visualize the bounds of each region in the scene view.
        /// </summary>
        public bool VisualizeRegionBounds {
            get => _visualizeRegionBounds;
            set => _visualizeRegionBounds = value;
        }

        /// <summary>
        /// Whether to visualize the queries that are performed for a voxel when baking.
        /// </summary>
        public bool VisualizeVoxelQueries {
            get => _visualizeVoxelQueries;
            set => _visualizeVoxelQueries = value;
        }

        #endregion

        #region Other Properties

        /// <summary>
        /// (Editor Only) Preview mesh that is rendered to visualize the area.
        /// </summary>
        /// <remarks>
        /// When set, the old mesh will be destroyed.
        /// The supplied mesh will have its HideFlags set to HideAndDontSave,
        /// meaning it will not be saved with the scene, but will not be destroyed except manually here.
        /// Therefore it is important that this value be set to null when the NavArea is destroyed,
        /// in order to avoid leaking memory.
        /// </remarks>
        public Mesh PreviewMesh {
            get => _previewMesh;
            set {
                // If no change, do nothing.
                if (_previewMesh == value) return;

                // Destroy the old mesh.
                if (_previewMesh) {
                    DestroyImmediate(_previewMesh);
                }

                // Set the value and set its hide flags.
                _previewMesh = value;
                if (value) {
                    value.hideFlags = HideFlags.HideAndDontSave;
                }
            }
        }

        /// <summary>
        /// (Editor Only) List of materials to use for drawing the preview mesh.
        /// </summary>
        /// <remarks>
        /// These should be references to assets so that they don't need to be destroyed.
        /// </remarks>
        public Material[] PreviewMaterials { get; set; }

        /// <summary>
        /// Returns whether the native data for this area has been created.
        /// </summary>
        public abstract bool IsNativeDataCreated { get; }

        #endregion

        #region Editor Code

#if UNITY_EDITOR

        public static Action<NavAreaBase> MigrationToInstanceSettings;

        public bool FailedToCalculateUniqueID { get; private set; }

        /// <summary>
        /// Update the unique ID of the area based on its object ID, and ensure its data is named correctly.
        /// </summary>
        public virtual void UpdateUniqueID() {
            // Get the object identifier of this component.
            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(this);
            PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(this);

            ulong localId = id.targetObjectId;
            FailedToCalculateUniqueID = false;

            if (status == PrefabInstanceStatus.Connected) {
                NavAreaBase assetObject = PrefabUtility.GetCorrespondingObjectFromSource(this);
                if (assetObject) {
                    id = GlobalObjectId.GetGlobalObjectIdSlow(assetObject);
                }
            } else if (id.assetGUID.GetHashCode() == 0 || localId == 0) {
                if (!TryGetLocalObjectIDForObjectInPrefabMode(out id)) {
                    FailedToCalculateUniqueID = true;
                    return;
                }
            }

            // Before an object is saved, it doesn't have an ID yet.
            // Just keep it as 0 until it's saved.
            ulong assetHash = (ulong) id.assetGUID.GetHashCode();
            if (id.targetObjectId == 0 || assetHash == 0) {
                FailedToCalculateUniqueID = true;
                return;
            }

            long newID = (long) (id.targetObjectId ^ id.targetPrefabId ^ assetHash);
            newID = Math.Abs(newID);

            // Mark it as checked so this is only done once per script load.
            _hasCheckedId = true;
            if (_instanceID == newID) return;

            using ChangeNavAreaData change = ChangeNavAreaData.Instance();

            bool isAdded = IsAddedToDataMap;
            if (isAdded) {
                Deregister(true);
            }

            // Allow undo and mark the scene/prefab as dirty.
            Undo.RecordObject(this, "Set Unique ID");
            _instanceID = newID;
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);

            if (isAdded) {
                Register();
            }
        }

        private bool TryGetLocalObjectIDForObjectInPrefabMode(out GlobalObjectId id) {
            id = default;

            int selfIndex = Array.IndexOf(gameObject.GetComponents<NavAreaBase>(), this);
            if (selfIndex == -1) return false;

            PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (stage == null) return false;

            GameObject rootObject = stage.prefabContentsRoot;

            string path = null;
            if (rootObject != gameObject) {
                path = rootObject.transform.GetRelativeTransformPath(transform);
                if (string.IsNullOrEmpty(path)) return false;
            }

            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(stage.assetPath);
            if (!prefabObject) return false;

            Transform correspondingTransformInPrefab = prefabObject.transform;
            if (path != null) {
                correspondingTransformInPrefab = prefabObject.transform.GetTransformAtRelativePath(path);
                if (!correspondingTransformInPrefab) return false;
            }

            NavAreaBase[] areas = correspondingTransformInPrefab.GetComponents<NavAreaBase>();
            if (areas.Length <= selfIndex) return false;

            NavAreaBase targetObject = areas[selfIndex];
            id = GlobalObjectId.GetGlobalObjectIdSlow(targetObject);
            return true;
        }

        /// <summary>
        /// Check if the script is in a prefab (not a scene object).
        /// </summary>
        /// <returns>Whether the script is in a prefab asset (not a prefab instance).</returns>
        protected virtual bool IsPrefab() {
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(gameObject);
            PrefabAssetType type = PrefabUtility.GetPrefabAssetType(gameObject);
            PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(gameObject);

            return (stage != null && transform.parent == null) ||
                   ((type == PrefabAssetType.Regular || type == PrefabAssetType.Variant) &&
                    status == PrefabInstanceStatus.NotAPrefab);
        }
#endif

        #endregion

        #region Unity Callbacks

        /// <summary>
        /// Register this area in the <see cref="Instances"/> dictionary and perform initialization.
        /// </summary>
        protected virtual void OnEnable() {
            if (Application.isPlaying) {
                Register();
            }

#if UNITY_EDITOR
            _isDataDuplicated = string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_data));

            if (!_isMigratedToInstanceSettings) {
                MigrationToInstanceSettings?.Invoke(this);
            }

            if (_data != null && _dataVersionForExternalLinks != ((INavAreaData) _data).Version) {
                Undo.RecordObject(this, "Update Data Version");
                _dataVersionForExternalLinks = ((INavAreaData) _data).Version;
                _externalLinks = Array.Empty<NavExternalLinkData>();
                _externalLinkRanges = Array.Empty<SerializableRange>();
            }
#endif
        }

        /// <summary>
        /// Remove this area from the <see cref="Instances"/> dictionary.
        /// </summary>
        protected virtual void OnDisable() {
            // Clear preview mesh so it is destroyed.
            PreviewMesh = null;

            Deregister(!Application.isPlaying);
        }

        /// <summary>
        /// Reset certain properties to the first NavArea on the object.
        /// </summary>
        protected virtual void Reset() {
            _isMigratedToInstanceSettings = true;

            NavAreaBase[] areasOnObject = GetComponents<NavAreaBase>();
            if (areasOnObject[0] == this) return;

            NavAreaBase other = areasOnObject[0];
            Bounds = other.Bounds;
            AutoDetectMovement = other.AutoDetectMovement;
        }

        /// <summary>
        /// Dispose native-side data for this area.
        /// </summary>
        protected virtual void OnDestroy() {
            // Clear preview mesh so it is destroyed.
            PreviewMesh = null;

            if (_isDataDuplicated) {
                DestroyImmediate(_data);
            }

            Deregister(true);
        }

        /// <summary>
        /// Update UniqueID in editor, and check movement.
        /// </summary>
        protected virtual void Update() {
#if UNITY_EDITOR
            if (!Application.isPlaying) {
                if (_instanceID == 0 || !(_hasCheckedId)) {
                    UpdateUniqueID();
                }
            }
#endif

            if (Application.isPlaying && _autoDetectMovement) {
                Vector3 pos = transform.position;
                Quaternion rot = transform.rotation;

                if (pos != LastPosition || rot != LastRotation) {
                    UpdateTransform();
                    LastPosition = pos;
                    LastRotation = rot;
                }
            }
        }

        #endregion

        #region Public Methods

        public abstract void Register();

        public abstract void Deregister(bool destroyData);

        protected void DuplicateAreaData() {
            Assert.IsTrue(Application.isPlaying);

            if (_isDataDuplicated) return;

            _data = Instantiate(_data);
            _isDataDuplicated = true;
        }

        public void ClearExternalLinks() {
            _externalLinkRanges = Array.Empty<SerializableRange>();
            _externalLinks = Array.Empty<NavExternalLinkData>();

            if (!IsNativeDataCreated) return;

            bool isAdded = IsAddedToDataMap;
            Deregister(true);

            if (isAdded) {
                Register();
            }
        }

        /// <summary>
        /// Update the native data of this NavArea.
        /// </summary>
        /// <remarks>
        /// This is called automatically if <see cref="AutoDetectMovement"/> is enabled.
        /// </remarks>
        public void UpdateTransform() {
            using ChangeNavAreaData change = ChangeNavAreaData.Instance();

            InternalUpdateTransform();
        }

        protected abstract void InternalUpdateTransform();

        public abstract void UpdateNativeExternalLinks(bool updateSerializedData, bool keepLinksToUnloadedScenes,
                                                       UnsafeList<NativeNavExternalLinkData> newLinks,
                                                       UnsafeArray<SerializableRange> newLinkRanges,
                                                       UnsafeArray<UnsafeList<NativeNavExternalLinkData>> manualLinks);

        public void SetStartLocation(int index, Vector3 position) {
            _startLocations[index] = position;
        }

        #endregion
    }

    /// <summary>
    /// Generic base class for areas in which HyperNav pathfinding can occur.
    /// </summary>
    public abstract class NavArea<TArea, TData, TNativeData, TPointers, TSettings> :
        NavAreaBase, INavArea<TData, TNativeData, TPointers>
        where TArea : NavArea<TArea, TData, TNativeData, TPointers, TSettings>
        where TData : ScriptableObject, INavAreaData<TNativeData, TPointers>
        where TNativeData : unmanaged, IDisposable, INativeNavAreaData
        where TPointers : struct, IDisposable, INativeNavAreaDataPointers
        where TSettings : NavAreaBaseSettings<TSettings>, new() {
        #region Private Fields

        [SerializeField]
        [Expandable(showChildTypes: true, OnlyShowMainLine = true)]
        [HelpBox("Allows creating a settings definition shared between multiple areas of the same type. " +
                 "If this is set, Instance Settings will be ignored.")]
        private NavAreaBaseSettingsAsset<TSettings> _sharedSettings;

        [SerializeField]
        private TSettings _instanceSettings;

        private TNativeData _nativeData;
        private TPointers _pointers;

        #endregion

        #region Serialized Field Accessor Properties

        /// <inheritdoc />
        public TData Data {
            get => (TData) _data;
            set {
                if (DebugUtility.CheckPlaying(true)) return;
                _data = value;
            }
        }

        /// <summary>
        /// The native data for the volume. Will not be initialized until the volume is registered.
        /// </summary>
        public ref readonly TNativeData NativeData => ref _nativeData;

        /// <summary>
        /// The internal pointers to the native data for the volume.
        /// </summary>
        public ref readonly TPointers DataStructurePointers => ref _pointers;

        /// <summary>
        /// Settings specific to this area instance.
        /// On set, <see cref="SharedSettings"/> will be set to null.
        /// </summary>
        public TSettings InstanceSettings {
            get => _instanceSettings;
            set {
                _instanceSettings =
                    value ?? throw new ArgumentNullException(nameof(value), "Instance settings cannot be null.");
                _sharedSettings = null;
            }
        }

        /// <summary>
        /// Shared settings asset for all areas of this type.
        /// If not null, <see cref="InstanceSettings"/> will be ignored.
        /// </summary>
        public NavAreaBaseSettingsAsset<TSettings> SharedSettings {
            get => _sharedSettings;
            set => _sharedSettings = value;
        }

        /// <summary>
        /// The settings object in use by this area, whether shared or instance.
        /// The returned object, while mutable, should not be modified directly as it may reference a ScriptableObject.
        /// </summary>
        public TSettings Settings => _sharedSettings ? _sharedSettings.Data : _instanceSettings;

        /// <inheritdoc />
        public override NavAreaBaseSettings BaseSettings => Settings;

        /// <inheritdoc />
        public override bool IsNativeDataCreated => _nativeData.IsCreated;

        /// <inheritdoc />
        public override NavLayer Layer {
            get => Settings.Layer;
            set {
                if (value == Settings.Layer) return;
                using ChangeNavAreaData c = ChangeNavAreaData.Instance();
                Settings.Layer = value;
                InternalUpdateTransform();
            }
        }

        #endregion

        #region Static Properties

        private static readonly Dictionary<long, TArea> InternalInstances = new();

        /// <summary>
        /// Data for all loaded volumes in the format used by jobs.
        /// </summary>
        public static NativeParallelHashMap<long, TNativeData> NativeDataMap;

        /// <summary>
        /// All currently loaded volumes.
        /// </summary>
        public static IReadOnlyDictionary<long, TArea> Instances => InternalInstances;

        #endregion

        protected override void Reset() {
            base.Reset();

            NavAreaBase[] areasOnObject = GetComponents<NavAreaBase>();
            if (areasOnObject[0] == this) return;

            NavAreaBase other = areasOnObject[0];

            _instanceSettings.BlockingLayers = other.BaseSettings.BlockingLayers;
            _instanceSettings.StaticOnly = other.BaseSettings.StaticOnly;
            _instanceSettings.MaxAgentRadius = other.BaseSettings.MaxAgentRadius;
        }

        #region Public Methods

        /// <summary>
        /// Register this volume in the <see cref="Instances"/> dictionary and perform initialization.
        /// This can be used in edit mode to create the native data. It does not need to be called at runtime.
        /// </summary>
        public override void Register() {
            // If disabled or already added, do nothing.
            if (!isActiveAndEnabled || IsAddedToDataMap) return;

            // If neither the serialized data OR native data are created, do nothing.
            if ((Data == null || !Data.IsBaked) && !_nativeData.IsCreated) return;

            // If we have serialized data but native data is not created, create it.
            if (!_nativeData.IsCreated) {
                if (RandomInstanceID && Application.isPlaying) {
                    _instanceID = IDRandomizer.NextLong(1, long.MaxValue);
                }

                bool result = Data.ToNativeData(this, out _nativeData, out _pointers);

                if (!result) {
                    Debug.LogError("Failed to create native data for NavVolume. It may need to be re-baked.", this);
                    return;
                }
            }

            InternalInstances[InstanceID] = (TArea) this;

            LastPosition = transform.position;
            LastRotation = transform.rotation;

            using (ChangeNavAreaData.Instance()) {
                // Create native-side dictionary if needed.
                if (!NativeDataMap.IsCreated) {
                    NativeDataMap = new NativeParallelHashMap<long, TNativeData>(8, Allocator.Persistent);
                }

                // Register in native-side dictionary.
                NativeDataMap[InstanceID] = _nativeData;
            }

            IsAddedToDataMap = true;
        }

        /// <summary>
        /// Remove this volume from the <see cref="Instances"/> dictionary, and optionally destroy its native data.
        /// </summary>
        /// <param name="destroyData">Whether to free the memory used for native data.</param>
        public override void Deregister(bool destroyData) {
            if (!_nativeData.IsCreated) return;

            if (IsAddedToDataMap) {
                using (ChangeNavAreaData.Instance()) {
                    // Deregister from managed-side dictionary.
                    InternalInstances.Remove(InstanceID);

                    // Deregister from native-side dictionary if it's created.
                    if (NativeDataMap.IsCreated) {
                        NativeDataMap.Remove(InstanceID);
                        // ReSharper disable once UseMethodAny.2
                        if (NativeDataMap.Count() == 0) {
                            NativeDataMap.Dispose();
                            NativeDataMap = default;
                        }
                    }
                }

                IsAddedToDataMap = false;
            }

            if (destroyData) {
                _pointers.Dispose();
                _nativeData.Dispose();
                _pointers = default;
                _nativeData = default; // Avoid dangling pointers
            }
        }

        private bool TryGetGameObjectForExternalLink(in NativeNavExternalLinkData link, out GameObject obj) {
            obj = null;

            if (link.ToAreaType == NavAreaTypes.Volume) {
                if (!NavVolume.Instances.TryGetValue(link.ToArea, out NavVolume otherVolume)) {
                    return false;
                }

                obj = otherVolume.gameObject;
                return true;
            } else {
                if (!NavSurface.Instances.TryGetValue(link.ToArea, out NavSurface otherSurface)) {
                    return false;
                }

                obj = otherSurface.gameObject;
                return true;
            }
        }

        public override unsafe void UpdateNativeExternalLinks(
            bool updateSerializedData,
            bool keepLinksToUnloadedScenes,
            UnsafeList<NativeNavExternalLinkData> newLinksInput,
            UnsafeArray<SerializableRange> newLinkRanges,
            UnsafeArray<UnsafeList<NativeNavExternalLinkData>> manualLinks) {
            ScenePaths.Clear();

            TNativeData nativeData = NativeData;
            TPointers pointers = DataStructurePointers;

            UnsafeList<NativeNavExternalLinkData> newLinks = new(newLinksInput.Length, Allocator.Persistent);

            int regionCount = nativeData.RegionCount;

            Span<SerializableRange> ranges = stackalloc SerializableRange[regionCount];

            for (int j = 0; j < regionCount; j++) {
                SerializableRange range = new(newLinks.Length, 0);

                if (keepLinksToUnloadedScenes && ExternalLinkRanges.Count > j) {
                    SerializableRange oldRange = ExternalLinkRanges[j];
                    for (int k = oldRange.Start; k < oldRange.End; k++) {
                        NavExternalLinkData link = ExternalLinks[k];
                        if (string.IsNullOrEmpty(link.ConnectedScenePath) ||
                            SceneManager.GetSceneByPath(link.ConnectedScenePath).isLoaded) continue;

                        range.Length++;
                        newLinks.Add(link.ToNativeData());
                        ScenePaths.Add(link.ConnectedScenePath);
                    }
                }

                SerializableRange rangeFromJob = newLinkRanges[j];
                for (int k = rangeFromJob.Start; k < rangeFromJob.End; k++) {
                    NativeNavExternalLinkData link = newLinksInput[k];

                    if (!TryGetGameObjectForExternalLink(link, out GameObject toObj)) {
                        Debug.LogError($"Area with ID {link.ToArea} not found.");
                        continue;
                    }

                    range.Length++;
                    newLinks.Add(newLinksInput[k]);
                    ScenePaths.Add(toObj.scene.path);
                }

                UnsafeList<NativeNavExternalLinkData> manualLinksForRegion =
                    manualLinks.IsNull ? default : manualLinks[j];

                for (int k = 0; k < manualLinksForRegion.Length; k++) {
                    NativeNavExternalLinkData link = manualLinksForRegion[k];
                    if (!TryGetGameObjectForExternalLink(link, out GameObject toObj)) {
                        Debug.LogError($"Area with ID {link.ToArea} not found.");
                        continue;
                    }

                    range.Length++;
                    newLinks.Add(link);
                    ScenePaths.Add(toObj.scene.path);
                }

                ranges[j] = range;
            }

            pointers.ExternalLinksData.Dispose();
            pointers.ExternalLinksData = default;

            // Alias the newLinks list as an array.
            // Since the allocator is passed in, the array now owns the memory and the list can be forgotten.
            UnsafeArray<NativeNavExternalLinkData> newLinksArray =
                new(newLinks.Ptr, newLinks.Length, Allocator.Persistent);

            nativeData.UpdateExternalLinksInPlace(newLinksArray, ranges);

            UpdateNativeData(nativeData, pointers, false, updateSerializedData, ScenePaths);
        }

        /// <summary>
        /// Update native data for the nav area, optionally referencing new arrays.
        /// </summary>
        /// <remarks>
        /// This does NOT automatically dispose the old arrays, as they may be shared with the new data.
        /// If you are not reusing the old arrays, you should dispose them manually.
        /// </remarks>
        /// <param name="newData">New native data.</param>
        /// <param name="newPointers">New pointers to arrays.</param>
        /// <param name="updateSerializedAreaData">Whether to update the serialized area ScriptableObject as well.
        /// Note that this will incur a major performance cost because it will allocate managed memory.
        /// If this is used at runtime, it will copy the ScriptableObject to avoid modifying assets.</param>
        /// <param name="updateSerializedLinkData">Whether to update the serialized external link data as well.
        /// Note that this will incur a performance cost because it will allocate managed memory.</param>
        /// <param name="externalLinkScenePaths">If updating serialized link data, should contain the scene paths
        /// for each external link in the <see cref="newData"/>.<see cref="ExternalLinks"/> array.
        /// </param>
        public void UpdateNativeData(in TNativeData newData, in TPointers newPointers,
                                     bool updateSerializedAreaData, bool updateSerializedLinkData,
                                     IReadOnlyList<string> externalLinkScenePaths = null) {
            using ChangeNavAreaData change = ChangeNavAreaData.Instance();

            _nativeData = newData;
            _pointers = newPointers;

            // This can happen if two areas share an instance ID.
            if (IsAddedToDataMap && !NativeDataMap.IsCreated) {
                IsAddedToDataMap = false;
            }

            if (IsAddedToDataMap) {
                NativeDataMap[InstanceID] = _nativeData;
            }

            if (updateSerializedLinkData) {
                int externalLinkCount = newData.ExternalLinkCount;
                int regionCount = newData.RegionCount;

                if (externalLinkCount > 0) {
                    Assert.IsNotNull(externalLinkScenePaths);
                    Assert.AreEqual(externalLinkCount, externalLinkScenePaths.Count);
                }

                if (_externalLinks?.Length != externalLinkCount)
                    _externalLinks = new NavExternalLinkData[externalLinkCount];

                if (_externalLinkRanges?.Length != regionCount)
                    _externalLinkRanges = new SerializableRange[regionCount];

                unsafe {
                    fixed (TNativeData* newDataPtr = &newData) {
                        for (int i = 0; i < _externalLinks.Length; i++) {
                            NativeNavExternalLinkData nativeLink = newDataPtr->GetExternalLinkData(i);
                            string toScenePath = externalLinkScenePaths![i];
                            _externalLinks[i] = new NavExternalLinkData(nativeLink, toScenePath);
                        }

                        for (int i = 0; i < _externalLinkRanges.Length; i++) {
                            _externalLinkRanges[i] = newDataPtr->GetExternalLinkRange(i);
                        }
                    }
                }
            }

            if (updateSerializedAreaData) {
                if (Application.isPlaying) {
                    DuplicateAreaData();
                }

                Data.UpdateFromNativeData(newData);
            }

            DataVersionForExternalLinks = Data?.Version ?? 0;
        }

        /// <summary>
        /// Update the native data on all loaded NavAreas.
        /// Note, this must be called on each area type class (NavSurface and NavVolume) separately.
        /// </summary>
        /// <remarks>
        /// Use this after moving all areas when <see cref="AutoDetectMovement"/> is disabled.
        /// </remarks>
        public static void UpdateAllTransforms() {
            using ChangeNavAreaData change = ChangeNavAreaData.Instance();

            foreach (KeyValuePair<long, TArea> pair in InternalInstances) {
                pair.Value.InternalUpdateTransform();
            }
        }

        protected override void InternalUpdateTransform() {
            _nativeData = RebuildNativeData();

            if (IsAddedToDataMap && !NativeDataMap.IsCreated) {
                IsAddedToDataMap = false;
            }

            if (IsAddedToDataMap) {
                NativeDataMap[InstanceID] = _nativeData;
            }
        }

        protected abstract TNativeData RebuildNativeData();

        #endregion
    }
}
