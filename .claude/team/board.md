# 🟥 백만 번 죽는 네모 (가제) — 팀 작업 보드

> 마지막 업데이트: 2026-10-11 (claude)
> 기획서: `Docs/MillionDeathSquare_PC_기획서.md` (기획: 민수님 👑) — PC(Steam)판 + 공통 규칙. 모바일(토스)판은 별도 기획서
> 이전 게임 기록은 베이스 프로젝트 저장소 히스토리 참고. 남은 일 목록: `Docs/TODO.md`

---

## 1차 구현 (2026-10-11, claude — Unity 컴파일/플레이 미검증, 코어는 테스트 통과)

| 기획서 | 구현 | 위치 |
|---|---|---|
| 4장 캠페인 | 월드 5 × 10, 7개 클리어 → 다음 월드, 월드 클리어 → 에디터 장치 해금, 최소 데스·최단 시간·0데스 저장 | `Core/Campaign*.cs`, `MDSSaveStore` |
| 4.1 월드 1 | 10스테이지 정식 데이터 (왕복 장애물만). 전부 0데스 풀이 대본으로 테스트에서 증명 | `Core/CampaignWorld1.cs`, `CampaignSolutions.cs` |
| 4.1 월드 2~5 | **임시 맵** (원 회전 / 열쇠·문 / 얼음·컨베이어 / 사라지는 바닥) — 깰 수 있음은 증명됨, 레벨 기획 필요 | `Core/CampaignPlaceholders.cs` |
| 3장 아트 | 도형 스프라이트 생성(셋업 툴), 팔레트 8종, 눈 표정 5종(코드 전환), 늘어남·찌그러짐, 죽음 조각·화면 흔들림, 동전 톡, 골인 만세 | `MDSShapes`, `MDSPaletteAsset`, `MDSPlayerView`, `MDSObstacleView`, `MDSEffects` |
| 5장 에디터 | 칸 배치·지우기·드래그, 장애물 경로(8점, 왕복/반복/원, 속도 3단계, 출발 지연, 기차 복제), 되돌리기, 시험 플레이, 직접 깨야 업로드(어떤 편집이든 다시 잠김) | `Core/MapEditorModel.cs`, `MDSEditorWindow` |
| 6장 공유 | 창작마당 "목록에 표시 안 함"(실패 시 친구만), 맵 코드(항목 번호 → 8자 안팎), 공유 코드(MDS1-…), 좋아요·체감 난이도·정해진 문구 후기, 클리어율 | `MDSSteamServices`, `MDSLocalServices`, `MapCode`, `MapSerializer` |
| 6.4 기록 | 맵별 최소 데스·최단 시간 순위표, **플랫폼별 분리**, 제출 전 리플레이 검증 | `ILeaderboardService`, `RecordBoards`, `ReplayVerifier` |
| 7장 멀티방 | 비공개 2~4인, 방 코드·친구 초대, 레이스/협동/생존(3분), 방장 중계(위치 20Hz), 시작 틱 동기화, 결과(순위·가장 많이 죽은 장소), 서로 통과·반투명 | `Core/RoomSession.cs`, `Core/Multiplayer.cs`, `MDSMultiplayer`, `MDSRoomWindow` |
| 8장 조작 | 방향키·WASD·게임패드(스틱+방향 패드 축), R 처음부터, Esc 일시정지 | `MDSInput` |
| 11장 화면 | 메인 / 캠페인 선택 / 플레이(데스 크게) / 에디터 / 맵 찾기 / 맵 상세·후기 / 멀티방 / 결과 | `UI/MDS*.cs` + 셋업 툴 프리팹 |
| 10장 운영 | 맵 이름 금지어 필터 연결 지점(목록은 자리표시), 후기 자유 입력 없음, 공개 목록 없음 | `MapNameFilter` |

- **테스트**: `tools/run-core-tests.sh` (mcs+mono, 71개) / Unity Test Runner EditMode (같은 소스)
- **Unity 계층**: Unity 모듈 참조 DLL + 패키지 스텁으로 타입 검사만 함. Steam 코드는 Steamworks.NET 2024.8.0 참조로 타입 검사만 함 → **실제 에디터/빌드 확인 필요**
- **에디터 작업 순서**: `Tools/Million Death Square/0. 전부 실행` → Base.unity Play (README "처음 실행")

## 🔴 결정 필요 (기획/운영)
| # | 내용 | 비고 |
|---|------|------|
| D-1 | Steam 앱 ID | 지금은 480(Spacewar) — `MDSSteamAppId.Value`, `steam_appid.txt` |
| D-2 | 창작마당 "목록에 표시 안 함" 지원 확인 | 코드는 Unlisted 시도 → 실패 시 FriendsOnly |
| D-3 | 기본가 5,500 / 6,600원 | |
| D-4 | 월드 2~5 스테이지 40개 레벨 기획 | 임시 맵 교체, `tools/solve-campaign.sh`로 풀이 재생성 |
| D-5 | 맵 이름 금지어 목록 | `MapNameFilter.PlaceholderWords` 교체 |

## 🟠 다음 작업
| # | 작업 | 담당 |
|---|------|------|
| N-1 | Unity에서 컴파일·EditMode 테스트·Base 씬 플레이 확인 | dev-logic-core / qa-engineer |
| N-2 | Steamworks.NET 설치 + Steam 켜기 → 업로드·맵 코드·순위표·로비 실기 검증 (2계정) | dev-platform / dev-network |
| N-3 | 전용 효과음 15종·BGM 7곡 (지금은 기존 클립 재사용) | dev-sound |
| N-4 | UI 프리팹 레이아웃 다듬기 (셋업 툴은 비율 배치만) | dev-ui-game |
| N-5 | 모바일판(토스) 기획서 반영 — 터치 입력(MDSInput.VirtualMove), 세로/가로 결정 | design-ux |
