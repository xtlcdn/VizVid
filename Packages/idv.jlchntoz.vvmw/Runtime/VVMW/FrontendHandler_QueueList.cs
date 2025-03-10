using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        [SerializeField, LocalizedLabel] bool enableQueueList = true;
        [UdonSynced] VRCUrl[] queuedUrls, queuedQuestUrls;
        [UdonSynced] string queuedTitles;
        [UdonSynced] byte[] queuedPlayerIndex;
        [UdonSynced] string currentTitle;
        VRCUrl[] localQueuedUrls, localQueuedQuestUrls;
        byte[] localQueuedPlayerIndex;
        string[] localQueuedTitles;
        string localCurrentTitle;

        /// <summary>
        /// The URLs of the queued items.
        /// </summary>
        /// <remarks>The returned array is meant to be read only, do not modify.</remarks>
        public VRCUrl[] QueueUrls {
            get {
                if (!Utilities.IsValid(localQueuedUrls)) localQueuedUrls = new VRCUrl[0];
                return localQueuedUrls;
            }
        }

        /// <summary>
        /// The player backend index of the queued items.
        /// </summary>
        /// <remarks>
        /// This is 1-based index, 0 is invalid.
        /// </remarks>
        /// <remarks>The returned array is meant to be read only, do not modify.</remarks>
        public byte[] QueuePlayerIndex {
            get {
                if (!Utilities.IsValid(localQueuedPlayerIndex)) localQueuedPlayerIndex = new byte[0];
                return localQueuedPlayerIndex;
            }
        }

        /// <summary>
        /// The titles of the queued items.
        /// </summary>
        /// <remarks>The returned array is meant to be read only, do not modify.</remarks>
        public string[] QueueTitles {
            get {
                if (!Utilities.IsValid(localQueuedTitles)) localQueuedTitles = new string[0];
                return localQueuedTitles;
            }
        }

        /// <summary>
        /// Whether the queue list is enabled.
        /// </summary>
        public bool HasQueueList => enableQueueList;

        /// <inheritdoc cref="PlayUrl(VRCUrl, VRCUrl, string, byte)"/>
        public void PlayUrl(VRCUrl url, byte index) => PlayUrl(url, null, null, index);

        /// <inheritdoc cref="PlayUrl(VRCUrl, VRCUrl, string, byte)"/>
        public void PlayUrl(VRCUrl pcUrl, VRCUrl questUrl, byte index) => PlayUrl(pcUrl, questUrl, null, index);

        /// <summary>
        /// Play or enqueue a video URL.
        /// </summary>
        /// <param name="url">The URL of the video.</param>
        /// <param name="pcUrl">The URL of the video for PC.</param>
        /// <param name="questUrl">The URL of the video for Quest (mobile).</param>
        /// <param name="queuedTitle">The title of the video to be displayed in the queue list.</param>
        /// <param name="index">The player index (1-based) to play the video.</param>
        /// <remarks>
        /// If queue list is enabled and the player is playing or loading, the URL will be enqueued.
        /// Except if it is playing an entry of a playlist, in this case will always intrrupts the current playback.
        /// </remarks>
        public void PlayUrl(VRCUrl pcUrl, VRCUrl questUrl, string queuedTitle, byte index) {
            if (VRCUrl.IsNullOrEmpty(pcUrl)) return;
            if (VRCUrl.IsNullOrEmpty(questUrl)) questUrl = pcUrl;
            if (localPlayListIndex > 0) {
                localPlayListIndex = 0;
                localQueuedUrls = null;
                localQueuedQuestUrls = null;
                localQueuedPlayerIndex = null;
                localQueuedTitles = null;
                core.Stop();
            }
            if (string.IsNullOrEmpty(queuedTitle))
                queuedTitle = $"{Networking.LocalPlayer.displayName}:\n{UnescapeUrl(pcUrl)}";
            if (enableQueueList && (core.IsReady || core.IsLoading || (Utilities.IsValid(localQueuedUrls) && localQueuedUrls.Length > 0))) {
                if (IsArrayNullOrEmpty(localQueuedUrls)) {
                    localQueuedUrls = new VRCUrl[] { pcUrl };
                } else {
                    var newQueue = new VRCUrl[localQueuedUrls.Length + 1];
                    Array.Copy(localQueuedUrls, newQueue, localQueuedUrls.Length);
                    newQueue[localQueuedUrls.Length] = pcUrl;
                    localQueuedUrls = newQueue;
                }
                bool isQuestQueueEmpty = IsArrayNullOrEmpty(localQueuedQuestUrls);
                if (!pcUrl.Equals(questUrl) || !isQuestQueueEmpty) {
                    if (isQuestQueueEmpty) {
                        localQueuedQuestUrls = new VRCUrl[] { questUrl };
                    } else {
                        var newAltQueue = new VRCUrl[localQueuedQuestUrls.Length + 1];
                        Array.Copy(localQueuedQuestUrls, newAltQueue, localQueuedQuestUrls.Length);
                        newAltQueue[localQueuedQuestUrls.Length] = questUrl;
                        localQueuedQuestUrls = newAltQueue;
                    }
                }
                if (IsArrayNullOrEmpty(localQueuedPlayerIndex)) {
                    localQueuedPlayerIndex = new byte[] { index };
                } else {
                    var newPlayerIndexQueue = new byte[localQueuedPlayerIndex.Length + 1];
                    Array.Copy(localQueuedPlayerIndex, newPlayerIndexQueue, localQueuedPlayerIndex.Length);
                    newPlayerIndexQueue[localQueuedPlayerIndex.Length] = index;
                    localQueuedPlayerIndex = newPlayerIndexQueue;
                }
                if (IsArrayNullOrEmpty(localQueuedTitles)) {
                    localQueuedTitles = new string[] { queuedTitle };
                } else {
                    var newTitles = new string[localQueuedTitles.Length + 1];
                    Array.Copy(localQueuedTitles, newTitles, localQueuedTitles.Length);
                    newTitles[localQueuedTitles.Length] = queuedTitle;
                    localQueuedTitles = newTitles;
                }
                RequestSync();
                UpdateState();
                return;
            }
            RecordPlaybackHistory(pcUrl, questUrl, index, queuedTitle);
            localCurrentTitle = queuedTitle;
            RequestSync();
            core.PlayUrl(pcUrl, questUrl, index);
            core._ResetTitle();
        }

        void PlayQueueList(int index, bool deleteOnly) {
            int newLength = Utilities.IsValid(localQueuedUrls) ? localQueuedUrls.Length : 0;
            if (newLength <= 0) {
                if (!deleteOnly && localPlayingPlaylistIndex == 0 && index < 0 && RepeatAll) {
                    GetLastPlayedUrl(out VRCUrl lastPCUrl, out VRCUrl lastQuestUrl, out byte lastActivePlayer);
                    core.PlayUrl(lastPCUrl, lastQuestUrl, lastActivePlayer);
                    core._ResetTitle();
                    RecordPlaybackHistory(lastPCUrl, lastQuestUrl, lastActivePlayer, localCurrentTitle);
                }
                return;
            }
            if (index >= newLength) return;
            if (index < 0) {
                if (deleteOnly) {
                    localPlayListIndex = 0;
                    localQueuedUrls = null;
                    localQueuedQuestUrls = null;
                    localQueuedPlayerIndex = null;
                    localQueuedTitles = null;
                    RequestSync();
                    UpdateState();
                    return;
                }
                index = Shuffle ? UnityEngine.Random.Range(0, newLength) : 0;
            }
            var shouldReEnqueue = !deleteOnly && localPlayingPlaylistIndex == 0 && RepeatAll;
            if (!shouldReEnqueue) newLength--;
            var url = localQueuedUrls[index];
            bool hasQuestUrl = !IsArrayNullOrEmpty(localQueuedQuestUrls);
            var questUrl = hasQuestUrl ? localQueuedQuestUrls[index] : url;
            var playerIndex = localQueuedPlayerIndex[index];
            var title = localQueuedTitles[index];
            var newQueue = newLength == localQueuedUrls.Length ? localQueuedUrls : new VRCUrl[newLength];
            var newQuestQueue = hasQuestUrl ? newLength == localQueuedQuestUrls.Length ? localQueuedQuestUrls : new VRCUrl[newLength] : null;
            var newPlayerIndexQueue = newLength == localQueuedUrls.Length ? localQueuedPlayerIndex : new byte[newLength];
            var newTitles = newLength == localQueuedUrls.Length ? localQueuedTitles : new string[newLength];
            if (index > 0) {
                if (localQueuedUrls != newQueue)
                    Array.Copy(localQueuedUrls, 0, newQueue, 0, index);
                if (hasQuestUrl && localQueuedQuestUrls != newQuestQueue)
                    Array.Copy(localQueuedQuestUrls, 0, newQuestQueue, 0, index);
                if (localQueuedPlayerIndex != newPlayerIndexQueue)
                    Array.Copy(localQueuedPlayerIndex, 0, newPlayerIndexQueue, 0, index);
                if (localQueuedTitles != newTitles)
                    Array.Copy(localQueuedTitles, 0, newTitles, 0, index);
            }
            int copyCount = Mathf.Min(localQueuedUrls.Length - 1, newLength) - index;
            Array.Copy(localQueuedUrls, index + 1, newQueue, index, copyCount);
            if (hasQuestUrl) Array.Copy(localQueuedQuestUrls, index + 1, newQuestQueue, index, copyCount);
            Array.Copy(localQueuedPlayerIndex, index + 1, newPlayerIndexQueue, index, copyCount);
            Array.Copy(localQueuedTitles, index + 1, newTitles, index, copyCount);
            if (shouldReEnqueue) {
                int lastIndex = newLength - 1;
                GetLastPlayedUrl(out VRCUrl lastPCUrl, out VRCUrl lastQuestUrl, out byte lastActivePlayer);
                newQueue[lastIndex] = lastPCUrl;
                if (hasQuestUrl) newQuestQueue[lastIndex] = lastQuestUrl;
                newPlayerIndexQueue[lastIndex] = lastActivePlayer;
                newTitles[lastIndex] = localCurrentTitle;
            }
            localQueuedUrls = newQueue;
            localQueuedQuestUrls = newQuestQueue;
            localQueuedPlayerIndex = newPlayerIndexQueue;
            localQueuedTitles = newTitles;
            if (!deleteOnly) {
                localCurrentTitle = title;
                core.PlayUrl(url, questUrl, playerIndex);
                core._ResetTitle();
                RecordPlaybackHistory(url, questUrl, playerIndex, title);
            }
            RequestSync();
            UpdateState();
        }

        void GetLastPlayedUrl(out VRCUrl pcUrl, out VRCUrl questUrl, out byte playerIndex) {
            if (core.IsReady) {
#if UNITY_ANDROID || UNITY_IOS
                pcUrl = core.AltUrl;
                questUrl = core.Url;
#else
                pcUrl = core.Url;
                questUrl = core.AltUrl;
#endif
                playerIndex = core.ActivePlayer;
            } else {
#if UNITY_ANDROID || UNITY_IOS
                pcUrl = core.LastAltUrl;
                questUrl = core.LastUrl;
#else
                pcUrl = core.LastUrl;
                questUrl = core.LastAltUrl;
#endif
                playerIndex = core.LastActivePlayer;
            }
        }
    }
}