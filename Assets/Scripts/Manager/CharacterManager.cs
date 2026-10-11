using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Seunghak.Data;
using Seunghak.Platform;
using UnityEngine;

namespace Seunghak.Manager
{
    /// <summary>
    /// 캐릭터 스킨 시스템 관리자.
    /// - 보유 목록: PlayerPrefs + JSON 직렬화
    /// - 현재 선택 캐릭터: PlayerPrefs 저장/로드
    /// - 캐릭터 번들 로드/언로드 (BundleManager 위임)
    /// - CharacterData ScriptableObject 캐싱 (Resources.Load)
    ///
    /// CharacterData 경로 규칙:
    ///   E_CHARACTER_ID.CAT → "Game/Characters/Cat/Cat_Data"
    /// </summary>
    public class CharacterManager : UnitySingleton<CharacterManager>
    {
        // ── PlayerPrefs 키 ────────────────────────────────────────────────
        private const string PREF_KEY_SELECTED = "SelectedCharacter";
        private const string PREF_KEY_OWNED    = "OwnedCharacters";   // JSON int[]

        // ── 상태 ──────────────────────────────────────────────────────────
        private E_CHARACTER_ID _selectedCharacterId = E_CHARACTER_ID.CHICK;
        private readonly HashSet<E_CHARACTER_ID> _ownedCharacters = new HashSet<E_CHARACTER_ID>();

        /// <summary>CharacterData ScriptableObject 캐시.</summary>
        private readonly Dictionary<E_CHARACTER_ID, CharacterData> _dataCache
            = new Dictionary<E_CHARACTER_ID, CharacterData>();

        /// <summary>현재 로드된 캐릭터 번들 이름 (character_xxx). null = 기본 캐릭터.</summary>
        private string _currentLoadedCharacterBundle = null;

        // ── 프로퍼티 ──────────────────────────────────────────────────────
        public E_CHARACTER_ID SelectedCharacterId => _selectedCharacterId;

        // ── 초기화 ────────────────────────────────────────────────────────
        protected override void InitSingleton()
        {
            LoadOwnedCharacters();
            LoadSelectedCharacter();
        }

        /// <summary>
        /// PlayerPrefs에서 보유/선택 캐릭터를 다시 읽습니다.
        /// 플랫폼 저장소 스냅샷을 PlayerPrefs에 복원한 직후 호출됩니다.
        /// </summary>
        public void ReloadFromLocalCache()
        {
            LoadOwnedCharacters();
            LoadSelectedCharacter();
        }

        // ── 캐릭터 선택 ───────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터를 선택하고 해당 프리팹을 반환.
        /// - 미보유 캐릭터: null 반환
        /// - 기본 캐릭터(isDefault): main 번들에서 로드
        /// - 추가 캐릭터: 개별 번들 로드 후 프리팹 반환, 이전 번들 자동 언로드
        /// </summary>
        public async UniTask<GameObject> SelectCharacterAsync(E_CHARACTER_ID characterId,
                                                               CancellationToken ct = default)
        {
            if (!IsOwned(characterId))
            {
                Debug.LogWarning($"[CharacterManager] 미보유 캐릭터: {characterId}");
                return null;
            }

            CharacterData data = GetCharacterData(characterId);
            if (data == null)
            {
                Debug.LogError($"[CharacterManager] CharacterData 없음: {characterId}");
                return null;
            }

            // 선택값은 번들 로드보다 먼저 확정한다. 예전엔 번들 로드(await)가 끝난 뒤에야
            // 갱신해서, 로드가 느린 WebGL에서 캐릭터를 고르자마자 Play를 누르거나 로드가
            // 예외로 끝나면 선택이 저장되지 않아 이전 캐릭터로 인게임에 진입했다.
            // 인게임은 캐릭터 프리팹을 쓰지 않으므로(도형 네모) 이 번들을 기다릴 필요가 없다.
            _selectedCharacterId = characterId;
            SaveSelectedCharacter();

            // 이전 캐릭터 번들 언로드 (새 캐릭터와 다른 번들일 때만)
            UnloadPreviousCharacterBundle(data);

            // 비기본 캐릭터: 개별 번들 로드
            if (!data.isDefault)
            {
                try
                {
                    await BundleManager.Instance.LoadBundleAsync(data.characterBundleName, ct);
                    _currentLoadedCharacterBundle = data.characterBundleName;
                }
                catch (System.OperationCanceledException)
                {
                    throw;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CharacterManager] 캐릭터 번들 로드 실패(선택은 유지): {data.characterBundleName} - {e.Message}");
                    return null;
                }
            }
            else
            {
                _currentLoadedCharacterBundle = null;
            }

            // 에셋 로드
            string bundleName = data.isDefault ? "main" : data.characterBundleName;
            GameObject prefab = BundleManager.Instance.LoadAsset<GameObject>(bundleName, data.prefabName);

            if (prefab == null)
            {
                // 이 반환값은 상점 미리보기 용도일 뿐, 인게임(도형 네모)은
                // 이 값을 쓰지 않는다. 캐릭터 번들이 아직 준비되지 않아 미리보기를 못 찾더라도
                // 선택 자체(= _selectedCharacterId 갱신)까지 막을 이유는 없다.
                Debug.LogWarning($"[CharacterManager] 미리보기 프리팹 로드 실패(선택은 계속 진행): {data.prefabName} (번들: {bundleName})");
            }

            Debug.Log($"[CharacterManager] 캐릭터 선택 완료: {characterId}");
            return prefab;
        }

        // ── 소유 관리 ─────────────────────────────────────────────────────

        /// <summary>캐릭터 보유 여부. 기본 캐릭터(isDefault=true)는 항상 true.</summary>
        public bool IsOwned(E_CHARACTER_ID characterId)
        {
            CharacterData data = GetCharacterData(characterId);
            if (data != null && data.isDefault) return true;
            return _ownedCharacters.Contains(characterId);
        }

        /// <summary>캐릭터 보유 추가 (결제/보상 후 호출).</summary>
        public void AddOwnedCharacter(E_CHARACTER_ID characterId)
        {
            _ownedCharacters.Add(characterId);
            SaveOwnedCharacters();
            Debug.Log($"[CharacterManager] 캐릭터 획득: {characterId}");
        }

        public IReadOnlyCollection<E_CHARACTER_ID> GetOwnedCharacters() => _ownedCharacters;

        // ── CharacterData 접근 ────────────────────────────────────────────

        /// <summary>
        /// CharacterData ScriptableObject 로드 (캐싱).
        /// 경로 규칙: E_CHARACTER_ID.CAT → Resources/Game/Characters/Cat/Cat_Data
        /// </summary>
        public CharacterData GetCharacterData(E_CHARACTER_ID characterId)
        {
            if (_dataCache.TryGetValue(characterId, out var cached)) return cached;

            string idName = characterId.ToString();
            string capitalized = char.ToUpper(idName[0]) + idName.Substring(1).ToLower();
            string resourcePath = $"Game/Characters/{capitalized}/{capitalized}_Data";

            var data = Resources.Load<CharacterData>(resourcePath);
            if (data == null)
            {
                Debug.LogWarning($"[CharacterManager] CharacterData 없음: Resources/{resourcePath}");
                return null;
            }

            _dataCache[characterId] = data;
            return data;
        }

        /// <summary>캐릭터 선택 UI 열기 전 전체 데이터 미리 캐싱.</summary>
        public void PreloadAllCharacterData()
        {
            foreach (E_CHARACTER_ID id in System.Enum.GetValues(typeof(E_CHARACTER_ID)))
            {
                if (id == E_CHARACTER_ID.NONE) continue;
                GetCharacterData(id);
            }
        }

        // ── 내부 ──────────────────────────────────────────────────────────

        private void UnloadPreviousCharacterBundle(CharacterData newData)
        {
            if (_currentLoadedCharacterBundle == null) return;
            // 새 캐릭터가 같은 번들이면 유지
            if (!newData.isDefault && _currentLoadedCharacterBundle == newData.characterBundleName) return;

            BundleManager.Instance.UnloadBundle(_currentLoadedCharacterBundle, unloadAllObjects: true);
            _currentLoadedCharacterBundle = null;
        }

        private void LoadOwnedCharacters()
        {
            _ownedCharacters.Clear();
            _ownedCharacters.Add(E_CHARACTER_ID.CHICK); // 기본 캐릭터 항상 보유

            string json = PlayerPrefs.GetString(PREF_KEY_OWNED, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                // JsonUtility는 int[] 직렬화 미지원 → 간단한 파싱 사용
                json = json.Trim('[', ']');
                if (string.IsNullOrEmpty(json)) return;
                foreach (string part in json.Split(','))
                {
                    if (int.TryParse(part.Trim(), out int id))
                        _ownedCharacters.Add((E_CHARACTER_ID)id);
                }
            }
            catch { Debug.LogWarning("[CharacterManager] OwnedCharacters 파싱 실패"); }
        }

        private void SaveOwnedCharacters()
        {
            var parts = new List<string>();
            foreach (var id in _ownedCharacters)
                parts.Add(((int)id).ToString());
            string json = "[" + string.Join(",", parts) + "]";
            PlayerPrefs.SetString(PREF_KEY_OWNED, json);
            PlatformSaveSync.MarkDirty();
            PlayerPrefs.Save();
        }

        private void LoadSelectedCharacter()
        {
            int saved = PlayerPrefs.GetInt(PREF_KEY_SELECTED, (int)E_CHARACTER_ID.CHICK);
            _selectedCharacterId = (E_CHARACTER_ID)saved;
        }

        private void SaveSelectedCharacter()
        {
            PlayerPrefs.SetInt(PREF_KEY_SELECTED, (int)_selectedCharacterId);
            PlatformSaveSync.MarkDirty();
            PlayerPrefs.Save();
        }

        /// <summary>개발자용 치트: 보유 캐릭터를 기본 캐릭터(CHICK)만 남기고 초기화하고,
        /// 선택 캐릭터도 CHICK로 되돌립니다. 번들 로드 상태는 다음 SelectCharacterAsync 호출 시
        /// 자연스럽게 정리되므로 여기서는 건드리지 않습니다.</summary>
        public void ResetOwnedCharacters()
        {
            _ownedCharacters.Clear();
            _ownedCharacters.Add(E_CHARACTER_ID.CHICK);
            PlayerPrefs.DeleteKey(PREF_KEY_OWNED);

            _selectedCharacterId = E_CHARACTER_ID.CHICK;
            PlayerPrefs.DeleteKey(PREF_KEY_SELECTED);
            PlatformSaveSync.MarkDirty();
            PlayerPrefs.Save();

            Debug.Log("[CharacterManager] 개발자 치트: 보유/선택 캐릭터 초기화 완료");
        }
    }
}
