// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using UnityEngine;

#endregion

namespace NWH.Common.AssetInfo
{
    /// <summary>
    /// ScriptableObject containing metadata and URLs for an NWH asset.
    /// Used by the welcome window and asset information systems.
    /// </summary>
    [CreateAssetMenu(fileName = "AssetInfo", menuName = "NWH/AssetInfo", order = 0)]
    public class AssetInfo : ScriptableObject
    {
        /// <summary>
        /// Display name of the asset.
        /// </summary>
        public string assetName = "Asset";

        /// <summary>
        /// Unity Asset Store URL for this asset.
        /// </summary>
        public string assetURL = "https://assetstore.unity.com/packages/tools/physics/nwh-vehicle-physics-2-166252";

        /// <summary>
        /// URL to the changelog documentation page.
        /// </summary>
        public string changelogURL = "";

        /// <summary>
        /// Discord server invite link for support and community.
        /// </summary>
        public string discordURL = "https://discord.gg/59CQGEJ";

        /// <summary>
        /// URL to the main documentation page.
        /// </summary>
        public string documentationURL = "";

        /// <summary>
        /// Support email contact link.
        /// </summary>
        public string emailURL = "mailto:arescec@gmail.com";

        /// <summary>
        /// Unity Forum thread URL for this asset.
        /// </summary>
        public string forumURL = "";

        /// <summary>
        /// URL to quick start guide documentation.
        /// </summary>
        public string quickStartURL = "";

        /// <summary>
        /// URL to upgrade notes between versions.
        /// </summary>
        public string upgradeNotesURL = "";

        /// <summary>
        /// Current version string of the asset.
        /// </summary>
        public string version = "1.0";

        /// <summary>
        /// Recent updates/changes in the current version (3-5 bullet points).
        /// </summary>
        [TextArea(3, 10)]
        public string[] recentUpdates = new string[0];

        /// <summary>
        /// NWH publisher page URL on Unity Asset Store.
        /// </summary>
        public string publisherURL = "https://assetstore.unity.com/publishers/14460";
    }
}