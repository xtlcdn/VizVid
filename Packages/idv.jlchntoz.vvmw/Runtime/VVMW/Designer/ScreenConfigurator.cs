using System;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [ExecuteInEditMode]
    [EditorOnly]
    [AddComponentMenu("VizVid/Components/Screen Configurator")]
    public class ScreenConfigurator : MonoBehaviour, IVizVidCompoonent {
        [SerializeField, Locatable, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")] internal Core core;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.videoScreenTarget")] internal Renderer screenRenderer;
        [SerializeField] int targetMode = 1;
        [SerializeField] int targetIndex = -1;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.screenTargetPropertyNames")] string targetPropertyName = "_MainTex";
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.avProPropertyNames")] string avProPropertyName = "_IsAVProVideo";
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.screenTargetDefaultTextures")] Texture defaultTexture;
        Renderer previousRenderer;
        Core previousCore;

        Core IVizVidCompoonent.Core => core;

        public Renderer Renderer {
            get {
                var result = screenRenderer;
                if (result) return result;
                result = GetComponentInChildren<Renderer>();
                if (result) return result;
                result = GetComponentInChildren<Renderer>(true);
                return result;
            }
        }

        internal bool IsPrefabEditingMode {
            get {
#if UNITY_EDITOR
                return PrefabStageUtility.GetPrefabStage(gameObject) != null;
#else
                return false;
#endif
            }
        }

        static void RemoveIndexFromArray<T>(ref T[] array, int index) {
            if (index < 0 || index >= array.Length) return;
            var newArray = new T[array.Length - 1];
            Array.Copy(array, 0, newArray, 0, index);
            if (index < array.Length - 1) Array.Copy(array, index + 1, newArray, index, array.Length - index - 1);
            array = newArray;
        }

        static void RemoveFromCore(Core core, Renderer renderer) {
            if (!core) return;
            int index = Array.IndexOf(core.screenTargets, renderer);
            if (index < 0) return;
#if UNITY_EDITOR
            Undo.RecordObject(core, "Screen Configurator");
#endif
            RemoveIndexFromArray(ref core.screenTargets, index);
            RemoveIndexFromArray(ref core.screenTargetModes, index);
            RemoveIndexFromArray(ref core.screenTargetIndeces, index);
            RemoveIndexFromArray(ref core.screenTargetPropertyNames, index);
            RemoveIndexFromArray(ref core.avProPropertyNames, index);
            RemoveIndexFromArray(ref core.screenTargetDefaultTextures, index);
        }

        void Awake() {
            if (Application.isPlaying || IsPrefabEditingMode) return;
            previousRenderer = screenRenderer;
            if (core) {
                previousCore = core;
                return;
            }
            var renderer = Renderer;
            if (!renderer) return;
            var cores = FindObjectsOfType<Core>(true);
            foreach (var c in cores) {
                if (c.screenTargets == null || Array.IndexOf(c.screenTargets, renderer) < 0) continue;
#if UNITY_EDITOR
                Undo.RecordObject(this, "Screen Configurator");
#endif
                core = c;
            }
            if (!core) {
                core = GetComponentInParent<Core>(true);
                if (!core) {
                    if (cores.Length > 0) core = cores[0];
                    else return;
                }
                if (!AddToCoreIfEmpty(renderer)) AppendToCore(renderer);
            }
            previousCore = core;
        }

        void OnValidate() {
            if (Application.isPlaying || IsPrefabEditingMode) return;
#if UNITY_EDITOR
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;
#endif
            int index;
            if (previousCore != core) {
                RemoveFromCore(previousCore, previousRenderer);
                previousCore = core;
            }
            if (!core) return;
            var renderer = Renderer;
            if (!renderer) return;
            try {
                if (renderer != screenRenderer) {
    #if UNITY_EDITOR
                    Undo.RecordObject(this, "Screen Configurator");
    #endif
                    screenRenderer = renderer;
                }
                if (AddToCoreIfEmpty(renderer)) {
                    previousRenderer = renderer;
                    return;
                }
                index = Array.IndexOf(core.screenTargets, previousRenderer);
                if (index < 0) {
                    AppendToCore(renderer);
                    return;
                }
                if (
                    targetMode != core.screenTargetModes[index] ||
                    targetIndex != core.screenTargetIndeces[index] ||
                    targetPropertyName != core.screenTargetPropertyNames[index] ||
                    avProPropertyName != core.avProPropertyNames[index] ||
                    defaultTexture != core.screenTargetDefaultTextures[index]
                ) {
#if UNITY_EDITOR
                    Undo.RecordObject(this, "Screen Configurator");
#endif
                    targetMode = core.screenTargetModes[index];
                    targetIndex = core.screenTargetIndeces[index];
                    targetPropertyName = core.screenTargetPropertyNames[index];
                    avProPropertyName = core.avProPropertyNames[index];
                    defaultTexture = core.screenTargetDefaultTextures[index];
                }
                if (previousRenderer != renderer) {
#if UNITY_EDITOR
                    Undo.RecordObject(core, "Screen Configurator");
#endif
                    core.screenTargets[index] = renderer;
                }
            } finally {
                previousRenderer = renderer;
#if UNITY_EDITOR
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
#endif
            }
        }

        bool AddToCoreIfEmpty(Renderer renderer) {
            if (core.screenTargets != null && core.screenTargets.Length > 0)
                return false;
#if UNITY_EDITOR
            Undo.RecordObject(core, "Screen Configurator");
#endif
            core.screenTargets = new[] { renderer };
            core.screenTargetModes = new[] { targetMode };
            core.screenTargetIndeces = new[] { targetIndex };
            core.screenTargetPropertyNames = new[] { targetPropertyName };
            core.avProPropertyNames = new[] { avProPropertyName };
            core.screenTargetDefaultTextures = new[] { defaultTexture };
            return true;
        }

        void AppendToCore(Renderer renderer) {
            int index = core.screenTargets.Length;
            int size = index + 1;
#if UNITY_EDITOR
            Undo.RecordObject(core, "Screen Configurator");
#endif
            Array.Resize(ref core.screenTargets, size);
            core.screenTargets[index] = renderer;
            Array.Resize(ref core.screenTargetModes, size);
            core.screenTargetModes[index] = targetMode;
            Array.Resize(ref core.screenTargetIndeces, size);
            core.screenTargetIndeces[index] = targetIndex;
            Array.Resize(ref core.screenTargetPropertyNames, size);
            core.screenTargetPropertyNames[index] = targetPropertyName;
            Array.Resize(ref core.avProPropertyNames, size);
            core.avProPropertyNames[index] = avProPropertyName;
            Array.Resize(ref core.screenTargetDefaultTextures, size);
            core.screenTargetDefaultTextures[index] = defaultTexture;
        }
    }
}