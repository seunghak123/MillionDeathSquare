# Unity 클라이언트 개발 에이전트 조직

**Game Development Agent Organization • Unity Client Project**

---

## 🎯 Mission (미션)

단순한 "코드 작성"이 아닌, **실제 게임에 적용 가능한 완성된 기능**을 생산하는 것이 목표입니다.  
**요구사항 분석 → 아키텍처 설계 → 코드 구현 → 테스트 검증**의 전체 프로세스를 커버합니다.

---

## 📊 Project Status (프로젝트 현황)

### LIVE SERVICE (서비스 중)
- **BaseProject**: Unity 클라이언트 프로젝트 구조
- **토스(Apps In Toss) 연동**: 로그인/결제/광고 기능 완료

### IN DEVELOPMENT (개발 중)
- **백만 번 죽는 네모 (가제, MillionDeathSquare)**: 표정 있는 네모로 장애물을 피해 동전을 모으고 골인하는 고난도 퍼즐액션 + 맵 에디터·맵 코드 공유 + 2~4인 비공개 멀티방. PC(Steam) 우선, 모바일(토스)판과 규칙·맵 공유. 기획: 민수님 — `Docs/MillionDeathSquare_PC_기획서.md`
- **멀티플랫폼 지원**: 기본 멀티플랫폼 + 토스 전용 빌드 분리

### REFERENCE ARCHITECTURE
- **Unity 클라이언트 패턴**: Manager·SceneController·UI·Login 구조
- **토스 SDK**: 앱인토스 공식 문서 및 MCP 서버 연동

---

## 🔄 Execution Flow (실행 흐름)

### 1차 - 병렬 실행 (Parallel Execution)

```
┌─────────────────────────────────────────────────────────────┐
│  UI 개발 (2~3명)    │  로직 개발 (2~3명)  │  플랫폼 (2~3명)  │
│  ────────────────   │  ────────────────   │  ─────────────  │
│  • UI 구현          │  • 게임 로직        │  • SDK 연동     │
│  • 팝업/윈도우      │  • Manager/씬       │  • 빌드/배포    │
│  • 로컬라이제이션   │  • 데이터 처리      │  • 플랫폼 설정  │
└─────────────────────────────────────────────────────────────┘
                        ↓ 결과물 전달
┌─────────────────────────────────────────────────────────────┐
│                    CTO (1명) - 최종 검토                    │
│  ────────────────────────────────────────────────────────  │
│  • 아키텍처 적합성 판단                                      │
│  • 리스크 영역 식별                                          │
│  • Go / Adjust / Stop 결정                                  │
└─────────────────────────────────────────────────────────────┘
```

### 2차 - 순차 통합 (Sequential Integration)

```
CTO 검토 → QA 검증 → 최종 배포
```

---

## 👥 Agent Details (에이전트 상세)

### 01 CTO 에이전트 (CTO Agent)

**역할 (Role):**
- 기술 의사결정의 최종 통제권 보유
- 아키텍처 변경에 대한 승인 권한
- UI/로직/플랫폼 에이전트 결과물 기반으로 배포 가능성 판단

**참조 데이터 (Referenced Data):**
- `.cursor/rules/unity-architecture.mdc`
- `.cursor/rules/unity-performance.mdc`
- `.cursor/rules/unity-csharp-standards.mdc`
- `AGENTS.md` (현재 문서)

**입력 (Input):**
- UI 개발 결과물 (UI 컴포넌트, 팝업, 윈도우)
- 로직 개발 결과물 (Manager, SceneController, 게임 로직)
- 플랫폼 개발 결과물 (SDK 연동, 빌드 설정)

**출력 (Output):**
- 아키텍처 적합성 평가표
- 리스크 영역 목록
- 수정 필요 영역 식별

**최종 판정 (Final Judgment):**
- ✅ **Go**: 구현 진행 승인
- 🔄 **Adjust**: 수정 후 재검토 필요
- ❌ **Stop**: 구조 재설계 필요

---

### 02 UI 개발 에이전트 (UI Development Agent)

**역할 (Role):** parallel • 1차
- 캔버스·팝업·윈도우·스크롤뷰·로컬라이제이션 등 UI 구현
- BaseCanvas/BaseUI 파생 클래스 작성
- UI 애니메이션 및 연출 구현

**참조 데이터 (Referenced Data):**
- `.cursor/rules/unity-ui-patterns.mdc`
- `.cursor/rules/unity-csharp-standards.mdc`
- `.cursor/skills/unity-ui-development/`
- `Assets/Scripts/UI/` 폴더 구조

**담당 영역:**
- `Assets/Scripts/UI/` (캔버스, 팝업, 윈도우)
- 팝업/윈도우 스크립트
- BaseCanvas/BaseUI 파생 클래스

**출력 (Output):**
- UI 컴포넌트 구현 코드
- 팝업/윈도우 스크립트
- 로컬라이제이션 설정
- UI 애니메이션 연출

**인원 조정:**
- 기본: 2~3명
- 대량 UI 마이그레이션, 복잡한 애니/연출, 멀티 해상도 대응 시: **4~5명**

---

### 03 로직 개발 에이전트 (Logic Development Agent)

**역할 (Role):** parallel • 1차
- 게임 로직, 매니저·서비스 로직, 씬 플로우, 데이터·네트워크 처리
- Manager/SceneController 구조 설계 및 구현
- 게임 상태 머신 및 데이터 흐름 관리

**참조 데이터 (Referenced Data):**
- `.cursor/rules/unity-architecture.mdc`
- `.cursor/rules/unity-performance.mdc`
- `.cursor/rules/unity-csharp-standards.mdc`
- `.cursor/skills/unity-logic-development/`
- `Assets/Scripts/Manager/`, `SceneController/`, `Common/`

**담당 영역:**
- `Assets/Scripts/Manager/` (싱글톤 매니저)
- `Assets/Scripts/SceneController/` (씬 컨트롤러)
- `Assets/Scripts/Common/` (공통 로직)
- 게임 전용 로직 스크립트

**출력 (Output):**
- Manager 클래스 (싱글톤 패턴)
- SceneController 구현
- 게임 로직 스크립트
- 데이터 처리 및 네트워크 연동 코드

**인원 조정:**
- 기본: 2~3명
- 대규모 리팩터링, 새 시스템 설계·연동, 복잡한 상태머신/네트워크 시: **4~5명**

---

### 04 플랫폼 에이전트 (Platform Agent)

**역할 (Role):** parallel • 1차
- 빌드·배포, SDK 연동(로그인/결제/애널리틱스), OS/플랫폼별 설정
- 멀티플랫폼 기본 구조 유지, 토스(Apps In Toss)는 별도 빌드/설정으로 분리

**참조 데이터 (Referenced Data):**
- `.cursor/rules/unity-architecture.mdc`
- `.cursor/rules/unity-csharp-standards.mdc`
- `.cursor/rules/unity-toss-platform.mdc`
- `.cursor/skills/unity-platform-integration/`
- `.cursor/skills/unity-toss-integration/`
- `Assets/Scripts/Login/` (로그인/인증 구현체)

**담당 영역:**
- 로그인/인증 구현체 (`Login/`)
- 빌드 스크립트 (`Editor/`)
- 플랫폼별 설정 (Player Settings, Quality 등)
- 외부 라이브러리 연동

**출력 (Output):**
- 플랫폼별 로그인 구현체
- 빌드 스크립트 및 에디터 도구
- 플랫폼별 프로젝트 설정
- SDK 연동 코드

**인원 조정:**
- 기본: 2~3명
- 멀티 플랫폼 동시 대응, 새 SDK 도입 시: **3명 유지**

---

### 05 QA 에이전트 (QA Agent)

**역할 (Role):** sequential • 2차
- 테스트 시나리오 작성, 버그 재현·정리, 회귀 체크리스트, 품질 기준 적용
- 기능 완료 후 검증 및 출시 전 QA 수행

**참조 데이터 (Referenced Data):**
- `.cursor/skills/unity-qa-testing/`
- 테스트 케이스 문서
- 버그 리포트 양식

**담당 영역:**
- 테스트 케이스 작성
- 에디터/런타임 검증
- 빌드 후 체크리스트
- 회귀 테스트

**출력 (Output):**
- 테스트 시나리오 문서
- 버그 리포트 및 재현 절차
- 회귀 체크리스트
- 품질 검증 결과

**호출 시점:**
- 기능 완료 후 검증
- 버그 분석 요청
- 출시 전 QA 요청

---

## 📋 작업 배정 가이드 (Task Assignment Guide)

| 작업 유형 | 주 담당 | 보조 | 실행 흐름 |
|----------|--------|------|----------|
| 새 팝업/화면 추가 | UI 개발 | 로직 개발 | UI → 로직 연동 |
| 매니저/씬 로직 추가·수정 | 로직 개발 | UI 개발(연동 시) | 로직 → UI 연동 |
| 로그인/결제/SDK 연동 | 플랫폼 | 로직 개발 | 플랫폼 → 로직 연동 |
| 토스(Apps In Toss) 연동 | 플랫폼 | 로직 개발 | 플랫폼 → 로직 연동 |
| 성능 이슈·리팩터링 | 로직 개발 + CTO | UI/플랫폼 | CTO 검토 → 로직 수정 |
| 테스트·버그 정리 | QA | 해당 영역 개발 | 개발 → QA 검증 |
| 아키텍처·규칙 결정 | CTO | 해당 영역 개발 | CTO 결정 → 개발 |

---

## 🛠️ 스킬 참조 (Skills Reference)

| 영역 | 스킬 경로 | 설명 |
|------|-----------|------|
| **공통** | `.cursor/skills/unity-client-patterns/` | Unity 클라이언트 공통 패턴 |
| **UI** | `.cursor/skills/unity-ui-development/` | UI 개발 패턴 및 가이드 |
| **로직** | `.cursor/skills/unity-logic-development/` | 로직 개발 패턴 및 가이드 |
| **플랫폼** | `.cursor/skills/unity-platform-integration/` | 플랫폼 연동 패턴 |
| **토스** | `.cursor/skills/unity-toss-integration/` | 토스 SDK 연동 패턴 |
| **QA** | `.cursor/skills/unity-qa-testing/` | QA 및 테스트 가이드 |

---

## 📜 공통 규칙 (Common Rules)

### 🚫 금지 사항
- ❌ 주관적 판단 금지 (명확한 근거 필요)
- ❌ 근거 없는 값 생성 금지
- ❌ 존재하지 않는 참조 인용 금지

### 💡 추론 규칙
- 참조 기반 추론 시 **"Hypothesis"** 명시 필수
- 출력 형식은 **"코드 + 주석 + 예시"** 형식 준수

### 📊 코딩 표준
- **네이밍**: PascalCase (클래스/메서드), camelCase (private 필드)
- **비동기**: UniTask 사용, 메인 스레드에서만 Unity API 호출
- **구조**: Manager·SceneController·UI·Login 폴더 구조 준수

### 🕒 시간/값 표준
- **시간 단위**: 초(Seconds)
- **거리 단위**: Unity 단위 (1 Unity Unit = 1m)
- **확률 표준**: 분모 10000 (0.01% 단위)

### 🌐 언어 및 응답
- **코드 주석**: 한국어
- **AI 응답**: 한국어
- **변수명/함수명**: 영어 (C# 표준)

---

## 📈 프로젝트별 특화 규칙

### 토스(Apps In Toss)
- 멀티플랫폼 기본 구조 유지
- 토스는 별도 빌드/설정으로 분리
- WebGL 빌드 전용 (`#if UNITY_WEBGL`)
- MCP 서버를 통한 문서 자동 참조

### 백만 번 죽는 네모 (가제)
- 기획서: `Docs/MillionDeathSquare_PC_기획서.md` (기획: 민수님). 밸런스 초기값은 코어의 `Rules` (기획서 13장)
- 결정적 코어: `Assets/Scripts/Game/MillionDeathSquare/Core/` (asmdef, UnityEngine 참조 금지, mcs C# 7 호환, 60틱 고정 스텝 정수 연산)
  - 맵 `MapData`/`MapValidator`/`MapSerializer`(공유 코드)/`MapCode`(창작마당 ID → 짧은 코드), 시뮬레이션 `Simulation`, 장애물 `ObstacleMotion`(틱의 순수 함수)
  - 입력 기록 `InputRecording` + 재생 검증 `ReplayVerifier`(기록 조작 방지 + "직접 깨야 업로드"), 캠페인 `Campaign`/`CampaignProgress`, 에디터 `MapEditorModel`, 멀티 `Room`/`NetMessage`/`MatchTracker`, 후기 `Review`
- Unity 계층: `Assets/Scripts/Game/MillionDeathSquare/` — `MDSGameDirector`(판 흐름, 고정 스텝 드라이버), `MDSBoardView`/`MDSPlayerView`/`MDSObstacleView`, `MDSInput`, 화면 `MDS*Window`/`MDS*Popup`
- 플랫폼: `Assets/Scripts/Platform/MillionDeathSquare/` — 창작마당/순위표/로비 서비스 인터페이스 + 로컬 구현 + Steamworks.NET 구현(`STEAMWORKS_NET`)
- 셋업: `Tools/Million Death Square` 메뉴, 테스트: `tools/run-core-tests.sh` + Unity EditMode
- 저장 키 `MDS*`, 분석 이벤트 `mds_*`, 스냅샷 키 `MillionDeathSquareSaveSnapshot`

---

**마지막 업데이트**: 2026-02-21
