using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class CaptureEditor : EditorWindow
{
    [SerializeField] private Camera captureCam;
    private static CaptureEditor captureTool;
    private string pathString = string.Empty;
    private string savefolderPath = string.Empty;
    private int captureSizeX, captureSizeY = 0;
    [MenuItem("Tools/CaptureCam", false, 2000)]
    private static void Open()
    {
        if (!Application.isPlaying)
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name != "CaptureTool")
            {
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Editor/CaptureTool.unity");
            }
        }

        if (captureTool == null)
        {
            captureTool = CreateInstance<CaptureEditor>();
        }

        captureTool.ShowUtility();
    }
    public void OnGUI()
    {
        EditorGUILayout.Space();
        pathString = EditorGUILayout.TextField("SaveFileName", pathString);
        EditorGUILayout.Space();
        captureSizeX = EditorGUILayout.IntField("SizeX", captureSizeX);
        captureSizeY = EditorGUILayout.IntField("SizeY", captureSizeY);
        if (GUILayout.Button("SelectSavePath"))
        {
            savefolderPath = EditorUtility.OpenFolderPanel("Select Folder", "", "");
        }
        if (captureCam == null)
        {
            captureCam = Camera.main;
        }
        if (GUILayout.Button("TakePicture"))
        {
            string savePath = savefolderPath + '/' + pathString + ".png";
            // 카메라 렌더 텍스처를 생성
            RenderTexture rt = new RenderTexture(captureSizeX, captureSizeY, 24);
            captureCam.targetTexture = rt;
            captureCam.Render();

            Texture2D screenshot = new Texture2D(captureSizeX, captureSizeY, TextureFormat.BGRA32, false);
            RenderTexture.active = rt;
            screenshot.ReadPixels(new Rect(0, 0, captureSizeX, captureSizeY), 0, 0);
            screenshot.Apply();

            // 카메라 렌더링 시작
            Color32[] campixcels = screenshot.GetPixels32();
            Color32 bridgeColor = campixcels[0];
            byte alpha = 0;
            for(int i=0;i< campixcels.Length;i++)
            {
                if (campixcels[i].Equals(bridgeColor))
                {
                    campixcels[i].a = alpha;
                }
            }
            screenshot.SetPixels32(campixcels);
            // PNG 파일로 저장
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);

            // 메모리 해제 및 렌더 텍스처 설정 해제
            RenderTexture.active = null;
            captureCam.targetTexture = null;
            //Destroy(rt);
        }
    }
}
