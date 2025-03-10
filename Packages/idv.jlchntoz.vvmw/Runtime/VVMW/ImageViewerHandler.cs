﻿using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Image;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Components.Video;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// A backend for loading and displaying images.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Image Viewer Handler")]
    [DefaultExecutionOrder(0)]
    public class ImageViewerHandler : AbstractMediaPlayerHandler {
        VRCImageDownloader loader;
        bool loop, isPlaying;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override bool IsActive {
            get => isActive;
            set {
                isActive = value;
                texture = null;
                currentUrl = null;
                isReady = false;
                isPlaying = false;
            }
        }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override bool Loop {
            get => loop;
            set => loop = value;
        }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override bool IsPlaying => isPlaying;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override bool IsStatic => true;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override void LoadUrl(VRCUrl url, bool reload) {
            if (!Utilities.IsValid(loader)) loader = new VRCImageDownloader();
            if (url.Equals(currentUrl) && !reload && texture) {
                isReady = true;
                if (isActive) core.OnVideoReady();
                return;
            }
            loader.DownloadImage(url, null, (IUdonEventReceiver)this);
            currentUrl = url;
            texture = null;
            isReady = false;
            isPlaying = false;
        }

        public override void OnImageLoadSuccess(IVRCImageDownload image) {
            if (!image.Url.Equals(currentUrl)) return;
            texture = image.Result;
            isReady = true;
            isPlaying = false;
            if (isActive) core.OnVideoReady();
        }

        public override void OnImageLoadError(IVRCImageDownload image) {
            texture = null;
            currentUrl = null;
            isReady = false;
            isPlaying = false;
            if (isActive) {
                var error = VideoError.Unknown;
                switch (image.Error) {
                    case VRCImageDownloadError.DownloadError:
                    case VRCImageDownloadError.InvalidImage:
                        error = VideoError.PlayerError;
                        break;
                    case VRCImageDownloadError.InvalidURL:
                        error = VideoError.InvalidURL;
                        break;
                    case VRCImageDownloadError.AccessDenied:
                        error = VideoError.AccessDenied;
                        break;
                    case VRCImageDownloadError.TooManyRequests:
                        error = VideoError.RateLimited;
                        break;
                }
                core.OnVideoError(error);
            }
        }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override void Play() {
            isPlaying = true;
            if (isActive) {
                core.OnVideoPlay();
                if (texture) core._OnTextureChanged();
            }
        }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override void Pause() => Play();

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override void Stop() {
            texture = null;
            currentUrl = null;
            isReady = false;
            isPlaying = false;
            if (isActive) core.OnVideoEnd();
        }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        override int IsSupported(string urlStr) {
            int index = urlStr.IndexOf('#');
            if (index < 0) {
                index = urlStr.IndexOf('?');
                if (index < 0) index = urlStr.Length;
            }
            int startIndex = urlStr.LastIndexOf('.', index - 1) + 1;
            if (startIndex < 0 || startIndex >= index) return -1;
            switch (urlStr.Substring(startIndex, index - startIndex).ToLower()) {
                case "png":
                case "jpg":
                case "jpeg":
                    return 1;
            }
            return -1;
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        protected override void PreProcess() {
            if (Utilities.IsValid(applyTurstedUrl)) applyTurstedUrl(TrustedUrlTypes.ImageUrl, ref trustedUrlDomains);
        }
#endif
    }
}