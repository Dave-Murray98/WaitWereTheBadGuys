// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using UnityEngine;
using UnityEngine.UI;

#endregion

// Based on: https://forum.unity.com/threads/fpstotext-free-fps-framerate-calculator-with-options.463667/
namespace NWH.Common.Demo
{
    /// <summary>
    /// Displays the current framerate in a Text component with optional color coding.
    /// Supports both instantaneous and averaged FPS measurements.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class FpsToText : MonoBehaviour
    {
        /// <summary>
        /// Text color when framerate is below badBelow threshold.
        /// </summary>
        public  Color   bad             = Color.red;

        /// <summary>
        /// FPS threshold below which the color changes to bad (red).
        /// </summary>
        public  int     badBelow        = 30;

        /// <summary>
        /// Round FPS to nearest integer for cleaner display.
        /// </summary>
        public  bool    forceIntResult  = true;

        /// <summary>
        /// Text color when framerate is above okayBelow threshold.
        /// </summary>
        public  Color   good            = Color.green;

        /// <summary>
        /// Use averaging over multiple samples instead of single frame measurement.
        /// </summary>
        public  bool    groupSampling   = true;

        /// <summary>
        /// Text color when framerate is between badBelow and okayBelow.
        /// </summary>
        public  Color   okay            = Color.yellow;

        /// <summary>
        /// FPS threshold below which the color changes to okay (yellow).
        /// </summary>
        public  int     okayBelow       = 60;

        /// <summary>
        /// Number of samples to average when groupSampling is enabled.
        /// </summary>
        public  int     sampleSize      = 120;

        /// <summary>
        /// Use Time.smoothDeltaTime instead of Time.deltaTime for calculations.
        /// </summary>
        public  bool    smoothed        = true;

        /// <summary>
        /// Update text display every N frames. 1 = every frame.
        /// </summary>
        public  int     updateTextEvery = 1;

        /// <summary>
        /// Enable color coding based on framerate thresholds.
        /// </summary>
        public  bool    useColors       = true;

        /// <summary>
        /// Use Environment.TickCount instead of Time.deltaTime for calculations.
        /// </summary>
        public  bool    useSystemTick;
        private float   _fps;
        private float[] _fpsSamples;
        private int     _sampleIndex;
        private int     _sysFrameRate;
        private int     _sysLastFrameRate;

        private int _sysLastSysTick;

        private Text _targetText;
        private int  _textUpdateIndex;
        private System.Text.StringBuilder _stringBuilder;


        protected virtual void Start()
        {
            _targetText = GetComponent<Text>();
            _fpsSamples = new float[sampleSize];
            for (int i = 0; i < _fpsSamples.Length; i++)
            {
                _fpsSamples[i] = 0.001f;
            }

            _stringBuilder = new System.Text.StringBuilder(16);

            if (!_targetText)
            {
                enabled = false;
            }
        }


        protected virtual void Update()
        {
            if (_targetText == null)
            {
                return;
            }

            if (groupSampling)
            {
                Group();
            }
            else
            {
                SingleFrame();
            }

            _sampleIndex     = _sampleIndex < sampleSize - 1 ? _sampleIndex + 1 : 0;
            _textUpdateIndex = _textUpdateIndex > updateTextEvery ? 0 : _textUpdateIndex + 1;
            if (_textUpdateIndex == updateTextEvery)
            {
                _stringBuilder.Clear();
                _stringBuilder.Append((int)_fps);
                _targetText.text = _stringBuilder.ToString();
            }

            if (!useColors)
            {
                return;
            }

            if (_fps < badBelow)
            {
                _targetText.color = bad;
                return;
            }

            _targetText.color = _fps < okayBelow ? okay : good;
        }


        protected virtual void Reset()
        {
            sampleSize      = 20;
            updateTextEvery = 1;
            smoothed        = true;
            useColors       = true;
            good            = Color.green;
            okay            = Color.yellow;
            bad             = Color.red;
            okayBelow       = 60;
            badBelow        = 30;
            useSystemTick   = false;
            forceIntResult  = true;
        }


        protected virtual void SingleFrame()
        {
            if (useSystemTick)
            {
                _fps = GetSystemFramerate();
            }
            else
            {
                float deltaTime = smoothed ? Time.smoothDeltaTime : Time.deltaTime;
                _fps = deltaTime > 0.0001f ? 1f / deltaTime : 0f;
            }

            if (forceIntResult)
            {
                _fps = (int)_fps;
            }
        }


        protected virtual void Group()
        {
            if (useSystemTick)
            {
                _fpsSamples[_sampleIndex] = GetSystemFramerate();
            }
            else
            {
                float deltaTime = smoothed ? Time.smoothDeltaTime : Time.deltaTime;
                _fpsSamples[_sampleIndex] = deltaTime > 0.0001f ? 1f / deltaTime : 0f;
            }

            _fps = 0;
            bool loop = true;
            int  i    = 0;
            while (loop)
            {
                if (i == sampleSize - 1)
                {
                    loop = false;
                }

                _fps += _fpsSamples[i];
                i++;
            }

            _fps /= _fpsSamples.Length;
            if (forceIntResult)
            {
                _fps = (int)_fps;
            }
        }


        protected virtual int GetSystemFramerate()
        {
            if (Environment.TickCount - _sysLastSysTick >= 1000)
            {
                _sysLastFrameRate = _sysFrameRate;
                _sysFrameRate     = 0;
                _sysLastSysTick   = Environment.TickCount;
            }

            _sysFrameRate++;
            return _sysLastFrameRate;
        }
    }
}