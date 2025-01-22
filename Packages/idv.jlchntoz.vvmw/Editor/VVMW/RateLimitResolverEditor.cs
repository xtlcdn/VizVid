using UnityEditor;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(RateLimitResolver))]
    public class RateLimitResolverEditor : VVMWEditorBase {
        public override void OnInspectorGUI() {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            DrawInspectorGUI();
        }

        public override void DrawInspectorGUI() {
            EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.RateLimitResolver.message"), MessageType.Info);
        }
    }
}