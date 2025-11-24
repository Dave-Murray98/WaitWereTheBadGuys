// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System.Collections.Generic;
using Infohazard.Core;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Infohazard.HyperNav {
    public interface INavArea {
        /// <summary>
        /// The transform of the area.
        /// </summary>
        public Transform Transform { get; }

        /// <summary>
        /// External links to other areas.
        /// </summary>
        public IReadOnlyList<NavExternalLinkData> ExternalLinks { get; }

        /// <summary>
        /// For each region, the range of external links that originate from that region.
        /// </summary>
        public IReadOnlyList<SerializableRange> ExternalLinkRanges { get; }

        /// <summary>
        /// The unique ID of the area.
        /// </summary>
        public long InstanceID { get; }

        /// <summary>
        /// The bounds of the area in local space.
        /// </summary>
        public Bounds Bounds { get; }

        /// <summary>
        /// The layer this area exists on.
        /// </summary>
        public NavLayer Layer { get; }

        /// <summary>
        /// The baked data for the area.
        /// </summary>
        public INavAreaData Data { get; }

#if UNITY_EDITOR
        public void UpdateUniqueID();
#endif

        /// <summary>
        /// Clear the generated external links.
        /// </summary>
        public void ClearExternalLinks();

        /// <summary>
        /// Update the external links with new native data. Optionally, update the serialized data s well.
        /// </summary>
        /// <param name="updateSerializedData">Weather to update the serialized data.</param>
        /// <param name="keepLinksToUnloadedScenes">
        /// If true, links to scenes that are not currently loaded will be kept.</param>
        /// <param name="newLinks">The new external links.</param>
        /// <param name="newLinkRanges">The new external link ranges.</param>
        /// <param name="manualLinks">Manual links to other areas.</param>
        public void UpdateNativeExternalLinks(
            bool updateSerializedData,
            bool keepLinksToUnloadedScenes,
            UnsafeList<NativeNavExternalLinkData> newLinks,
            UnsafeArray<SerializableRange> newLinkRanges,
            UnsafeArray<UnsafeList<NativeNavExternalLinkData>> manualLinks);
    }

    public interface INavArea<out TData, TNativeData, TPointers> : INavArea
        where TData : INavAreaData<TNativeData, TPointers>
        where TNativeData : INativeNavAreaData {

        /// <summary>
        /// The baked data for the area.
        /// </summary>
        public new TData Data { get; }

        /// <summary>
        /// The native data for the volume. Will not be initialized until the volume is registered.
        /// </summary>
        public ref readonly TNativeData NativeData { get; }

        /// <summary>
        /// The internal pointers to the native data for the volume.
        /// </summary>
        public ref readonly TPointers DataStructurePointers { get; }

        /// <summary>
        /// Update the area with new native data. Optionally, update the serialized data as well.
        /// </summary>
        /// <param name="newData">The new native data.</param>
        /// <param name="newPointers">Tracked pointers to the new native data.</param>
        /// <param name="updateSerializedAreaData">Whether to update the serialized area data.</param>
        /// <param name="updateSerializedLinkData">Whether to update the serialized link data.</param>
        /// <param name="externalLinkScenePaths">The paths to the scenes that contain the external links.</param>
        public void UpdateNativeData(in TNativeData newData, in TPointers newPointers,
                                     bool updateSerializedAreaData, bool updateSerializedLinkData,
                                     IReadOnlyList<string> externalLinkScenePaths = null);
    }
}
