// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace Infohazard.HyperNav.Jobs.Utility {
    public unsafe struct NativeCancellationTokenSource : IDisposable, IEquatable<NativeCancellationTokenSource> {
        private int* _pointer;
        private readonly Allocator _allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private readonly AtomicSafetyHandle _safetyHandle;

        private static readonly int StaticSafetyId = AtomicSafetyHandle.NewStaticSafetyId<NativeCancellationToken>();
#endif

        public readonly bool IsCreated => _pointer != null;

        public bool IsCancellationRequested {
            get {
                if (_pointer == null) return false;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (!AtomicSafetyHandle.IsDefaultValue(_safetyHandle)) {
                    AtomicSafetyHandle.CheckReadAndThrow(_safetyHandle);
                }
#endif

                // Read from the buffer and return the value
                return UnsafeUtility.ReadArrayElement<int>(_pointer, 0) > 0;
            }
        }

        public readonly NativeCancellationToken Token {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (_pointer == null) {
                    throw new InvalidOperationException("NativeCancellationTokenSource has not been initialized.");
                }
#endif

                NativeCancellationToken token = new(_pointer);
                
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeCancellationTokenExtensions.SetAtomicSafetyHandle(ref token, _safetyHandle);
#endif
                
                return token;
            }
        }

        public readonly UnsafeCancellationToken UnsafeToken {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (_pointer == null) {
                    throw new InvalidOperationException("NativeCancellationTokenSource has not been initialized.");
                }
#endif

                return new UnsafeCancellationToken(_pointer);
            }
        }

        public NativeCancellationTokenSource(Allocator allocator) {
            _allocator = allocator;
            _pointer = (int*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<int>(), UnsafeUtility.AlignOf<int>(),
                _allocator, 0);
            UnsafeUtility.MemClear(_pointer, UnsafeUtility.SizeOf<int>());

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            _safetyHandle = AtomicSafetyHandle.Create();

            // Set the safety ID on the AtomicSafetyHandle so that error messages describe this container type properly.
            AtomicSafetyHandle.SetStaticSafetyId(ref _safetyHandle, StaticSafetyId);
#endif
        }

        public readonly void Cancel() {
            if (_pointer == null) {
                throw new InvalidOperationException("Trying to cancel non-created NativeCancellationTokenSource.");
            }

            // No safety check here. This is meant to be called from the main thread while a job is reading from it.
            *_pointer = 1;
        }

        public void Dispose() {
            if (_pointer == null) return;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (!AtomicSafetyHandle.IsDefaultValue(_safetyHandle)) {
                AtomicSafetyHandle.CheckDeallocateAndThrow(_safetyHandle);
            }
#endif

            UnsafeUtility.FreeTracked(_pointer, _allocator);
            _pointer = null;
        }

        public bool Equals(NativeCancellationTokenSource other) {
            return other._pointer == _pointer;
        }
    }

    [NativeContainer]
    [NativeContainerIsReadOnly]
    [BurstCompile]
    public unsafe struct NativeCancellationToken {
        [NativeDisableUnsafePtrRestriction] private int* _pointer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS

        // The AtomicSafetyHandle field must be named exactly 'm_Safety'.
        // ReSharper disable once InconsistentNaming
        internal AtomicSafetyHandle m_Safety;

#endif

        public static readonly NativeCancellationToken None = new() {
            _pointer = null,

            // Need to create a safety handle so the job safety system can accept it.
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = AtomicSafetyHandle.Create()
#endif
        };

        public bool IsCancellationRequested {
            get {
                if (_pointer == null) return false;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // Check that you can read from the native container right now.
                if (!AtomicSafetyHandle.IsDefaultValue(m_Safety)) {
                    AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
                }
#endif

                // Read from the buffer and return the value
                return UnsafeUtility.ReadArrayElement<int>(_pointer, 0) > 0;
            }
        }

        public NativeCancellationToken(int* pointer) {
            _pointer = pointer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_Safety = default;
#endif
        }

        public object ThrowIfCancellationRequested() {
            if (IsCancellationRequested) {
                throw new OperationCanceledException();
            }

            return null;
        }
    }

    [BurstCompile]
    public readonly unsafe struct UnsafeCancellationToken {
        private readonly int* _pointer;

        public static UnsafeCancellationToken None => new(null);

        public bool IsCancellationRequested {
            get {
                if (_pointer == null) return false;

                return UnsafeUtility.ReadArrayElement<int>(_pointer, 0) > 0;
            }
        }

        public UnsafeCancellationToken(int* pointer) {
            _pointer = pointer;
        }

        public object ThrowIfCancellationRequested() {
            if (IsCancellationRequested) {
                throw new OperationCanceledException();
            }

            return null;
        }
    }

    public static class NativeCancellationTokenExtensions {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public static void SetAtomicSafetyHandle(ref NativeCancellationToken token, AtomicSafetyHandle safety) {
            token.m_Safety = safety;
        }
#endif
    }
}