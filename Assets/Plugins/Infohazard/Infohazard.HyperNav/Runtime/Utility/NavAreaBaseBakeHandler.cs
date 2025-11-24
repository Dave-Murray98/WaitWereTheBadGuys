// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Infohazard.HyperNav.Jobs.Utility;
using Unity.Collections;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Infohazard.HyperNav {
    /// <summary>
    /// Represents current bake state of a volume, including progress fraction and current operation display name.
    /// </summary>
    public struct NavAreaBakeProgress {
        public float Progress;
        public string Operation;
    }

    public abstract class NavAreaBaseBakeHandler {
        protected readonly Stopwatch Stopwatch = new();

        private readonly StringBuilder _stringBuilder = new();
        private readonly NavAreaBase _area;
        protected readonly bool SanityChecks;
        protected readonly bool UpdateSerializedData;
        private bool _started;
        private NativeCancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Invoked when the bake progress for the volume changes.
        /// </summary>
        public event Action ProgressUpdated;

        /// <summary>
        /// The current bake progress of the volume.
        /// </summary>
        public NavAreaBakeProgress Progress { get; private set; } = new() {
            Operation = "Waiting to start",
            Progress = 0f,
        };

        /// <summary>
        /// Create a new bake handler for a volume.
        /// </summary>
        /// <param name="area">Volume to bake.</param>
        /// <param name="sanityChecks">Whether to run sanity checks to catch baking issues.</param>
        /// <param name="updateSerializedData">Whether to update the serialized data after baking.</param>
        public NavAreaBaseBakeHandler(NavAreaBase area, bool sanityChecks, bool updateSerializedData) {
            _area = area;
            SanityChecks = sanityChecks;
            UpdateSerializedData = updateSerializedData;

            _cancellationTokenSource = new NativeCancellationTokenSource(Allocator.Persistent);
        }

        /// <summary>
        /// Run the bake process asynchronously.
        /// </summary>
        public async UniTask RunAsync(CancellationToken cancellationToken = default) {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            if (_started) {
                throw new InvalidOperationException("NavAreaBaseBakeHandler is not meant to be re-used.");
            }

            _started = true;

            if (cancellationToken != CancellationToken.None) {
                cancellationToken.Register(Cancel);
            }

            try {
                await GenerateData(_cancellationTokenSource.Token);
                if (_cancellationTokenSource.IsCancellationRequested) {
                    throw new OperationCanceledException();
                }
            } finally {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = default;
            }

#if UNITY_EDITOR
            if (UpdateSerializedData) {
                EditorUtility.SetDirty((Object)((INavArea)_area).Data);
                if (_area.gameObject.scene.IsValid()) {
                    EditorSceneManager.MarkSceneDirty(_area.gameObject.scene);
                }

                AssetDatabase.SaveAssets();
            }
#endif

            Debug.Log($"Time taken to bake data:{Environment.NewLine}{_stringBuilder}");
        }

        protected abstract UniTask GenerateData(NativeCancellationToken token);

        /// <summary>
        /// Cancel the bake process.
        /// </summary>
        public void Cancel() {
            _cancellationTokenSource.Cancel();
            _area.PreviewMesh = null;
        }

        protected void UpdateBakeProgress(string text, float progress) {
            Progress = new NavAreaBakeProgress {
                Operation = text,
                Progress = progress,
            };
            ProgressUpdated?.Invoke();
        }

        protected void LogStopwatch(string label) {
            _stringBuilder.Append($"{label}: {Stopwatch.ElapsedMilliseconds} ms{Environment.NewLine}");
        }
    }
}
