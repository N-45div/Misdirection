using TMPro;
using UnityEngine;

namespace Misdirection
{
    /// <summary>World-space TextMesh Pro labels for in-headset test UIs.</summary>
    public static class WorldText
    {
        public static TextMeshPro Create(Transform parent, string name, Vector2 size, float maxFontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = maxFontSize;
            tmp.color = Color.white;
            tmp.richText = true;
            return tmp;
        }

        /// <summary>Text reads correctly when its forward axis points away from the viewer.</summary>
        public static void Face(Transform text, Vector3 viewer, bool upright = true)
        {
            Vector3 dir = text.position - viewer;
            if (upright)
            {
                dir.y = 0f;
            }
            if (dir.sqrMagnitude > 1e-6f)
            {
                text.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }
    }
}
