# 백만 번 죽는 네모 (가제) — Million Death Square — Unity Project

## 프로젝트 개요
- **게임**: 백만 번 죽는 네모 (가제, 코드명 `MillionDeathSquare`) — 표정 있는 네모를 움직여 장애물을 피해 동전을 모두 모으고 골인하는 고난도 퍼즐액션. 캠페인(월드 5 × 10) + 맵 에디터·맵 코드 공유 + 2~4인 비공개 멀티방
- **기획서**: `Docs/MillionDeathSquare_PC_기획서.md` (기획: 민수님) — PC(Steam)판 + 공통 규칙. 규칙/수치는 이 문서가 기준
- **판 구성**: **PC(Steam)판이 주 플랫폼**. 모바일판(토스 미니게임)은 규칙·맵·에디터·아트를 공유하며 별도 기획서로 다룸 → 베이스의 Toss 플랫폼 계층은 모바일판용으로 유지
- **서버 없음**: 맵 공유 = Steam 창작마당(1단계 "목록에 표시 안 함" + 맵 코드), 맵 기록 = Steam 순위표(플랫폼별 분리), 멀티방 = Steam 로비 + 방장 중계
- **이름 규칙**: 새 저장 키는 `MDS` 접두사, 분석 이벤트는 `mds_` 접두사, 저장 스냅샷 키 `MillionDeathSquareSaveSnapshot`, Addressables 라벨 `milliondeathsquare`, APK 패키지 `com.seunghak.milliondeathsquare`, 제품명 `백만 번 죽는 네모`(ASCII가 필요하면 `Million Death Square`). 이전 게임 이름(Fossil/Suika/수박)은 쓰지 않는다
- **엔진**: Unity 2023.2.20f1, 2D(SpriteRenderer + 직교 카메라), 단일 씬 아키텍처
- **비동기**: UniTask (Cysharp.Threading.Tasks) / **에셋**: Addressables + AssetBundle

## 코드 구조
- **결정적 코어** `Assets/Scripts/Game/MillionDeathSquare/Core/` (asmdef `MillionDeathSquare.Core`, `noEngineReferences: true`)
  - UnityEngine 참조 금지. Mono `mcs`(C# 7.x)로 컴파일돼야 함 — record, switch 식, `??=`, target-typed new, 타입 없는 `default`, nullable 주석 금지
  - 60틱/초 고정 스텝, 정수(1칸 = `Fixed.CELL` 단위) 연산만. 장애물 위치는 틱의 순수 함수 → 멀티 동기화·리플레이 검증의 기반
  - 맵 모델/검증/직렬화(공유 코드·맵 코드), 시뮬레이션, 입력 기록·재생 검증, 캠페인·저장 모델, 에디터 모델(되돌리기·업로드 게이트), 멀티방 모델·메시지, 후기, 플랫폼별 기록
- **Unity 계층** `Assets/Scripts/Game/MillionDeathSquare/` (네임스페이스 `Seunghak.Game.MillionDeathSquare`) — 고정 스텝 드라이버(보간 표시), 보드/캐릭터 뷰, 입력, 화면(UI_TYPE `MDS*`)
- **플랫폼 서비스** `Assets/Scripts/Platform/MillionDeathSquare/` — `IWorkshopService`/`ILeaderboardService`/`ILobbyService`/`INetTransport`. 로컬 구현(에디터·오프라인)과 Steamworks.NET 구현(`#if STEAMWORKS_NET`)
- **셋업 툴** `Tools/Million Death Square/...` (`Assets/Scripts/Editor/MillionDeathSquareSetupTool.cs`) — 스프라이트·팔레트·캠페인 에셋·UI 프리팹(Addressables 등록)·테스트 씬·Base.unity 연결
- **테스트**: `Assets/Tests/EditMode` (NUnit) + `tools/run-core-tests.sh` (mcs + mono, NUnit 없이 코어만). 코어를 바꾸면 반드시 둘 다 통과시킨다

## 아키텍처 규칙
- 단일 씬(`Base.unity`)에서 뷰 전환 (SetActive / UIManager.PushUI)
- 모든 Manager는 `UnitySingleton<T>` 상속 (Awake/OnDestroy는 베이스가 private으로 가짐 → 파생 클래스에 정의 금지, 초기화는 `InitSingleton()`)
- UI 오브젝트는 **에디터에서만 생성** (런타임 Instantiate 금지) — 셋업 툴이 프리팹을 만든다
- BaseUI 생명주기: EnterWindow → StartWindow → ExitWindow → RestoreWindow
- 네임스페이스: `Seunghak`, `Seunghak.Manager`, `Seunghak.Game.MillionDeathSquare`, `Seunghak.UIManager`, `Seunghak.Platform`, `Seunghak.Network` 등

---

## 에이전트 팀 구조

이 프로젝트는 `.claude/commands/`에 정의된 에이전트 팀으로 개발합니다.

### 리더십
| 역할 | 커맨드 | 모델 | 담당 |
|------|--------|------|------|
| **PM** | `/pm` | opus | 프로젝트 총괄, 팀 조율, 로드맵, 스프린트 |
| **CTO** | `/cto` | opus | 기술 아키텍처, 코드 리뷰, 인터페이스 설계 |

### 기획팀
| 역할 | 커맨드 | 모델 | 담당 |
|------|--------|------|------|
| 시스템 기획 | `/design-system` | sonnet | 핵심 게임 메카닉, 게임 루프, 규칙 |
| 레벨 기획 | `/design-level` | sonnet | 맵 구성, 행 배치, 난이도 커브 |
| 콘텐츠 기획 | `/design-content` | sonnet | 캐릭터, 아이템, 이벤트, 업데이트 |
| 경제/밸런스 기획 | `/design-economy` | sonnet | 재화 설계, 수치 밸런싱, 수익화 |
| UI/UX 기획 | `/design-ux` | sonnet | 화면 플로우, 와이어프레임, UX 명세 |
| 라이브 서비스 기획 | `/design-live` | sonnet | 시즌 이벤트, A/B 테스트, 운영 기획 |

### 클라이언트 개발팀
| 역할 | 커맨드 | 모델 | 담당 |
|------|--------|------|------|
| UI 코어 개발 | `/dev-ui-core` | sonnet | BaseUI 프레임워크, UIManager, Canvas |
| UI 게임 개발 | `/dev-ui-game` | sonnet | Window/Popup 구현, SceneController 연동 |
| 로직 코어 개발 | `/dev-logic-core` | sonnet | GameManager, Bootstrap, MapGenerator, 상태 머신 |
| 게임플레이 개발 | `/dev-logic-gameplay` | sonnet | PlayerController, 장애물, 충돌, ObjectPool |
| 그래픽스 개발 | `/dev-graphics` | sonnet | 셰이더, 파티클, VFX, 렌더링 최적화 |
| 사운드 개발 | `/dev-sound` | sonnet | 오디오 시스템, BGM/SFX, WebGL 오디오 |
| 플랫폼 개발 | `/dev-platform` | sonnet | Steamworks(창작마당·순위표·로비), Toss(모바일판), Addressables, 입력 추상화 |
| 네트워크 개발 | `/dev-network` | sonnet | 멀티방(방장 중계·위치 동기화), 리더보드, 오프라인 대응 |
| 에디터 툴 개발 | `/dev-tools` | sonnet | 에디터 툴, 빌드 스크립트, 자동화 |
| 성능 분석 | `/profiler` | sonnet | GC/메모리/렌더링 병목 진단 |

### QA팀
| 역할 | 커맨드 | 모델 | 담당 |
|------|--------|------|------|
| QA 엔지니어 | `/qa-engineer` | sonnet | 테스트 계획, 수동 테스트, 버그 리포트, 출시 게이트 |
| QA 자동화 | `/qa-automation` | sonnet | Unity Test Framework, CI 연동, 회귀 테스트 |

### 마케팅팀
| 역할 | 커맨드 | 모델 | 담당 |
|------|--------|------|------|
| UA 마케터 | `/marketing-ua` | sonnet | 광고 소재, 채널 운영, CPI/ROAS 최적화 |
| ASO 전문가 | `/marketing-aso` | sonnet | 앱스토어 최적화, 키워드, 스크린샷 |
| 콘텐츠 마케터 | `/marketing-content` | sonnet | SNS 콘텐츠, 커뮤니티, 바이럴 캠페인 |
| 데이터 분석가 | `/marketing-analytics` | sonnet | KPI 대시보드, 퍼널/코호트, A/B 분석 |

### DevOps
| 역할 | 커맨드 | 모델 | 담당 |
|------|--------|------|------|
| DevOps / CI·CD | `/devops` | sonnet | 빌드 자동화, GitHub Actions, 배포 파이프라인 |

---

## 팀 커뮤니케이션
- `.claude/team/board.md` — 공유 작업 보드 (각 에이전트가 읽기/쓰기)
- `.claude/team/contracts.md` — 팀 간 인터페이스 계약서
- 각 에이전트는 작업 전 board를 읽고, 작업 후 업데이트 필수

---

## 개발 원칙
- **PC(Steam) 우선**, 모바일(토스) 공유를 위해 코어는 엔진·플랫폼 독립. 맵 데이터 형식(공유 코드)은 두 판 공통
- 조작: 방향키·WASD, 게임패드(레거시 Input 축), R = 처음부터. 에디터는 마우스 클릭·드래그, 숫자 키 도구 전환, Ctrl+Z/Ctrl+Y
- 결정성: 코어에 `float`/`double`/`DateTime`/`Random` 금지 (난수는 시드 기반 정수 RNG만). 렌더링 보간은 Unity 계층에서만
- GC.Alloc 최소화: 시뮬레이션 `Step()`과 뷰 갱신 핫패스에서 `new` 금지, 이벤트는 미리 할당한 버퍼 사용
- 유저 맵 관리 부담 최소화: 맵 이름 금지어 필터(`MapNameFilter`), 후기는 정해진 문구만, 1단계는 맵 코드 공유만
- 기록(최소 데스·최단 시간)은 **플랫폼별로 분리** 집계, 업로드 전 입력 기록 재생 검증

## 성능 목표
| 항목 | 목표 |
|------|------|
| FPS | 60fps |
| GC.Alloc (게임플레이) | 0 B/frame |
| Draw Call | ≤ 50 |
| 메모리 | ≤ 256 MB (모바일판 기준) |
| 초기 로드 시간 | ≤ 5초 |

---

## 팀워크 모드 (활성화)
- 복잡한 구현 요청 → 기본적으로 `/team` 워크플로 사용
- 팀 파이프라인: `team-plan → team-prd → team-exec → team-verify → team-fix`
- 멀티파일 변경, 리팩터링, 새 기능 추가는 반드시 팀 에이전트 위임
- `/pm` (opus) 이 전체 조율, `/cto` (opus) 가 아키텍처 결정 및 코드 리뷰 담당

## 딥리뷰 모드 (활성화)
- **모든 요청에서 모호성 제거 우선**: 요청이 불명확하면 즉시 `analyst` 에이전트로 요구사항 명확화
- 구현 전 체크리스트:
  1. 요청 범위가 명확한가? (어느 파일, 어느 함수, 어떤 동작)
  2. 아키텍처 영향이 있는가? (있으면 `/cto` 검수)
  3. 기존 계약(contracts.md)과 충돌하는가?
- 불명확한 요청 예시: "UI 만들어줘", "버그 고쳐줘", "최적화해줘" → 반드시 구체적 질문 후 진행
- `critic` 에이전트를 통해 계획 검토 후 실행 (`--consensus` 플래그 권장)

## 의사결정 흐름
```
기능 요청
  → pm: 범위/우선순위 결정
  → 기획팀: 명세 작성
  → cto: 기술 타당성 확인
  → 개발팀: 구현
  → qa-engineer: 검증
  → devops: 배포
  → marketing-analytics: 효과 측정
```
