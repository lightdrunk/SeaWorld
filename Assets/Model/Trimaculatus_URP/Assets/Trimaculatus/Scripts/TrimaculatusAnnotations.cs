using System;
using System.Linq;
using UnityEngine;

namespace Trimaculatus.Education
{
    [DisallowMultipleComponent]
    public sealed class TrimaculatusAnnotations : MonoBehaviour
    {
        [Serializable]
        public struct Anchor
        {
            public string id;
            public string label;
            public Transform target;
        }

        public Anchor[] anchors = Array.Empty<Anchor>();
        public bool showLabels;
        public bool includeIndividualRings;
        public Camera labelCamera;
        [Tooltip("Assign a font with Chinese glyphs if the project's default font lacks them.")]
        public Font labelFont;
        [Tooltip("Keep the overlay sparse by entering a single anatomy ID, or leave blank to show key structures.")]
        public string focusId = "";

        [ContextMenu("Rebuild annotation anchors")]
        public void Rebuild()
        {
            anchors = GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.StartsWith("Label_", StringComparison.Ordinal))
                .Select(t => new Anchor { id = t.name.Substring(6), label = ChineseLabel(t.name.Substring(6)), target = t })
                .OrderBy(a => a.id).ToArray();
        }

        public Transform FindAnchor(string anatomyId)
        {
            foreach (var anchor in anchors) if (anchor.id == anatomyId) return anchor.target;
            return null;
        }

        void OnGUI()
        {
            if (!showLabels) return;
            var cam = labelCamera != null ? labelCamera : Camera.main;
            if (cam == null) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            if (labelFont != null) style.font = labelFont;
            foreach (var anchor in anchors)
            {
                if (anchor.target == null) continue;
                if (!string.IsNullOrEmpty(focusId) && anchor.id != focusId) continue;
                bool ring = anchor.id.StartsWith("TrunkRing_") || anchor.id.StartsWith("TailRing_");
                if (ring && !includeIndividualRings && string.IsNullOrEmpty(focusId)) continue;
                Vector3 p = cam.WorldToScreenPoint(anchor.target.position);
                if (p.z <= 0) continue;
                Vector2 size = style.CalcSize(new GUIContent(anchor.label));
                GUI.Box(new Rect(p.x + 8, Screen.height - p.y - size.y / 2, size.x + 12, size.y + 4), anchor.label, style);
            }
        }

        public static string ChineseLabel(string id)
        {
            if (id.StartsWith("TrunkRing_")) return "躯干环 " + id.Substring(10);
            if (id.StartsWith("TailRing_")) return "尾环 " + id.Substring(9);
            switch (id)
            {
                case "HeadTrunk_RightAngle": return "头—躯干直角连接";
                case "TubularSnout": return "管状长吻";
                case "LowCoronet_5TinyPoints": return "五突低骨冠";
                case "CheekHook_L": return "颊部钩状棘";
                case "SupraorbitalHook_L": return "眼上钩状棘";
                case "DorsalFin_20Soft": return "单一背鳍 · 20软条 · 无硬棘";
                case "AnalFin_4Soft": return "极小臀鳍 · 4软条";
                case "PectoralFin_L_18Soft": return "左胸鳍 · 18软条";
                case "PectoralFin_R_18Soft": return "右胸鳍 · 18软条";
                case "MaleBroodPouch": return "雄性育儿囊";
                case "QuadrangularPrehensileTail": return "四棱形可卷曲尾";
                case "TailTip_NoCaudalFin": return "尾端 · 无尾鳍";
                case "NoPelvicFins": return "无腹鳍";
                case "GillOpening_L": return "小鳃孔";
                case "OpercularRidge_L": return "鳃盖骨隆棱";
                case "Eye_L": return "左眼 · 可独立转动";
                case "Eye_R": return "右眼 · 可独立转动";
                case "Anus": return "肛门";
                default: return id;
            }
        }
    }
}
