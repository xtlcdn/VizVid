using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel, Range(0, 5)] float timeDriftDetectThreshold = 0.9F;
        [UdonSynced] long ownerServerTime;
        [UdonSynced] long time;
        [UdonSynced] float syncedSpeed = 1, syncedActualSpeed = 1;
        [FieldChangeCallback(nameof(PerformerId))]
        [UdonSynced] ushort performerId;
        [FieldChangeCallback(nameof(SyncOffset))]
        float syncOffset = 0;
        [FieldChangeCallback(nameof(Speed))]
        float speed = 1, actualSpeed = 1;
        float syncLatency;
        float lastSyncRawTime;
        bool isResyncTime;
        DateTime lastSyncTime;
        VRCPlayerApi performer;

        /// <summary>
        /// The current time of the video in seconds.
        /// </summary>
        /// <remarks>
        /// If it is a live stream, this value will be zero.
        /// </remarks>
        public float Time {
            get => Utilities.IsValid(activeHandler) ? activeHandler.Time : 0;
            private set {
                activeHandler.Time = value;
                lastSyncRawTime = value;
            }
        }

        /// <summary>
        /// The duration of the video in seconds.
        /// </summary>
        /// <remarks>
        /// If it is a live stream, this value will be infinity.
        /// </remarks>
        public float Duration => Utilities.IsValid(activeHandler) ? activeHandler.Duration : 0;

        /// <summary>
        /// The offset of the video time to other players, in seconds.
        /// </summary>
        public float SyncOffset {
            get => syncOffset;
            set {
                if (syncOffset == value) return;
                syncOffset = value;
                SendEvent("_OnSyncOffsetChange");
                if (synced && Utilities.IsValid(activeHandler) && activeHandler.IsPlaying) {
                    var duration = activeHandler.Duration;
                    if (duration <= 0 || float.IsInfinity(duration)) return;
                    Time = CalcVideoTime();
                    SendEvent("_OnTimeDrift");
                }
            }
        }

        /// <summary>
        /// The playback progress of the video, from 0 to 1.
        /// </summary>
        /// <remarks>
        /// If it is a live stream, this value will be zero, and setting this value will have no effect.
        /// </remarks>
        public float Progress {
            get {
                if (!Utilities.IsValid(activeHandler)) return 0;
                var duration = activeHandler.Duration;
                if (activeHandler.Duration <= 0 || float.IsInfinity(duration)) return 0;
                return activeHandler.Time / activeHandler.Duration;
            }
            set {
                if (!Utilities.IsValid(activeHandler)) return;
                var duration = activeHandler.Duration;
                if (activeHandler.Duration <= 0 || float.IsInfinity(duration)) return;
                Time = duration * value;
                RequestSync();
            }
        }

        /// <summary>
        /// Is current video player backend support speed adjustment.
        /// </summary>
        public bool SupportSpeedAdjustment => Utilities.IsValid(activeHandler) && activeHandler.SupportSpeedAdjustment;

        /// <summary>
        /// The playback speed of the video.
        /// </summary>
        /// <remarks>
        /// The value will be clamped between 0.1 and 2.
        /// </remarks>
        public float Speed {
            get => speed;
            set {
                if (speed == value) return;
                speed = Mathf.Clamp(value, 0.1F, 2);
                SyncSpeed();
                RequestSync();
            }
        }

        /// <summary>
        /// Gets the performer of the video player playback.
        /// </summary>
        public VRCPlayerApi Performer => performer;

        ushort PerformerId {
            set {
                performerId = value;
                if (performerId == 0)
                    performer = null;
                else
                    performer = VRCPlayerApi.GetPlayerById(performerId);
                SendEvent("_OnPerformerChange");
            }
        }

        float PerformerLatency => Utilities.IsValid(performer) && !performer.isLocal ? UnityEngine.Time.realtimeSinceStartup - Networking.SimulationTime(performer) : 0;

        /// <summary>
        /// Event entry point on the ownership of the video player is transferred.
        /// Internal use only. Do not call this method.
        /// </summary>
        public override void OnOwnershipTransferred(VRCPlayerApi player) {
            if (!player.isLocal) isLocalReloading = false;
            syncLatency = 0;
        }

        void StartSyncTime() {
            if (!synced) return;
            SyncTime(true);
            if (!isResyncTime) {
                isResyncTime = true;
                SendCustomEventDelayedSeconds(nameof(_AutoSyncTime), 0.5F);
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _AutoSyncTime() {
            if (!gameObject.activeInHierarchy || !enabled || isLoading || isLocalReloading || !Utilities.IsValid(activeHandler) || !activeHandler.IsReady) {
                isResyncTime = false;
                return;
            }
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                isResyncTime = false;
                RequestSerialization();
                return;
            }
            SyncTime(false);
            SendCustomEventDelayedSeconds(nameof(_AutoSyncTime), 0.5F);
        }

        long CalcSyncTime(out float actualSpeed) {
            if (!Utilities.IsValid(activeHandler)) {
                actualSpeed = 1;
                return 0;
            }
            actualSpeed = activeHandler.Speed;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            var videoTime = Mathf.Repeat(activeHandler.Time, duration);
            var syncTime = (long)((videoTime / actualSpeed - syncOffset + PerformerLatency) * TimeSpan.TicksPerSecond);
            if (activeHandler.IsPlaying) syncTime = Networking.GetNetworkDateTime().Ticks - syncTime;
            if (synced) syncedActualSpeed = actualSpeed;
            return syncTime;
        }

        float CalcVideoTime() {
            if (!Utilities.IsValid(activeHandler)) return 0;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            float videoTime;
            int intState = state;
            switch (intState) {
                case PLAYING: videoTime = ((float)(Networking.GetNetworkDateTime().Ticks - time) / TimeSpan.TicksPerSecond + syncOffset + syncLatency - PerformerLatency) * actualSpeed; break;
                case PAUSED: videoTime = (float)time / TimeSpan.TicksPerSecond; break;
                default: return 0;
            }
            return loop ? Mathf.Repeat(videoTime, duration) : Mathf.Clamp(videoTime, 0, duration);
        }

        void SyncTime(bool forced) {
            if (Networking.IsOwner(gameObject)) {
                var newTime = CalcSyncTime(out float speed);
                if (forced || Mathf.Abs((float)(newTime - time) / TimeSpan.TicksPerSecond) >= timeDriftDetectThreshold) {
                    time = newTime;
                    actualSpeed = speed;
                    RequestSerialization();
                }
            } else {
                var duration = activeHandler.Duration;
                if (duration <= 0 || float.IsInfinity(duration)) return;
                float t = CalcVideoTime();
                var t2 = activeHandler.Time;
                if (!activeHandler.IsPlaying || t2 != lastSyncRawTime) {
                    lastSyncRawTime = t2;
                    if (!forced) {
                        if (loop) t2 = Mathf.Repeat(t2, duration);
                        forced = Mathf.Abs(t2 - t) >= timeDriftDetectThreshold;
                    }
                }
                if (forced) {
                    Time = t;
                    SendEvent("_OnTimeDrift");
                }
            }
        }

        /// <summary>
        /// Request the owner to synchronize the video player state.
        /// </summary>
        /// <remarks>
        /// If synchronization is disabled, this method will have no effect.
        /// </remarks>
        public void _RequestOwnerSync() {
            isOwnerSyncRequested = false;
            if (!synced) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OwnerSync));
        }

        /// <summary>
        /// Let current user be the performer of current video player playback. (Experimental)
        /// </summary>
        /// <param name="enable">Enable or disable the performance mode.</param>
        /// <remarks>
        /// Video playback time will be auto adjusted to cancel-out the latency between the performer and audience (other users).
        /// </remarks>
        public void SetOwnPerformer(bool enable) {
            if (!synced) return;
            var player = Networking.LocalPlayer;
            if (enable) {
                var newHostId = player.playerId;
                if (newHostId == performerId) return;
                PerformerId = (ushort)newHostId;
            } else if (performerId == player.playerId) {
                PerformerId = 0;
            } else return;
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(player, gameObject);
            RequestSerialization();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void OwnerSync() {
            if (!Networking.IsOwner(gameObject) || !synced) return;
            if ((Networking.GetNetworkDateTime() - lastSyncTime).Ticks < OWNER_SYNC_COOLDOWN_TICKS) return;
            RequestSerialization();
        }

        void SyncSpeed() {
            if (Utilities.IsValid(activeHandler) && activeHandler.SupportSpeedAdjustment)
                activeHandler.Speed = speed;
            SetAudioPitch();
            SendEvent("_OnSpeedChange");
        }
    }
}
