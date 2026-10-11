# 백만 번 죽는 네모 (가제) — Million Death Square

표정 있는 작은 네모를 움직여 장애물을 피해 동전을 모으고 골인하는 고난도 퍼즐액션.
캠페인(월드 5 × 10)으로 장치를 익히고, 에디터로 만든 맵을 **맵 코드**로 친구에게 던지고, 2~4명 비공개 방에서 같이 죽는다.

- 기획서: `Docs/MillionDeathSquare_PC_기획서.md` (기획: 민수님) — **PC(Steam)판이 주 플랫폼**, 모바일(토스 미니게임)판과 규칙·맵·에디터·아트 공유
- 엔진: Unity 2023.2.20f1 (2D: SpriteRenderer + 직교 카메라, 단일 씬 `Base.unity`)
- 서버 없음: 맵 공유 = Steam 창작마당, 기록 = Steam 순위표(플랫폼별 분리), 멀티 = Steam 로비 + 방장 중계
- 남은 일·결정 사항: `Docs/TODO.md`, 팀 보드: `.claude/team/board.md`

## 현재 상태 (2026-10-11)

| 영역 | 상태 |
|---|---|
| 결정적 코어 (`Assets/Scripts/Game/MillionDeathSquare/Core`) | ✅ 구현 + 테스트 71개 통과 (mcs/mono). 60틱/초 정수 연산, 맵 모델·검증·공유 코드·맵 코드, 시뮬레이션, 리플레이 검증, 캠페인, 에디터 모델, 멀티방, 후기, 기록 |
| 캠페인 월드 1 (10스테이지) | ✅ 정식 데이터, 모든 스테이지 0데스 풀이를 테스트가 재생 검증 |
| 캠페인 월드 2~5 (40스테이지) | 🟡 **임시 맵** (장치 소개용 템플릿, 깰 수 있음은 검증됨) — 레벨 기획 필요 |
| Unity 계층 (보드·캐릭터·연출·화면·에디터·멀티) | 🟡 작성 완료, Unity 참조 DLL + 스텁으로 **타입 검사만** 함 — Unity에서 확인 필요 |
| Steam (창작마당·순위표·로비·네트워킹·클라우드) | 🟡 `#if STEAMWORKS_NET`, Steamworks.NET 2024.8.0 참조로 **타입 검사만** 함 — 실기 검증 필요 |
| 오프라인 대체 (에디터용) | ✅ 로컬 창작마당/순위표(PlayerPrefs), 루프백 로비 + 디버그 봇 |
| 아트·사운드 | 🟡 자리표시: 도형 스프라이트 생성, 기존 효과음 재사용 |

## 처음 실행 (Unity에서 순서대로)

1. **Unity 2023.2.20f1**로 프로젝트를 연다. 패키지 복원이 끝날 때까지 기다린다 (TMP Essential Resources를 물으면 Import).
2. Console에 컴파일 에러가 없는지 확인한다.
   - 메뉴가 안 보이면 Console 에러부터 — Unity 계층은 이 저장소에서 Unity 없이 타입 검사만 했다.
3. **Window > General > Test Runner > EditMode > Run All** — `MillionDeathSquare.Tests.EditMode` 71개가 통과해야 한다.
4. **Tools > Million Death Square > 0. 전부 실행** (또는 1 → 2 → 5 → 3 순서로 하나씩)
   1. 스프라이트·팔레트·캠페인 에셋 생성 → `Assets/BaseSources/MillionDeathSquare/`
   2. 화면 프리팹 8종 생성 + Addressables UI 그룹 등록 → `Assets/BaseSources/Prefabs/UI/MDS*.prefab`
   3. Base 씬 연결 — `MillionDeathSquare`(디렉터) + `Board` 오브젝트, Main Camera를 직교로, 씬 컨트롤러 연결, BaseCanvas 기준 해상도 1920×1080
   4. (선택) 테스트 씬 생성 → `Assets/Scenes/MillionDeathSquareTest.unity` — UI 없이 보드만으로 바로 플레이 (N/B 스테이지 이동, F2 모든 월드 열기)
   5. 게임패드 방향 패드 축(`MDS_DPadX`/`MDS_DPadY`) 추가
5. `Assets/Scenes/Base.unity`를 열고 Play → 메인 화면 → 캠페인 → 1-1.
   - 에디터 → 맵 그리기 → 시험 플레이로 깨기 → 업로드(오프라인이면 로컬 창작마당) → 맵 찾기에 코드 입력.
   - 멀티방 → 방 만들기 → "봇 추가(개발용)" → 맵 `1-3` 고르기 → 시작 (혼자서 멀티 화면 시험).
6. 빌드 전: **Tools > Addressables > Build Player Content** (베이스 프로젝트 규칙과 같음).

### Steam 연결 (PC판)

1. Package Manager → **Add package from git URL**:
   `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2024.8.0`
2. **Tools > Million Death Square > Steam 켜기** — Standalone에 `STEAMWORKS_NET` 정의를 넣고 프로젝트 루트에 `steam_appid.txt`(지금은 480 = Spacewar 테스트 앱)를 만든다.
3. Steam 클라이언트를 켠 상태에서 Play. Console에 `[PlatformFactory] SteamPlatform 생성`, `[MDSServices] Steam 서비스 사용`이 나오면 연결된 것.
4. 실제 앱 ID가 나오면 `Assets/Scripts/Platform/MillionDeathSquare/MDSSteamAppId.cs`와 `steam_appid.txt`를 바꾼다.
   Steamworks 파트너 사이트에서 창작마당(UGC) 활성화, Steam 클라우드 용량, 순위표 자동 생성 허용(FindOrCreateLeaderboard) 설정이 필요하다.
- 끄기: **Steam 끄기** (정의 제거 → 로컬 서비스로 동작).

## 명령줄 (Unity 없이)

```bash
tools/run-core-tests.sh            # 코어 + EditMode 테스트를 mcs로 컴파일해 mono로 실행 (NUnit 대신 최소 대체 구현)
tools/run-core-tests.sh Campaign   # 이름 필터
tools/solve-campaign.sh 1          # 월드 1을 풀이 탐색기(MapSolver)로 풀어 0데스 입력 대본 출력 (맵을 고친 뒤 CampaignSolutions.cs 갱신)
```
코어(`Core/`)는 UnityEngine을 참조하지 않고 Mono `mcs`(C# 7)로 컴파일돼야 한다 — record, switch 식, `??=`, target-typed new, 타입 없는 `default`, nullable 주석 금지.

## 조작

| 상황 | 키 |
|---|---|
| 플레이 | 방향키 / WASD / 게임패드 왼쪽 스틱·방향 패드, **R** 처음부터(데스·시간 유지), **Esc** 일시정지 |
| 에디터 | 마우스 왼쪽 클릭·드래그 = 배치, 오른쪽 = 지우기, **1~0** 도구 1~10, **Shift+1~0** 도구 11~20, **Ctrl+Z / Ctrl+Y** 되돌리기/다시하기, F5 시험 플레이 |
| 장애물 경로 | 장애물 도구로 칸 클릭 = 장애물 놓기 → 이어서 클릭할 때마다 경로 지점(반 칸 격자, 최대 8) → Enter/오른쪽 클릭 = 완료, Backspace = 마지막 점 지우기 |

## 구조

```
Assets/Scripts/Game/MillionDeathSquare/
  Core/            결정적 코어 (asmdef MillionDeathSquare.Core, noEngineReferences)
  UI/              화면 (MDSMainWindow, MDSCampaignWindow, MDSPlayWindow, MDSEditorWindow, MDSMapFindWindow, MDSMapDetailPopup, MDSRoomWindow, MDSResultPopup)
  MDSGameDirector  판 흐름 + 60Hz 고정 스텝 드라이버(보간 표시) + 입력 기록
  MDSBoardView / MDSPlayerView / MDSObstacleView / MDSEyes / MDSEffects   2D 표시·연출
  MDSInput / MDSSaveStore / MDSSettings / MDSAnalytics / MDSMultiplayer / MDSMapResolver
Assets/Scripts/Platform/MillionDeathSquare/   창작마당·순위표·로비 인터페이스, 로컬 구현, Steam 구현
Assets/Scripts/Platform/SteamPlatform.cs       IPlatform의 Steam 구현 (+ Steam 클라우드 저장소)
Assets/Scripts/Editor/MillionDeathSquareSetupTool.cs   Tools/Million Death Square 메뉴
Assets/Tests/EditMode/                         NUnit EditMode 테스트 (tools/run-core-tests.sh와 같은 소스)
tools/                                         명령줄 테스트·풀이 도구
```
토스(모바일판) 플랫폼 계층(`TossPlatform`, `README_TOSS.md`)은 모바일판을 위해 그대로 둔다.

## 자리표시 (출시 전 교체)

- 월드 2~5 스테이지 40개 (`Core/CampaignPlaceholders.cs`, 이름에 "(임시)")
- 도형 스프라이트·팔레트 색 (셋업 툴 생성, `MDSPalettes.asset`에서 조정)
- 효과음·BGM: 베이스 프로젝트 클립 재사용 (`SoundKeys.MDS_*`)
- 맵 이름 금지어 목록 (`MapNameFilter.PlaceholderWords`)
- Steam 앱 ID 480, 한 줄 후기 문구 8개(`ReviewPhrases`), 화면 레이아웃(비율 배치)
