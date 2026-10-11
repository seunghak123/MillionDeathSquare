# BaseProject Discord Agent Bot

디스코드 채널에서 명령을 보내면, 로컬 PC의 `claude` CLI가 이 프로젝트(`BaseProject`)에 대해
격리된 git worktree에서 작업을 수행하고, 완료되면 **자동으로 `main` 브랜치에 병합 + push**한
뒤 오너(본인)에게 DM으로 결과를 보고하는 릴레이 봇입니다.

## 동작 흐름

1. 지정된 서버(`GUILD_ID`)의 지정된 채널(`COMMAND_CHANNEL_ID`)에서 `!ai <할일>` 또는 `!수민 <할일>` 형태로 메시지 전송
2. 1차 필터(`guard.js`)가 개인정보/파일유출/봇 설정변경/유해 요청 여부를 검사 → 걸리면 즉시 차단
3. 통과하면 `bot/task-<id>` 브랜치 + 별도 worktree 생성, 채널에 "🚀 일 시작할게요!" 남김
4. 그 worktree 안에서 `claude -p` 를 **Bash 도구 포함 + `--permission-mode bypassPermissions`** 로
   헤드리스 실행함 (⚠️ 승인 절차 없이 PC에서 임의의 셸 명령이 바로 실행됨, 아래 안전장치 요약 참고).
   진행 중에는 "GameStartWindow.cs 수정 중이에요..." 처럼 채널 메시지가 실시간으로 갱신됨
5. 변경사항이 있으면 **바로 `main`에 자동 병합 + push** (승인 대기 없음), 오너에게는 완료 후 DM으로
   요약 + `git diff --stat` 보고만 감 (승인 요청 아님, 참고용)
6. 변경사항이 없으면 worktree/브랜치만 정리하고 종료
7. 봇 상태 메시지에 **답장**하면 같은 대화를 이어서 작업. 작업이 아직 진행 중이어도 답장으로 추가 요청을
   보낼 수 있으며(📝 접수), 현재 작업이 끝나는 즉시 같은 세션에서 자동으로 이어서 처리됨
   (여러 건 쌓이면 번호 매겨 한 번에 전달). 예전 상태 메시지에 답장해도 가장 최신 이어하기 작업으로 연결됨

## 1. 디스코드 봇 만들기

1. https://discord.com/developers/applications → **New Application**
2. 좌측 **Bot** 탭 → **Reset Token**으로 토큰 발급 → `.env`의 `DISCORD_TOKEN`에 저장
3. 같은 Bot 탭에서 **Privileged Gateway Intents** 중 **MESSAGE CONTENT INTENT** 켜기
4. 좌측 **OAuth2 → URL Generator**
   - SCOPES: `bot`
   - BOT PERMISSIONS: `Send Messages`, `Read Message History`, `Add Reactions`, `View Channel`
   - 생성된 URL로 접속해서 원하는 서버에 봇 초대
5. 디스코드 앱에서 **설정 → 고급 → 개발자 모드** 켜기
   - 명령을 받을 서버 우클릭 → **ID 복사** → `GUILD_ID`
   - 명령을 받을 채널 우클릭 → **ID 복사** → `COMMAND_CHANNEL_ID` (여러 채널이면 쉼표로 이어 붙임)
   - 본인 프로필 우클릭 → **ID 복사** → `OWNER_ID`

## 2. 설정

```
cd discord-bot
copy .env.example .env
```

`.env`를 열어 위에서 얻은 값들과 `PROJECT_ROOT`, `BASE_BRANCH` 등을 채웁니다.

## 3. 설치 및 수동 실행

```
cd discord-bot
npm install
npm start
```

## 4. Windows 부팅/로그인 시 자동 시작 등록

봇이 PC를 껐다 켜도 자동으로 다시 뜨게 하려면, **일반 PowerShell(관리자 아니어도 됨)** 창을 열고
아래 명령을 한 번 실행하세요. (자동화 셸에서는 권한 문제로 등록이 막혀서 직접 실행이 필요합니다.)

```powershell
schtasks /create /tn "BaseProjectDiscordAgentBot" /tr "wscript.exe C:\Users\dhtmd\Desktop\BaseProject\discord-bot\run-hidden.vbs" /sc onlogon /rl limited /f
```

- 로그인할 때마다 창 없이 `node index.js`가 백그라운드로 실행됩니다.
- 로그는 `discord-bot/bot.log`에 쌓입니다.
- 삭제(등록 취소)하려면:
  ```powershell
  schtasks /delete /tn "BaseProjectDiscordAgentBot" /f
  ```
- 지금 바로 켜보려면:
  ```powershell
  schtasks /run /tn "BaseProjectDiscordAgentBot"
  ```

## 안전장치 요약

| 항목 | 방식 |
|---|---|
| **임의 명령 실행 (⚠️ 차단 안 됨)** | **2026-09-19 오너 요청으로 Bash 도구 + `--permission-mode bypassPermissions` 활성화됨. `COMMAND_CHANNEL_ID` 채널에서 누구든 메시지를 보내면 승인 절차 없이 이 PC에서 임의의 셸 명령이 그대로 실행됨.** 남은 방어선은 `guard.js` 키워드 필터 + 시스템 프롬프트 규칙뿐이며 완벽한 보장이 아님 |
| 프로젝트 밖 파일 접근 | **제한 없음.** `--restricted`/`--add-dir` 제거됨 (PC 전체 접근을 오너가 명시적으로 선택함). Bash로 worktree 밖 어디든 접근 가능 |
| 개인정보/파일 유출 요청 | `guard.js` 키워드 필터 + 시스템 프롬프트 규칙 2중 차단 (완벽 보장은 아님, Bash 활성화로 우회 가능성 더 높아짐) |
| 봇 자신 설정 변경 | 키워드 필터로 차단, 필요 시 본인이 직접 코드 수정 |
| 실수/악의적 push | 모든 변경은 격리된 worktree에서 먼저 만들어짐. **현재 설정은 승인 없이 자동 병합/push** (오너 요청으로 변경됨) — 사후에 DM으로 결과만 보고받음 |
| 비용 폭주 방지 | `CLAUDE_MAX_BUDGET_USD`, `CLAUDE_TIMEOUT_MINUTES` 로 1회 실행당 상한 |

**한계**: `guard.js`의 키워드 필터와 시스템 프롬프트 규칙은 완벽한 보안 경계가 아니라
"최선 노력" 방어입니다. Bash가 승인 없이 PC 전체에 열려 있으므로, `COMMAND_CHANNEL_ID`
채널에 접근 가능한 사람 전부를 신뢰할 수 있는 경우에만 이 상태로 운영하세요. 정말 강한
격리가 필요하면 이 봇 자체를 별도 제한된 OS 계정이나 컨테이너 안에서 실행하는 것을
고려하세요.

## 여러 프로젝트 관리

BaseProject 외의 프로젝트도 같은 방식(task별 worktree → `main` 자동 병합 + push)으로 작업할 수 있습니다.
관리 명령은 claude를 거치지 않고 봇이 직접 처리합니다 (`projectCommands.js`, `projects.js`).

| 메시지 예시 | 동작 |
|-------------|------|
| `고수야 프로젝트 목록` | 관리 중인 프로젝트 목록 |
| `고수야 새 프로젝트 퍼즐 https://github.com/<계정>/<레포>.git` | `C:\Users\dhtmd\Projects\퍼즐`에 clone 후 등록. 빈 레포면 README 초기 커밋을 만들어 `main`으로 push |
| `고수야 프로젝트 등록 퍼즐 C:\경로\Puzzle` | 이미 PC에 있는 git 저장소 등록 (origin 원격 + 원격 기본 브랜치 필요) |
| `고수야 새 프로젝트 퍼즐 <URL> 3매치 퍼즐 모바일 게임` | URL 뒤에 설명을 붙이면 자동 배정에 사용 |
| `고수야 프로젝트 설명 퍼즐 3매치 퍼즐, 보석/콤보/하트` | 자동 배정용 설명 저장 (없으면 CLAUDE.md/README 앞부분 사용) |
| `고수야 프로젝트 별칭 퍼즐 매치3,보석게임` | 메시지에 이 단어가 나오면 바로 그 프로젝트 |
| `고수야 버튼 추가해줘` | **프로젝트 자동 배정** (아래 참고) |
| `고수야 [퍼즐] 버튼 추가해줘` | 해당 프로젝트로 강제 지정 |

**자동 배정 순서** (`router.js`): `[이름]` 지정 → 메시지에 이름/별칭이 하나만 나옴 → haiku가 설명 +
직전 작업 프로젝트를 보고 판단 (호출당 약 $0.001, 5초 정도) → 실패하면 직전 프로젝트 → 기본 프로젝트.
프로젝트가 BaseProject 하나뿐이면 판단 없이 바로 BaseProject. 상태 메시지에 `[프로젝트명] · 자동 판단`처럼
어떤 프로젝트로 갔는지 표시되니, 틀렸으면 `[이름] <할일>`로 다시 시키면 됩니다.

- 등록 정보는 `discord-bot/projects.json`에 저장됩니다 (PC 로컬 설정이라 gitignore).
- 새 프로젝트 생성 위치는 `.env`의 `NEW_PROJECTS_ROOT`로 바꿀 수 있습니다 (기본 `C:\Users\dhtmd\Projects`).
- 원격 레포는 직접 만들어서 URL을 주면 됩니다. push는 이 PC의 git 자격증명을 그대로 사용합니다.
- 각 프로젝트의 worktree는 `<프로젝트폴더>-bot-worktrees`, 병합용은 `<프로젝트폴더>-bot-integration` (프로젝트 폴더의 형제 디렉토리).
- 상태 메시지에 답장해서 이어가면 원래 task의 프로젝트에서 계속 작업합니다.
- 토스 배포 명령은 BaseProject 전용입니다.

## 토스 배포 (전용 핸들러)

채널(또는 `!ai ` / "고수야 " 접두사 뒤)에서 아래처럼 말하면 claude를 거치지 않고 `ci/toss/deploy-toss.ps1`을 직접 실행합니다.

| 메시지 예시 | 실행 |
|-------------|------|
| `토스 배포해줘` | dev 빌드·배포 → 성공하면 prod 빌드·배포 (`-Env all`) |
| `토스 dev로 배포해줘` | dev만 (테스트 광고 / 테스트 프로모션) |
| `토스 prod 배포` | prod만 (실제 광고 / 실제 프로모션) |

- 오너(`OWNER_ID`)만 실행할 수 있습니다.
- 한 번에 하나만 실행합니다. 이미 배포 중이면 거부 메시지가 나옵니다.
- 진행 단계를 상태 메시지로 갱신하고, 끝나면 성공(요약 + 배포 URL) / 실패(로그 끝부분)를 채널에 알립니다.
- 타임아웃: 기본 90분 (`TOSS_DEPLOY_TIMEOUT_MINUTES`로 변경). 초과 시 프로세스 트리를 종료합니다.
- 전체 로그는 `ci/toss/logs/`에 남습니다 (배포 키는 마스킹/미기록).
- Unity Editor가 이 프로젝트를 열고 있으면 배치모드 빌드가 불가하므로 스크립트가 중단하고 이유를 알려줍니다. 에디터를 닫고 다시 요청하세요.
- 봇 코드 변경(`tossDeploy.js`, `index.js`)은 봇 재시작 후 적용됩니다.
- 스크립트 단독 실행: `powershell -File ci\toss\deploy-toss.ps1 -Env dev -DryRun` (`-DryRun`은 실행될 명령만 출력)
