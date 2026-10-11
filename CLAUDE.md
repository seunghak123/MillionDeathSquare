# Fossil Merge (가제) - Unity Project

## 프로젝트 개요
- **게임**: Fossil Merge (가제) — 둥근 발굴대 위에 화석을 떨어뜨려 같은 화석끼리 합체, 11단계 공룡 골격을 완성하는 3D 합체 퍼즐
- **기획서**: `Docs/Fossil_Merge_기획서.md` (기획: 민수님) — 규칙/수치는 이 문서가 기준
- **이름 규칙**: 플레이어에게 보이는 이름, **저장 키·분석 이벤트 이름·번들 ID·씬·에셋·씬 오브젝트 이름**에는 "수박"/"Suika"를 쓰지 않는다 (출시 후 바꾸면 기존 유저 데이터·분석 호환이 깨짐). 새 키/이벤트/에셋은 `Fossil` 접두사 사용
- **코드 이름**: 클래스·네임스페이스는 아직 초기 프로토타입 이름(`Suika*`, `Seunghak.Game.Suika3D`) — 출시 전 정리 작업에서 변경 예정 (board.md 참고)
- **엔진**: Unity (단일 씬 아키텍처)
- **주 플랫폼**: WebGL (Toss 미니게임, Facebook Instant Games, GD/CG/itch.io/Newgrounds) — 웹 환경 최우선 대응
- **서브 플랫폼**: Android, iOS
- **비동기**: UniTask (Cysharp.Threading.Tasks)
- **에셋**: Addressables + AssetBundle

## 아키텍처 규칙
- 단일 씬(`Base.unity`)에서 뷰 전환 (SetActive / UIManager.PushUI)
- 모든 Manager는 `UnitySingleton<T>` 상속
- UI 오브젝트는 **에디터에서만 생성** (런타임 Instantiate 금지)
- BaseUI 생명주기: EnterWindow → StartWindow → ExitWindow → RestoreWindow
- 네임스페이스: `Seunghak`, `Seunghak.Manager`, `Seunghak.Game.Suika3D`, `Seunghak.UI`, `Seunghak.Platform`, `Seunghak.Network` 등

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
| 플랫폼 개발 | `/dev-platform` | sonnet | WebGL, Toss/FB SDK, Addressables, 광고, 입력 추상화 |
| 네트워크 개발 | `/dev-network` | sonnet | API 통신, 리더보드, 오프라인 대응 |
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

## WebGL 우선 개발 원칙
- 모든 코드는 WebGL에서 동작 검증 우선
- `System.IO`, 멀티스레딩, Native 플러그인은 WebGL 제약 고려
- GC.Alloc 최소화: 핫패스에서 `new` 금지, 캐싱/풀링 활용
- 모바일 터치 우선 조작: 탭 = 그 위치에 낙하, 좌우 드래그 = 회전 (`SuikaDropper` → `SuikaCameraRig`), 두 손가락 회전, 웹은 마우스·키보드 보조
- 지각 변동 모드 기울기 입력은 `SuikaTiltInput`만 경유 (WebGL deviceorientation jslib / 네이티브 자이로 / 에디터 I·J·K·L)
- 게임 중에는 광고 금지 — 광고는 `SuikaPlatformBridge`의 규칙(보상형/3판마다 전면/결과 화면 배너)으로만
- 햅틱은 `SuikaHaptics`를 통해서만 호출 (이벤트별 세기·간격 제어)

## 성능 목표
| 항목 | 목표 |
|------|------|
| FPS | 60fps |
| GC.Alloc (게임플레이) | 0 B/frame |
| Draw Call | ≤ 50 |
| 메모리 (WebGL) | ≤ 256 MB |
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
