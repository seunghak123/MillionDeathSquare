using UnityEditor;
using UnityEngine;
using Seunghak.Data;

namespace Seunghak.Editor
{
    /// <summary>
    /// 캐릭터 데이터 ScriptableObject 자동 생성 에디터 툴.
    /// Menu: Seunghak > Create Character Data Assets
    ///
    /// CharacterManager 경로 규칙:
    ///   E_CHARACTER_ID.CHICK → Resources/Game/Characters/Chick/Chick_Data.asset
    /// </summary>
    public static class CreateCharacterDataAssets
    {
        private struct CharacterSpec
        {
            public E_CHARACTER_ID id;
            public string displayName;   // 한글 표시 이름
            public bool isDefault;
            public string bundleName;    // isDefault=false일 때만 사용
            public string prefabName;    // 번들 내 프리팹 이름
            public int price;
            public E_ITEM_TYPE priceType;
        }

        private static readonly CharacterSpec[] Specs =
        {
            new CharacterSpec
            {
                id          = E_CHARACTER_ID.CHICK,
                displayName = "솜이",
                isDefault   = true,
                bundleName  = "",
                prefabName  = "Bird",   // main 번들의 Bird 프리팹
                price       = 0,
                priceType   = E_ITEM_TYPE.GOLD,
            },
            new CharacterSpec
            {
                id          = E_CHARACTER_ID.CAT,
                displayName = "망고",
                isDefault   = false,
                bundleName  = "character_cat",
                prefabName  = "Cat",
                price       = 200,
                priceType   = E_ITEM_TYPE.GOLD,
            },
            new CharacterSpec
            {
                id          = E_CHARACTER_ID.DOG,
                displayName = "루비",
                isDefault   = false,
                bundleName  = "character_dog",
                prefabName  = "Dog",
                price       = 300,
                priceType   = E_ITEM_TYPE.GOLD,
            },
            new CharacterSpec
            {
                id          = E_CHARACTER_ID.CHINCHILLA,
                displayName = "포포",
                isDefault   = false,
                bundleName  = "character_chinchilla",
                prefabName  = "Chinchilla",
                price       = 400,
                priceType   = E_ITEM_TYPE.GOLD,
            },
            new CharacterSpec
            {
                id          = E_CHARACTER_ID.SQUIRREL,
                displayName = "다람쥐",
                isDefault   = false,
                bundleName  = "character_squirrel",
                prefabName  = "Squirrel",
                price       = 300,
                priceType   = E_ITEM_TYPE.GOLD,
            },
            new CharacterSpec
            {
                id          = E_CHARACTER_ID.RABBIT,
                displayName = "토끼",
                isDefault   = false,
                bundleName  = "character_rabbit",
                prefabName  = "Rabbit",
                price       = 300,
                priceType   = E_ITEM_TYPE.GOLD,
            },
            new CharacterSpec
            {
                id          = E_CHARACTER_ID.DRAGON,
                displayName = "드래곤",
                isDefault   = false,
                bundleName  = "character_dragon",
                prefabName  = "Dargon_Pre",
                price       = 1000,
                priceType   = E_ITEM_TYPE.GOLD,
            },
        };

        [MenuItem("Seunghak/Create Character Data Assets")]
        public static void CreateAll()
        {
            int created = 0;
            int skipped = 0;

            foreach (var spec in Specs)
            {
                string idStr      = spec.id.ToString();
                string capitalized = char.ToUpper(idStr[0]) + idStr.Substring(1).ToLower();
                string folder     = $"Assets/Resources/Game/Characters/{capitalized}";
                string assetPath  = $"{folder}/{capitalized}_Data.asset";

                // 폴더 없으면 생성
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = $"Assets/Resources/Game/Characters";
                    EnsureFolder("Assets/Resources", "Game");
                    EnsureFolder("Assets/Resources/Game", "Characters");
                    EnsureFolder(parent, capitalized);
                }

                // 이미 존재하면 스킵
                if (AssetDatabase.LoadAssetAtPath<CharacterData>(assetPath) != null)
                {
                    Debug.Log($"[CharacterData] 이미 존재, 스킵: {assetPath}");
                    skipped++;
                    continue;
                }

                var data = ScriptableObject.CreateInstance<CharacterData>();
                data.characterId         = spec.id;
                data.characterName       = spec.displayName;
                data.isDefault           = spec.isDefault;
                data.characterBundleName = spec.bundleName;
                data.prefabName          = spec.prefabName;
                data.price               = spec.price;
                data.priceType           = spec.priceType;

                AssetDatabase.CreateAsset(data, assetPath);
                created++;
                Debug.Log($"[CharacterData] 생성 완료: {assetPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Character Data 생성 완료",
                $"생성: {created}개\n스킵(이미 존재): {skipped}개\n\n경로: Assets/Resources/Game/Characters/",
                "확인"
            );
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
