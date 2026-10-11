using UnityEngine;

namespace Seunghak.Data
{
    /// <summary>
    /// 캐릭터 1종의 정의 데이터.
    /// Resources/Game/Characters/{Name}/{Name}_Data.asset 경로에 저장.
    /// BundleManager, CharacterManager에서 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterData", menuName = "Seunghak/CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("식별자")]
        public E_CHARACTER_ID characterId;
        public string characterName;            // 표시 이름 (한글 가능)

        [Header("번들 정보")]
        public bool isDefault;                  // true = main 번들 포함 (기본 캐릭터)
        public string characterBundleName;      // isDefault=false 일 때만 사용 (ex: "character_cat")
        public string prefabName;               // 번들 내 프리팹 이름 (ex: "Cat")

        [Header("구매 정보")]
        public int price;                       // 0 = 무료
        public E_ITEM_TYPE priceType;           // GOLD or CRYSTALS

        [Header("아이콘")]
        public Sprite icon;                     // 캐릭터 선택 UI 썸네일

#if UNITY_EDITOR
        // 에디터 전용: 플레이어 빌드에 포함되면 Resources 경유로 캐릭터 프리팹 전체가 초기 다운로드에 딸려온다.
        [Header("에디터 미리보기")]
        [Tooltip("번들 없이 에디터에서 직접 확인할 때 사용. 런타임에는 BundleManager로 로드.")]
        public GameObject editorPreviewPrefab;
#endif
    }
}
