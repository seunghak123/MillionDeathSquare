using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.UI;
[CustomEditor(typeof(SkewedImage))]
public class SkewedImageEditor : ImageEditor
{
    public override void OnInspectorGUI()
    {
        SkewedImage skewedImage = (SkewedImage)target;

        // 기본 Image 인스펙터 그리기
        DrawDefaultInspector();

        // 변경 사항이 있을 경우 적용
        if (GUI.changed)
        {
            EditorUtility.SetDirty(skewedImage);
        }
    }
}
