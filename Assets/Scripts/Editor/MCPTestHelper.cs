using UnityEditor;

namespace Seunghak.Editor
{
    /// <summary>
    /// MCP(Claude Code)로 Play Mode를 직접 제어하기 위한 임시 테스트 헬퍼.
    /// 테스트 완료 후 삭제 가능.
    /// </summary>
    public static class MCPTestHelper
    {
        [MenuItem("Tools/MCP/Enter Play Mode")]
        public static void EnterPlayMode()
        {
            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/MCP/Exit Play Mode")]
        public static void ExitPlayMode()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;
        }
    }
}
