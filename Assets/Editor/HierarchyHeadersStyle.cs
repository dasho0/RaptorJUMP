using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class HierarchyHeaderStyle
{
    static HierarchyHeaderStyle()
    {
        EditorApplication.hierarchyWindowItemOnGUI += DrawCustomHeaders;
    }

    static void DrawCustomHeaders(int instanceID, Rect selectionRect)
    {
        GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
        if (obj == null) return;

        string rawName = obj.name;

        // Match name patterns like --- ENEMIES ---, == UI ==, ▓▓ CAMERAS ▓▓
        if (rawName.StartsWith("---") || rawName.StartsWith("==") || rawName.StartsWith("▓▓") || rawName.StartsWith("//") || rawName.StartsWith("##"))
        {
            // Draw background
            EditorGUI.DrawRect(selectionRect, new Color(0.1f, 0.1f, 0.1f)); // Dark gray/black

            // Strip special characters for display only
            string cleanName = rawName.Trim('-', '=', '/', '\\', '▓', '#', ' ');

            // Draw label (bold, white)
            EditorGUI.LabelField(selectionRect, cleanName, new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 0, 0, 0)
            });
        }
    }
}
