﻿using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon.Common;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW.Pickups {
    // Recreated U# form YamaButa's PickUpToggle, PickupSetScale_VR, PickupSetScale_PC Udon Graphs.
    // Source: https://yamabuta.booth.pm/items/4189997
    /// <summary>
    /// A pickupable screen component.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(VRC_Pickup))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Pickup Panel")]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#how-to-add-a-pickupable-screen")]
    public class PickupPanel : UdonSharpEventSender {
        [LocalizedHeader("HEADER:PickupPanel.References")]
        [SerializeField, LocalizedLabel] Transform scalingTarget;
        [BindEvent(nameof(Button.onClick), nameof(_LockButtonToggle))]
        [SerializeField, LocalizedLabel] Button lockButton;
        [BindEvent(nameof(Button.onClick), nameof(_MakeUpright))]
        [SerializeField, LocalizedLabel] Button uprightButton;
        [SerializeField, HideInInspector, BindUdonSharpEvent] LanguageManager languageManager;
        VRC_Pickup pickup;
        [LocalizedHeader("HEADER:PickupPanel.Settings")]
        [SerializeField, LocalizedLabel] float scaleSpeed = 1F;
        [SerializeField, LocalizedLabel] float minScale = 0.2F, maxScale = 5F;
        [SerializeField, LocalizedLabel] KeyCode smallerKey = KeyCode.Q, biggerKey = KeyCode.E;
        [SerializeField, LocalizedLabel] float vrAnalogThreshold = 0.5F;
        [FieldChangeCallback(nameof(Locked))] bool locked;
        [LocalizedHeader("HEADER:PickupPanel.Display")]
        [SerializeField, LocalizedLabel] Renderer instructionRenderer;
        [SerializeField, LocalizedLabel] Material vrInstructionMaterial, pcInstructionMaterial;
        [SerializeField, LocalizedLabel] GameObject lockOnIcon, lockOffIcon;
        bool isVR;
        [FieldChangeCallback(nameof(Scale))] float scale = 1F;

        public bool Locked {
            get => locked;
            set {
                locked = value;
                UpdateLockedState();
            }
        }

        public float Scale {
            get => scale;
            set {
                if (Mathf.Approximately(scale, value)) return;
                scale = Mathf.Clamp(value, minScale, maxScale);
                scalingTarget.localScale = Vector3.one * scale;
            }
        }

        void Start() {
            pickup = (VRC_Pickup)GetComponent(typeof(VRC_Pickup));
            var localPlayer = Networking.LocalPlayer;
            isVR = Utilities.IsValid(localPlayer) && localPlayer.IsUserInVR();
            instructionRenderer.sharedMaterial = isVR ? vrInstructionMaterial : pcInstructionMaterial;
            instructionRenderer.enabled = false;
            UpdateLockedState();
            if (Utilities.IsValid(languageManager)) _OnLanguageChanged();
        }

        void Update() {
            if (isVR || !pickup.IsHeld) return;
            if (Input.GetKey(smallerKey))
                Scale = scale - scaleSpeed * Time.deltaTime;
            else if (Input.GetKey(biggerKey))
                Scale = scale + scaleSpeed * Time.deltaTime;
        }

        public override void OnPickup() {
            instructionRenderer.enabled = true;
            SendEvent("_OnScreenPickup");
        }

        public override void OnDrop() {
            instructionRenderer.enabled = false;
            SendEvent("_OnScreenDrop");
        }

        public override void InputLookVertical(float value, UdonInputEventArgs args) {
            if (!isVR || !pickup.IsHeld) return;
            Scale = scale + Mathf.Sign(value) * (Mathf.Abs(value) - vrAnalogThreshold) * scaleSpeed * Time.deltaTime;
        }

        void UpdateLockedState() {
            if (locked && pickup.IsHeld) pickup.Drop();
            pickup.pickupable = !locked;
            lockOnIcon.SetActive(locked);
            lockOffIcon.SetActive(!locked);
        }

        public void _LockButtonToggle() => Locked = !locked;

        public void _Reset() {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            Locked = false;
            Scale = 1F;
            SendEvent("_OnReset");
        }

        public void _OnLanguageChanged() {
            if (Utilities.IsValid(pickup)) pickup.InteractionText = languageManager.GetLocale("PickupScreen");
        }

        public void _MakeUpright() {
            var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            // Quad's Forward vector is flipped
            var toHead = (transform.position - head).normalized;
            // Make it upright
            toHead.y = 0;
            transform.rotation = Quaternion.LookRotation(toHead, Vector3.up);
            SendEvent("_OnUpright");
        }
    }
}