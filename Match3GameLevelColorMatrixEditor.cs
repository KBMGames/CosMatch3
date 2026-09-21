
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Match3Game))]
public class Match3GameLevelColorMatrixEditor : Editor
{

    static readonly string[] kColorNames = { "Red", "Green", "Blue", "Yellow", "Purple" };

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI(); 
        DrawLevelColorMatrix();
    }

    void DrawLevelColorMatrix()
    {
        var m = target as Match3Game;
        if (m == null || m.levelColorMasks == null) return; 
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Per-level tile colors - click a cell to allow/block that color on the board");

        float lblW = 40f, cellW = 86f;
        var centerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

        
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(lblW);
        for (int c = 0; c < kColorNames.Length; c++)
            GUILayout.Label(kColorNames[c], centerStyle, GUILayout.Width(cellW));
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        for (int lvl = 0; lvl < m.levelColorMasks.Length; lvl++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("L" + (lvl + 1), GUILayout.Width(lblW));
            for (int c = 0; c < kColorNames.Length; c++)
            {
                bool on = (m.levelColorMasks[lvl] & (1 << c)) != 0;
                bool nv = GUILayout.Toggle(on, kColorNames[c], GUI.skin.toggle, GUILayout.Width(cellW));
                if (nv != on) m.levelColorMasks[lvl] = nv ? (m.levelColorMasks[lvl] | (1 << c)) : (m.levelColorMasks[lvl] & ~(1 << c));
            }
            EditorGUILayout.EndHorizontal();
        }
        if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(m);

        EditorGUILayout.HelpBox("A level row with every cell OFF falls back to ALL colors (the board must always have at least one). Levels past the end of the array use the last row's settings.", MessageType.None);
    }
}
