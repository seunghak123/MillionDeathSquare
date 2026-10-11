using System;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class RegenerateKoreanFontAtlas
{
    private const string SourceFontPath = "Assets/BaseSources/UI/Font/ONE Mobile POP OTF.otf";
    private const string TargetAssetPath = "Assets/BaseSources/UI/Font/ONE Mobile POP OTF SDF.asset";
    private const int AtlasWidth = 4096;
    private const int AtlasHeight = 4096;
    private const int AtlasPadding = 6;
    private static readonly int[] CandidatePointSizes = { 72, 66, 60, 54, 48, 42, 36 };

    public static void Execute()
    {
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError("[RegenerateKoreanFontAtlas] 소스 폰트를 찾을 수 없습니다: " + SourceFontPath);
            return;
        }

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TargetAssetPath);
        if (existing == null)
        {
            Debug.LogError("[RegenerateKoreanFontAtlas] 기존 폰트 에셋을 찾을 수 없습니다: " + TargetAssetPath);
            return;
        }

        string characterSet = BuildCharacterSet(out int hangulCount);
        Debug.Log($"[RegenerateKoreanFontAtlas] EUC-KR 라운드트립 한글 음절 수: {hangulCount}, 전체 문자 수: {characterSet.Length}");

        TMP_FontAsset newAsset = null;
        int usedPointSize = 0;
        string finalMissing = null;
        int previousMissingCount = -1;

        foreach (int pointSize in CandidatePointSizes)
        {
            var candidate = TMP_FontAsset.CreateFontAsset(
                sourceFont, pointSize, AtlasPadding, GlyphRenderMode.SDFAA,
                AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, true);

            if (candidate == null)
            {
                Debug.LogWarning($"[RegenerateKoreanFontAtlas] pointSize={pointSize} CreateFontAsset 실패, 다음 크기로 재시도");
                continue;
            }

            bool allAdded = candidate.TryAddCharacters(characterSet, out string missing);
            if (allAdded)
            {
                newAsset = candidate;
                usedPointSize = pointSize;
                finalMissing = string.Empty;
                Debug.Log($"[RegenerateKoreanFontAtlas] pointSize={pointSize} 에서 전체 문자 수용 성공");
                break;
            }

            // 누락 개수가 이전 시도(더 큰 point size)와 동일하면 아틀라스 공간 문제가 아니라
            // 폰트 파일 자체에 해당 글리프가 없는 것 — 더 줄여봐야 소용없으므로 이 크기를 채택하고 중단.
            if (missing.Length == previousMissingCount)
            {
                newAsset = candidate;
                usedPointSize = pointSize;
                finalMissing = missing;
                Debug.LogWarning($"[RegenerateKoreanFontAtlas] pointSize={pointSize} — 누락 {missing.Length}자는 폰트 파일에 글리프 자체가 없어 발생 (크기와 무관, 축소 중단). 이 크기로 채택.");
                break;
            }

            Debug.LogWarning($"[RegenerateKoreanFontAtlas] pointSize={pointSize} 실패 — 누락 {missing.Length}자, 더 작은 크기로 재시도");
            previousMissingCount = missing.Length;
            finalMissing = missing;
            usedPointSize = pointSize;
            if (newAsset != null) UnityEngine.Object.DestroyImmediate(newAsset, true);
            newAsset = candidate;
        }

        if (newAsset == null)
        {
            Debug.LogError("[RegenerateKoreanFontAtlas] 모든 후보 크기에서 실패했습니다. 중단합니다 (기존 에셋은 변경되지 않았습니다).");
            return;
        }

        if (!string.IsNullOrEmpty(finalMissing))
            Debug.LogWarning($"[RegenerateKoreanFontAtlas] 최종 누락 문자({finalMissing.Length}자): {finalMissing}");

        newAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        // 기존 서브에셋(텍스처/머티리얼) 제거 후 새 것으로 교체 — 파일 GUID는 유지되어 기존 참조가 깨지지 않는다.
        var oldSubAssets = AssetDatabase.LoadAllAssetsAtPath(TargetAssetPath);
        foreach (var sub in oldSubAssets)
        {
            if (sub == null || sub is TMP_FontAsset) continue;
            UnityEngine.Object.DestroyImmediate(sub, true);
        }

        EditorUtility.CopySerialized(newAsset, existing);

        if (newAsset.atlasTextures != null)
        {
            foreach (var tex in newAsset.atlasTextures)
            {
                if (tex != null)
                {
                    tex.name = existing.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(tex, existing);
                }
            }
        }
        if (newAsset.material != null)
        {
            newAsset.material.name = existing.name + " Material";
            AssetDatabase.AddObjectToAsset(newAsset.material, existing);
        }

        EditorUtility.SetDirty(existing);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(TargetAssetPath, ImportAssetOptions.ForceUpdate);

        UnityEngine.Object.DestroyImmediate(newAsset, true);

        Debug.Log($"[RegenerateKoreanFontAtlas] 완료. samplingPointSize={usedPointSize} (기존 32 대비 {usedPointSize / 32.0:0.0}배)");
    }

    private static string BuildCharacterSet(out int hangulCount)
    {
        var sb = new StringBuilder();
        hangulCount = 0;

        Encoding euckr;
        try { euckr = Encoding.GetEncoding(51949); }
        catch (Exception e)
        {
            Debug.LogWarning("[RegenerateKoreanFontAtlas] EUC-KR 인코딩 로드 실패, 전체 한글 음절(11172자)로 폴백: " + e.Message);
            euckr = null;
        }

        if (euckr != null)
        {
            for (int cp = 0xAC00; cp <= 0xD7A3; cp++)
            {
                char c = (char)cp;
                byte[] bytes = euckr.GetBytes(c.ToString());
                if (bytes.Length != 2) continue;
                string back = euckr.GetString(bytes);
                if (back.Length == 1 && back[0] == c)
                {
                    sb.Append(c);
                    hangulCount++;
                }
            }
        }
        else
        {
            for (int cp = 0xAC00; cp <= 0xD7A3; cp++)
            {
                sb.Append((char)cp);
                hangulCount++;
            }
        }

        for (int cp = 0x1100; cp <= 0x11FF; cp++) sb.Append((char)cp); // 한글 자모
        for (int cp = 0x3130; cp <= 0x318F; cp++) sb.Append((char)cp); // 한글 호환 자모
        for (int cp = 0x0020; cp <= 0x00FF; cp++) sb.Append((char)cp); // 라틴 + 라틴1 보충

        return sb.ToString();
    }
}
