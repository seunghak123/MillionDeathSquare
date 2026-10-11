# 백만 번 죽는 네모 — 남은 일 (2026-10-11)

기획서 14장 "남은 결정"과 구현 중 생긴 확인 사항. 끝나면 지우고 `.claude/team/board.md`에 기록.

## 1. 결정 필요 (기획·사업)

- [ ] **Steam 앱 ID** — 지금 480(Spacewar). `Assets/Scripts/Platform/MillionDeathSquare/MDSSteamAppId.cs` + 프로젝트 루트 `steam_appid.txt`(셋업 툴이 생성, 출시 빌드에는 넣지 않음)
- [ ] **기본가** 5,500원 / 6,600원 (기획서 9장, 14.1)
- [ ] **스테이지 11~50 레벨 기획** — 월드 2~5는 임시 템플릿(`Core/CampaignPlaceholders.cs`). 우리 에디터로 만들어 `MDSCampaign.asset`에 공유 코드로 넣거나 코어 데이터로 옮긴 뒤 `tools/solve-campaign.sh`로 풀이 대본을 갱신(`CampaignSolutions.cs`). 평균 데스 목표: 월드 1 ≤5, 월드 5 ≈50 (풀이 탐색기는 "0데스로 깰 수 있는지"만 증명함)
- [ ] 체크포인트 밀도 규칙 (초반 넉넉, 후반 감소) — 월드 2~5 기획 때 반영
- [ ] 맵 이름 **금지어 목록** — `MapNameFilter.PlaceholderWords`는 자리표시. 운영 목록으로 교체하거나 `MapNameFilter.Current`에 다른 필터 연결
- [ ] 한 줄 후기 정해진 문구 확정 (`ReviewPhrases`, 뒤에만 추가 가능 — 번호가 저장됨)
- [ ] 정식 게임 이름 (가제 유지 중), 무료 체험판 범위(월드 1 + 유저 맵 플레이)

## 2. Steamworks 확인 (기획서 6.1, 10장)

- [ ] **창작마당 "목록에 표시 안 함"(Unlisted) 지원** — 코드는 `k_ERemoteStoragePublishedFileVisibilityUnlisted`로 올리고 실패하면 `FriendsOnly`로 다시 시도 (`SteamWorkshopService.UploadAsync`, 결과의 `AppliedVisibility`). 실제 동작 확인 필요
- [ ] 창작마당 평가·댓글: 좋아요는 `SetUserItemVote`, 체감 난이도·문구·시도 수는 **맵별 순위표에 한 줄씩**(`mds_w_{코드}_review`, `_attempt`) 저장하는 방식으로 우회함 — 순위표 수 제한/정책 확인
- [ ] 메타데이터(공유 코드) 5000바이트 한도 — 32×18 + 장애물 60개 최악 경우 크기 측정
- [ ] 1인당 업로드 50개 제한은 로컬 구현만 검사함 — Steam에서는 `GetMyItemsAsync` 개수로 막을지 결정
- [ ] 창작마당 약관 동의(`NeedsLegalAgreement`) 시 오버레이로 약관 페이지 열기
- [ ] Steam 클라우드 용량·파일 수 설정 (저장 스냅샷 + 리플레이 파일 `replay_*.mdr`)
- [ ] 순위표 자동 생성(FindOrCreateLeaderboard) 허용 — 맵마다 4개(+후기·시도) 생성됨. 수가 많아질 때 정책 확인
- [ ] 로비: "비공개" = Public 로비 + 코드 필터(`mds_code`)로만 검색. Private/Invisible 로비로 바꿀지 검토 (코드 입장이 안 됨)
- [ ] 신고: Steam 창작마당 신고 기능 연결 (운영자 확인 절차)

## 3. mcs / 타입 검사로만 확인한 것 (Unity·Steam 실기 확인 필요)

이 저장소 작업 환경에는 Unity·Steam이 없다. 코어는 실제로 컴파일·테스트했지만 나머지는 아래 수준까지만 확인했다.

- **Unity 계층 전체** (`Game/MillionDeathSquare/*.cs`, `UI/*.cs`, `Platform/MillionDeathSquare/*.cs`, 수정한 공용 창·씬 컨트롤러): `mcs --parse` + Unity 모듈 참조 DLL과 UGUI/TMP/UniTask 스텁으로 타입 검사. 런타임 동작은 미확인
- **셋업 툴** (`Editor/MillionDeathSquareSetupTool.cs`): 타입 검사(구버전 UnityEditor 참조, `NamedBuildTarget` 제외). 프리팹 레이아웃·Addressables 등록·씬 연결 결과는 Unity에서 확인
- **Steam 구현** (`MDSSteamServices.cs`, `SteamPlatform.cs`): Steamworks.NET 2024.8.0 참조 DLL로 타입 검사. 콜백 순서·에러 처리·Unlisted·네트워킹 세션 수락은 2계정 실기 테스트 필요
- **EditMode 테스트 asmdef**: 같은 소스를 mcs + NUnit 대체 구현으로 돌림. Unity Test Runner에서 한 번 확인
- TMP `textWrappingMode` (ugui 2.0 API) — 구버전 TMP면 `enableWordWrapping`으로
- 게임패드 방향 패드 축 번호(6·7번째 축)는 Windows XInput 기준 — macOS/Linux·다른 패드 확인
- 새 스크립트의 `.meta`는 Unity가 처음 열 때 만든다 (GUID가 기기마다 달라질 수 있으니 첫 import 후 바로 커밋)

## 4. 구현 다듬기

- [ ] 전용 아트(눈 표정 5종·장애물 눈 2종 스프라이트, 타일 무늬), 파티클 4종 다듬기, 로고
- [ ] 효과음 15종(죽음 3종 랜덤 등)·BGM 7곡 — 지금은 `SoundKeys.MDS_*`가 기존 클립을 가리킴
- [ ] 화면 레이아웃: 셋업 툴은 비율 배치만 함. 맵 미리보기·결과 화면 "가장 많이 죽은 장소" 보드 표시(지금은 칸 좌표 문구)
- [ ] 멀티: 방장 이탈 시 방장 넘기기 후 세션 재구성(코어 `Room.Leave`는 넘기지만 `RoomSession` 방장 전환은 미구현), 늦게 들어온 사람 관전, 레이스 동시 골인 표시
- [ ] 리플레이 공유: 순위표에 붙인 입력 기록(`AttachLeaderboardUGC`)을 받아 "1등 유령" 재생
- [ ] 제작자 프로필(내 맵에서 죽은 총 횟수 — 지금은 `CreatorProfile` 모델만, 집계 수단 필요)
- [ ] 2·3단계 공개(추천 맵 / 전체 공개 목록) — 운영 인력 확보 후 (기획서 6.1)
- [ ] 모바일판: 터치 패드 → `MDSInput.VirtualMove`, 기록 플랫폼 `RecordPlatform.Mobile`, 공유 코드 붙여넣기 흐름

## 5. 기타 이름 정리

- 베이스 프로젝트에서 넘어온 레거시 창(`GameStartWindow`, `InGameHUDWindow`, `GameOverPopup`, 상점/랭킹 등)과 프리팹은 호환용으로 남겨 둠 — 쓰지 않으면 정리
- 3D 스카이박스·FX_Hit 등 베이스 에셋도 쓰지 않으면 정리 (보수적으로 남겨 둠)
