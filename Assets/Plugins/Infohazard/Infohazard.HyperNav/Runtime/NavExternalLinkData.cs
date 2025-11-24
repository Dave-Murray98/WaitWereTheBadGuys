// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace Infohazard.HyperNav {
    /// <summary>
    /// A connection from one region to another region in another volume.
    /// </summary>
    [Serializable]
    public class NavExternalLinkData {
        [SerializeField]
        private long _connectedAreaID;

        [SerializeField]
        private NavAreaTypes _connectedAreaType;

        [SerializeField]
        private int _connectedRegionID;

        [SerializeField]
        private Vector3 _fromPosition;

        [SerializeField]
        private Vector3 _toPosition;

        [SerializeField]
        private string _connectedScenePath;

        [SerializeField]
        private long _manualLinkID;

        /// <summary>
        /// The <see cref="NavVolume.InstanceID"/> of the connected volume.
        /// </summary>
        public long ConnectedAreaID => _connectedAreaID;

        /// <summary>
        /// The type of the connected area.
        /// </summary>
        public NavAreaTypes ConnectedAreaType => _connectedAreaType;

        /// <summary>
        /// The ID of the connected region.
        /// </summary>
        public int ConnectedRegionID => _connectedRegionID;

        /// <summary>
        /// The position at which the connection originates (local space).
        /// </summary>
        public Vector3 FromPosition => _fromPosition;

        /// <summary>
        /// The position at which the connection ends (local space).
        /// </summary>
        public Vector3 ToPosition => _toPosition;

        /// <summary>
        /// Scene path of the connected volume.
        /// </summary>
        public string ConnectedScenePath => _connectedScenePath;

        /// <summary>
        /// The unique ID of the ManualNavLink that created this link, or 0.
        /// </summary>
        public long ManualLinkID => _manualLinkID;

        /// <summary>
        /// Create a new NavExternalLinkData from the given native data.
        /// </summary>
        /// <param name="nativeData">Native data to copy.</param>
        /// <param name="connectedScenePath">Scene path of the connected volume.</param>
        /// <returns>The created NavExternalLinkData.</returns>
        public NavExternalLinkData(in NativeNavExternalLinkData nativeData, string connectedScenePath) {
            _connectedAreaID = nativeData.ToArea;
            _connectedAreaType = nativeData.ToAreaType;
            _connectedRegionID = nativeData.ToRegion;
            _fromPosition = nativeData.FromPosition.xyz;
            _toPosition = nativeData.ToPosition.xyz;
            _connectedScenePath = connectedScenePath;
            _manualLinkID = nativeData.ManualLinkID;
        }

        /// <summary>
        /// Convert to a native representation.
        /// </summary>
        /// <returns>Created native data.</returns>
        public NativeNavExternalLinkData ToNativeData() {
            float4 fromPosition = FromPosition.ToV4Pos();
            float4 toPosition = ToPosition.ToV4Pos();

            return new NativeNavExternalLinkData(ConnectedAreaID, _connectedAreaType, ConnectedRegionID, fromPosition,
                                                 toPosition, _manualLinkID);
        }
    }
}