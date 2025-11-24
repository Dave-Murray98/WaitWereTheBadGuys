// This file is part of the HyperNav package.
// Copyright (c) 2022-present Vincent Miller (Infohazard Games).

using System;
using Infohazard.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Infohazard.HyperNav {
    public class SurfaceUprightDirectionOverride : MonoBehaviour {
        [SerializeField]
        private UprightDirectionMode _uprightMode = UprightDirectionMode.FixedWorldDirection;

        [SerializeField]
        [ConditionalDraw(nameof(_uprightMode), UprightDirectionMode.HitNormal, false)]
        private Vector3 _uprightDirection = Vector3.up;

        public UprightDirectionMode UprightMode {
            get => _uprightMode;
            set => _uprightMode = value;
        }

        public Vector3 UprightDirection {
            get => _uprightDirection;
            set => _uprightDirection = value;
        }

        public bool IsNormalDirection => _uprightMode == UprightDirectionMode.HitNormal;

        public Vector3 UprightWorldDirection {
            get {
                return _uprightMode switch {
                    UprightDirectionMode.FixedWorldDirection => _uprightDirection,
                    UprightDirectionMode.FixedLocalDirection =>
                        transform.TransformDirection(_uprightDirection),
                    _ => Vector3.zero,
                };
            }
        }

        public enum UprightDirectionMode {
            FixedWorldDirection,
            FixedLocalDirection,
            HitNormal
        }
    }
}
