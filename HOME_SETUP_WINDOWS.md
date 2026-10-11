# Claude Code 환경 세팅 가이드 — Windows

새 머신 또는 환경 재구축 시 이 파일을 Claude에게 주고 "세팅해줘" 하면 됩니다.

---

## 현재 설치 상태 (2026-05-07 기준)

| 도구 | 버전 | 상태 |
|------|------|------|
| node | v24.13.1 | ✅ |
| python | 3.12.10 | ✅ |
| jq | 1.8.1 | ✅ |
| gh (GitHub CLI) | 설치됨 | ⚠️ 로그인 필요 |
| codex CLI | 0.128.0 | ✅ 로그인 완료 |
| claude CLI | 설치됨 | ✅ |
| coplay-mcp | 등록됨 | ✅ |

---

## 1. 시스템 도구 설치 (Windows)

```powershell
# GitHub CLI
winget install --id GitHub.cli -e

# jq
winget install jqlang.jq

# node (없으면)
winget install OpenJS.NodeJS.LTS

# python (없으면)
winget install Python.Python.3.12
```

---

## 2. Claude Code / Codex CLI 설치

```bash
npm install -g @anthropic-ai/claude-code
npm install -g @openai/codex
```

---

## 3. GitHub CLI 로그인 (PowerShell에서 실행)

```powershell
& "C:\Program Files\GitHub CLI\gh.exe" auth login
```

---

## 4. Codex 로그인

```bash
codex login
```

브라우저 인증 후 `Ctrl+C` 로 종료 (인증은 저장됨).
검증: `echo "테스트" | codex exec --skip-git-repo-check --json | tail -3`

---

## 5. coplay-mcp 등록

```bash
claude mcp add --scope user --transport stdio coplay-mcp \
  --env MCP_TOOL_TIMEOUT=720000 \
  -- uvx --python ">=3.11" coplay-mcp-server@latest
```

Unity Editor에서도 Coplay 패키지 설치 필요:
- Unity Package Manager → "Add package from git URL"
- `https://github.com/CoplayDev/unity-plugin.git#beta`

---

## 6. 레포 클론 (gh 로그인 후)

```bash
git clone --recurse-submodules https://github.com/supergenedev/game-claudecode.git ~/Desktop/game-claudecode
cd ~/Desktop/game-claudecode
./setup.sh
```

---

## 7. 에이전트 오케스트라 구조 (현재 세팅 완료)

```
.claude/commands/          ← 26개 에이전트 커맨드
  pm.md                    ← 오케스트레이터 (PM)
  cto.md                   ← 기술 총괄
  artist.md                ← 이미지·3D 생성 에이전트
  design-*.md (6개)        ← 기획팀
  dev-*.md (10개)          ← 개발팀
  qa-*.md (2개)            ← QA팀
  marketing-*.md (4개)     ← 마케팅팀
  devops.md                ← DevOps

.claude/team/
  board.md                 ← 팀 공유 작업 보드
  contracts.md             ← 팀 간 인터페이스 계약

workspaces/
  orchestrator/CLAUDE.md  ← PM 오케스트레이터 워크스페이스
  artist/CLAUDE.md        ← 이미지 생성 오케스트라

output/games/              ← 게임별 산출물 디렉터리
```

---

## 8. 이미지 생성 오케스트라

| 순위 | 도구 | 상태 |
|------|------|------|
| 1 | coplay-mcp | ✅ 활성 (Free 티어) |
| 2 | Gemini Imagen | ⏸ 보류 — `.env`에 `GEMINI_API_KEY` 입력 시 활성 |
| 3 | OpenAI gpt-image-2 | ⏸ 보류 — `.env`에 `OPENAI_API_KEY` 입력 시 활성 |

API 키 등록 방법: `.env.example` → `.env` 복사 후 키 입력
```bash
cp .env.example .env
# 그 다음 .env 직접 편집 (git에 절대 커밋 금지)
```

---

## 9. VS Code 설정

`C:\Users\{유저}\AppData\Roaming\Code\User\settings.json` 에 추가:
```json
{
  "window.restoreWindows": "one"
}
```

---

## 10. 남은 작업

- [ ] `gh auth login` — GitHub 로그인
- [ ] `game-claudecode` 레포 클론 및 `setup.sh` 실행
- [ ] `tmux` 설치 (WSL 또는 Git Bash 대체 검토)
- [ ] `.env` 파일 생성 및 API 키 입력 (필요 시)
