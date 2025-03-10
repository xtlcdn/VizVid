#if LTCGI_IMPORTED
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UdonSharpEditor;
using JLChnToZ.VRC.Foundation.Editors;
using pi.LTCGI;

using static JLChnToZ.VRC.Foundation.Editors.Utils;
using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Designer {
    public class LTCGIAutoSetup : ILTCGI_AutoSetup {
        const string CRT_PATH = "Packages/idv.jlchntoz.vvmw/Materials/VVMW/VideoCRT (For LTCGI).asset";
        const string SHADER_NAME = "JLChnToZ/VideoCRT";
        const string PROXY_SRC_PATH = "Packages/idv.jlchntoz.vvmw/Samples~/VizVidLTCGIAutoSetupProxy.cs";
        const string PROXY_DEST_PATH = "Assets/_pi_/_LTCGI-Adapters/Editor/";
        const string PROXY_DEST_NAME = PROXY_DEST_PATH + "LTCGI_VizVidAutoSetup.cs";

        readonly Dictionary<Core, string> cores = new Dictionary<Core, string>();
        bool coreDirty = true;

        [InitializeOnLoadMethod]
        static void OnLoad() {
            EditorApplication.delayCall += RegisterProxy;
            LTCGIConfigurator.OnPreProcess = Preprocess;
        }

        static void RegisterProxy() {
            if (File.Exists(PROXY_DEST_NAME)) return;
            var contents = File.ReadAllText(PROXY_SRC_PATH);
            contents = contents.Replace("%TYPE%", typeof(LTCGIAutoSetup).AssemblyQualifiedName);
            if (!Directory.Exists(PROXY_DEST_PATH)) Directory.CreateDirectory(PROXY_DEST_PATH);
            File.WriteAllText(PROXY_DEST_NAME, contents, new UTF8Encoding(true));
            AssetDatabase.Refresh();
        }

        static CustomRenderTexture GetOrSetSuitableCRT(LTCGI_Controller controller) {
            var rt = controller.VideoTexture as CustomRenderTexture;
            Material mat;
            Shader shader;
            if (rt != null && (mat = rt.material) != null && (shader = mat.shader) != null && shader.name == SHADER_NAME)
                return rt;
            rt = AssetDatabase.LoadAssetAtPath<CustomRenderTexture>(CRT_PATH);
            controller.VideoTexture = rt;
            return rt;
        }

        public LTCGIAutoSetup() {
            SceneManager.activeSceneChanged += OnSceneUpdate;
            EditorApplication.hierarchyChanged += OnTransformUpdate;
        }

        ~LTCGIAutoSetup() {
            SceneManager.activeSceneChanged -= OnSceneUpdate;
            EditorApplication.hierarchyChanged -= OnTransformUpdate;
        }

        public GameObject AutoSetupEditor(LTCGI_Controller controller) {
            bool first = false;
            GameObject go = null;
            if (coreDirty) {
                coreDirty = false;
                cores.Clear();
                foreach (var core in SceneManager.GetActiveScene().IterateAllComponents<Core>())
                    cores.Add(core, GetHierarchyPath(core));
            }
            foreach (var kv in cores) {
                var core = kv.Key;
                var path = kv.Value;
                if (!first) {
                    EditorGUILayout.LabelField("Detected VizVid instances:");
                    first = true;
                }
                if (GUILayout.Button($"Auto-Configure '{path}'")) {
                    foreach (var existing in SceneManager.GetActiveScene().IterateAllComponents<LTCGIConfigurator>(true)) {
                        if (existing.core == core)
                            go = existing.gameObject;
                        else
                            DestroyImmediate(existing.gameObject); // Only one configurator can exists.
                    }
                    if (go == null)
                        go = new GameObject("VizVid LTCGI Configurator", typeof(LTCGIConfigurator)) {
                            hideFlags = HideFlags.HideInHierarchy,
                        };
                    go.transform.SetParent(core.transform, false);
                    var configurator = go.GetComponent<LTCGIConfigurator>();
                    configurator.core = core;
                    configurator.controller = controller;
                    configurator.screens = new List<LTCGI_Screen>();
                    foreach (var screenTarget in core.screenTargets) {
                        if (screenTarget == null) continue;
                        var meshTarget = screenTarget as MeshRenderer;
                        if (meshTarget == null) continue;
                        if (!meshTarget.TryGetComponent(out MeshFilter meshFilter)) continue;
                        var mesh = meshFilter.sharedMesh;
                        if (mesh == null || mesh.vertexCount != 4) continue;
                        if (!meshTarget.TryGetComponent(out LTCGI_Screen ltcgiScreen)) {
                            ltcgiScreen = meshTarget.gameObject.AddComponent<LTCGI_Screen>();
                            ltcgiScreen.Specular = true;
                            ltcgiScreen.Diffuse = true;
                        }
                        if (meshTarget.GetComponentInParent<Rigidbody>(true))
                            ltcgiScreen.Dynamic = true;
                        ltcgiScreen.ColorMode = ColorMode.Texture;
                        ltcgiScreen.TextureIndex = 0;
                        configurator.screens.Add(ltcgiScreen);
                    }
                    GetOrSetSuitableCRT(controller);
                }
            }
            return go;
        }

        static string GetHierarchyPath(Core core) {
            var stack = new Stack<string>();
            for (var transform = core.transform; transform != null; transform = transform.parent) {
                stack.Push(transform.name);
            }
            return string.Join("/", stack);
        }

        void OnSceneUpdate(Scene oldScene, Scene newScene) => coreDirty = true;

        void OnTransformUpdate() => coreDirty = true;

        static void Preprocess(LTCGIConfigurator configurator) {
            try {
                var core = configurator.core;
                var controller = configurator.controller;
                if (core == null || controller == null) {
                    Debug.LogError("[VizVid LTCGI Configurator] Missing Core or Controller.");
                    return;
                }
                var rt = GetOrSetSuitableCRT(controller);
                var mat = rt.material;
                if (mat == null) {
                    Debug.LogError("[VizVid LTCGI Configurator] Missing Material.");
                    return;
                }
                var aspectRatioId = Shader.PropertyToID("_AspectRatio");
                float aspectRatios = 0;
                int count = 0;
                for (int i = 0; i < core.screenTargets.Length; i++) {
                    var target = core.screenTargets[i];
                    if (target == null) continue;
                    if (target is MeshRenderer renderer && renderer.TryGetComponent(out LTCGI_Screen screen)) {
                        int matIndex = core.screenTargetIndeces[i];
                        if (matIndex < 0) {
                            foreach (var sm in renderer.sharedMaterials)
                                if (sm != null) {
                                    if (sm.HasFloat(aspectRatioId)) {
                                        aspectRatios += sm.GetFloat(aspectRatioId);
                                        count++;
                                    } else {
                                        aspectRatios++;
                                        count++;
                                    }
                                }
                        } else {
                            var sm = renderer.sharedMaterials[matIndex];
                            if (sm != null) {
                                if (sm.HasFloat(aspectRatioId)) {
                                    aspectRatios += sm.GetFloat(aspectRatioId);
                                    count++;
                                } else {
                                    aspectRatios++;
                                    count++;
                                }
                            }
                        }
                        continue;
                    }
                }
                if (aspectRatios == 0 || count == 0) aspectRatios = 16F / 9F;
                else aspectRatios /= count;
                mat.SetFloat(aspectRatioId, aspectRatios);
                int index = Array.IndexOf(core.screenTargets, mat);
                if (index < 0) {
                    index = core.screenTargets.Length;
                    Array.Resize(ref core.screenTargets, index + 1);
                    Array.Resize(ref core.screenTargetModes, index + 1);
                    Array.Resize(ref core.screenTargetIndeces, index + 1);
                    Array.Resize(ref core.screenTargetPropertyNames, index + 1);
                    Array.Resize(ref core.avProPropertyNames, index + 1);
                    Array.Resize(ref core.screenTargetDefaultTextures, index + 1);
                    core.screenTargets[index] = mat;
                    core.screenTargetPropertyNames[index] = "_MainTex";
                    core.avProPropertyNames[index] = "_IsAVProVideo";
                    UdonSharpEditorUtility.CopyProxyToUdon(core);
                }
                controller.UpdateMaterials();
            } finally {
                DestroyImmediate(configurator.gameObject);
            }
        }
    }
}
#endif