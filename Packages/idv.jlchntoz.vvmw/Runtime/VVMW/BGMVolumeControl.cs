﻿using UdonSharp;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// This component fades out the background music when a video is playing.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Components/BGM Volume Control")]
    [RequireComponent(typeof(AudioSource))]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#how-to-make-background-music-fade-out-when-video-is-playing")]
    public class BGMVolumeControl : VizVidBehaviour {
        AudioSource audioSource;
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        [SerializeField, Locatable, BindUdonSharpEvent] Core core;
        [SerializeField, HideInInspector, Resolve(nameof(core), NullOnly = false)] GameObject coreGO;
        /// <summary>
        /// The target volume when the video is not playing.
        /// </summary>
        [Range(0, 1), LocalizedLabel] public float volume = 1;
        /// <summary>
        /// Is the background music muted.
        /// </summary>
        [LocalizedLabel] public bool isMuted;
        [SerializeField, LocalizedLabel, Range(0, 10)] float fadeTime = 1;
        bool isVideoPlaying;

        void Start() {
            audioSource = GetComponent<AudioSource>();
        }

        void OnEnable() {
            isVideoPlaying = core.enabled && coreGO.activeSelf && core.IsPlaying && !core.IsStatic;
        }

        void Update() {
            float targetVolune = isMuted || isVideoPlaying ? 0 : volume;
            audioSource.volume = fadeTime > 0 ? Mathf.MoveTowards(audioSource.volume, targetVolune, Time.deltaTime / fadeTime) : targetVolune;
            if (isVideoPlaying && (!core.enabled || !coreGO.activeSelf)) isVideoPlaying = false;
        }

        /// <summary>
        /// Mute the background music.
        /// </summary>
        public void Mute() => isMuted = true;

        /// <summary>
        /// Unmute the background music.
        /// </summary>
        public void Unmute() => isMuted = false;

        public override void OnVideoStart() => isVideoPlaying = !core.IsStatic;

        public override void OnVideoPlay() => isVideoPlaying = !core.IsStatic;

        public override void OnVideoPause() => isVideoPlaying = false;

        public override void OnVideoEnd() => isVideoPlaying = false;
    }
}