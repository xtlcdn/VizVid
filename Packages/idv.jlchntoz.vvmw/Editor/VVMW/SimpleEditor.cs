using UnityEditor;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(VizVidBehaviour), true)]
    public class VizVidBehaviourEditor : VVMWEditorBase {
        public override void DrawInspectorGUI() {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            serializedObject.Update();
            DrawEmbeddedInspectorGUI();
            serializedObject.ApplyModifiedProperties();
        }
    }
}