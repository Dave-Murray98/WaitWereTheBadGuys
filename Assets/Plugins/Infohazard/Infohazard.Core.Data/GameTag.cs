using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Infohazard.Core {
    public static class GameTag {
        public const string @Untagged = @"Untagged";
        public const string @Respawn = @"Respawn";
        public const string @Finish = @"Finish";
        public const string @EditorOnly = @"EditorOnly";
        public const string @MainCamera = @"MainCamera";
        public const string @Player = @"Player";
        public const string @GameController = @"GameController";
        public const string @LoadingScreen = @"LoadingScreen";
        public const string @FXBlockZone = @"FX Block Zone";
        public const string @Ship = @"Ship";
        public const string @FirstPersonRenderCamera = @"FirstPersonRenderCamera";
        public const string @Vehicle = @"Vehicle";
        public const string @Wheel = @"Wheel";
        public const string @LowPriority = @"LowPriority";
        public const string @HighPriority = @"HighPriority";

        public const long @UntaggedMask = 1 << 0;
        public const long @RespawnMask = 1 << 1;
        public const long @FinishMask = 1 << 2;
        public const long @EditorOnlyMask = 1 << 3;
        public const long @MainCameraMask = 1 << 4;
        public const long @PlayerMask = 1 << 5;
        public const long @GameControllerMask = 1 << 6;
        public const long @LoadingScreenMask = 1 << 7;
        public const long @FXBlockZoneMask = 1 << 8;
        public const long @ShipMask = 1 << 9;
        public const long @FirstPersonRenderCameraMask = 1 << 10;
        public const long @VehicleMask = 1 << 11;
        public const long @WheelMask = 1 << 12;
        public const long @LowPriorityMask = 1 << 13;
        public const long @HighPriorityMask = 1 << 14;

        public static readonly string[] Tags = {
            @"Untagged", @"Respawn", @"Finish", @"EditorOnly", @"MainCamera", @"Player", @"GameController", @"LoadingScreen", @"FX Block Zone", @"Ship", @"FirstPersonRenderCamera", @"Vehicle", @"Wheel", @"LowPriority", @"HighPriority", 
        };

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize() {
            Tag.GameOverrideTags = Tags;
        }
    }
}
