﻿using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// Automatically assigns unique stream links for each event, performer, or instance.
    /// </summary>
    /// <remarks>
    /// You may extend this class
    /// </remarks>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2)]
    [AddComponentMenu("VizVid/Stream Key Assigner")]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#how-to-automatically-assigns-unique-stream-links-for-each-event-performer-or-instance")]
    public partial class StreamLinkAssigner : VizVidBehaviour {
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        [Resolve(nameof(frontendHandler) + "." + nameof(FrontendHandler.core), HideInInspectorIfResolvable = true)]
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )]
        protected Core core;
        [SerializeField, LocalizedLabel(Key = "VVMW.Handler")]
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )]
        protected FrontendHandler frontendHandler;
        [SerializeField, LocalizedLabel] protected string streamKeyTemplate, streamUrlTemplate = "rtspt://example.com/live/{0}", altStreamUrlTemplate = "rtsp://example.com/live/{0}";
        [SerializeField, LocalizedLabel] protected bool currentUserOnly;
        [SerializeField, LocalizedLabel] protected VRCUrl[] streamLinks, altStreamLinks;
        [SerializeField, LocalizedLabel] protected string[] streamKeys;
        [SerializeField, LocalizedLabel] protected int playerIndex = 1;
        [SerializeField, LocalizedLabel] protected bool autoInterrupt = true;
        [SerializeField, LocalizedLabel] protected bool autoPlay = true;
        [SerializeField, LocalizedLabel] InputField inputFieldToCopy;
        [BindEvent(nameof(Button.onClick), nameof(_Regenerate))]
        [SerializeField, LocalizedLabel] Button regenerateButton;
        [BindEvent(nameof(Button.onClick), nameof(_Play))]
        [SerializeField, LocalizedLabel] Button playButton;
        [UdonSynced] int syncedStreamIndex = -1;
        protected int streamIndex = -1;

        protected virtual void Start() {
            if (currentUserOnly || Networking.IsOwner(gameObject)) _Regenerate();
        }

        public override void OnPreSerialization() {
            syncedStreamIndex = currentUserOnly ? -1 : streamIndex;
        }

        public override void OnDeserialization() {
            if (currentUserOnly || syncedStreamIndex < 0) return;
            streamIndex = syncedStreamIndex;
            UpdateText();
        }

        void UpdateText() {
            if (Utilities.IsValid(inputFieldToCopy)) inputFieldToCopy.text = streamKeys[streamIndex];
        }

        /// <summary>
        /// Reassign the stream key from the pool.
        /// </summary>
        public void _Regenerate() {
            RegenerateCore();
            UpdateText();
            if (!currentUserOnly) {
                if (!Networking.IsOwner(gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                RequestSerialization();
            }
            if (autoPlay) _Play();
        }

        /// <summary>
        /// The actual logic to assign the pool index of the stream key.
        /// </summary>
        /// <remarks>
        /// You may override this method to implement your own logic.
        /// </remarks>
        protected virtual void RegenerateCore() {
            if (!Utilities.IsValid(streamLinks) || streamLinks.Length == 0) {
                Debug.LogError("[Stream Key Assigner] No stream links are generated. Please report to the world creator to fix this.");
                return;
            }
            streamIndex = Random.Range(0, streamLinks.Length);
        }

        /// <summary>
        /// Send the assigned stream key to the player.
        /// </summary>
        public void _Play() {
            if (streamIndex < 0) {
                Debug.LogError("[Stream Key Assigner] No stream key is assigned. Unable to play.");
                return;
            }
            if (Utilities.IsValid(frontendHandler)) {
                bool enableIntrrupt = autoInterrupt && frontendHandler.HasQueueList && frontendHandler.PlayListIndex == 0;
                int currentPendingCount = enableIntrrupt ? frontendHandler.PendingCount : -1;
                frontendHandler.PlayUrl(streamLinks[streamIndex], altStreamLinks[streamIndex], (byte)playerIndex);
                if (enableIntrrupt) {
                    int pendingCount = frontendHandler.PendingCount;
                    if (pendingCount > currentPendingCount)
                        frontendHandler.PlayAt(0, pendingCount - 1, false);
                }
                return;
            }
            if (Utilities.IsValid(core)) core.PlayUrl(streamLinks[streamIndex], altStreamLinks[streamIndex], (byte)playerIndex);
        }
    }

#if !COMPILER_UDONSHARP
    public partial class StreamLinkAssigner : IVizVidCompoonent {
        Core IVizVidCompoonent.Core => core;
    }
#endif
}