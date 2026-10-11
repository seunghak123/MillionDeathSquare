const spawn = require('cross-spawn');
const fs = require('fs');
const os = require('os');
const path = require('path');
const config = require('./config');
const { SYSTEM_PROMPT_ADDENDUM } = require('./guard');

const CLAUDE_CONFIG_PATH = path.join(os.homedir(), '.claude.json');

// 새로 만든 task worktree는 claude 입장에서 "본 적 없는 워크스페이스"라서 기본적으로
// 신뢰되지 않고, 그러면 .mcp.json에 정의된 MCP 서버(mcp-unity 등)가 아예 안 뜬다.
// 이걸 미리 신뢰 처리해둔다. 여러 task가 동시에 이 파일을 쓰면 깨질 수 있어 큐로 직렬화한다.
let trustQueue = Promise.resolve();
function ensureWorkspaceTrusted(worktreePath) {
  trustQueue = trustQueue.then(() => doEnsureWorkspaceTrusted(worktreePath), () => doEnsureWorkspaceTrusted(worktreePath));
  return trustQueue;
}
async function doEnsureWorkspaceTrusted(worktreePath) {
  try {
    const raw = fs.readFileSync(CLAUDE_CONFIG_PATH, 'utf8');
    const data = JSON.parse(raw);
    data.projects = data.projects || {};
    const forward = worktreePath.replace(/\\/g, '/');
    // claude 내부적으로 드라이브 문자를 소문자로 정규화해서 키를 쓰는 걸 관찰해서, 양쪽 다 등록해둔다.
    const variants = new Set([
      forward,
      forward.replace(/^[A-Z]:/, (m) => m.toLowerCase()),
      forward.replace(/^[a-z]:/, (m) => m.toUpperCase()),
    ]);
    let changed = false;
    for (const key of variants) {
      if (!data.projects[key]) {
        data.projects[key] = { hasTrustDialogAccepted: true };
        changed = true;
      } else if (!data.projects[key].hasTrustDialogAccepted) {
        data.projects[key].hasTrustDialogAccepted = true;
        changed = true;
      }
    }
    if (changed) fs.writeFileSync(CLAUDE_CONFIG_PATH, JSON.stringify(data, null, 2));
  } catch (err) {
    console.error('[discord-bot] failed to pre-trust worktree workspace:', err.message);
  }
}

// 도구 호출 이벤트를 사람이 읽을 수 있는 진행상황 문구로 변환
function describeToolUse(block) {
  const input = block.input || {};
  const shortPath = (p) => (p ? String(p).split(/[\\/]/).pop() : null);
  switch (block.name) {
    case 'Read':
      return `👀 ${shortPath(input.file_path) || '파일'} 읽는 중`;
    case 'Edit':
      return `✏️ ${shortPath(input.file_path) || '파일'} 수정 중`;
    case 'Write':
      return `📝 ${shortPath(input.file_path) || '파일'} 작성 중`;
    case 'Grep':
      return `🔍 "${String(input.pattern || '').slice(0, 40)}" 검색 중`;
    case 'Glob':
      return `🔍 ${input.pattern || '파일 목록'} 탐색 중`;
    case 'Bash':
      return `⚡ \`${String(input.command || '').slice(0, 60)}\` 실행 중`;
    default:
      return `⚙️ ${block.name} 작업 중`;
  }
}

// 헤드리스 claude 실행 (stream-json 으로 진행상황을 실시간 파싱):
// Bash 도구 + bypassPermissions 활성화 (오너 요청, 2026-09-19): 채널의 누구든 명령을 보내면
// 승인/확인 절차 없이 이 PC에서 임의의 셸 명령이 그대로 실행된다. --restricted 를 걷어내고
// --add-dir 로 걸려있던 worktree 경로 제한도 없앴다 (PC 전체 접근 허용을 오너가 명시적으로 선택함).
// 남은 방어선은 guard.js 의 키워드 사전 필터 + SYSTEM_PROMPT_ADDENDUM 프롬프트 규칙뿐이며,
// 둘 다 "최선 노력"이지 강제 경계가 아니다. 정말 위험한 명령(예: rm -rf, 포맷, 셧다운)도
// 프롬프트에 명시적으로 안 나오면 걸러지지 않을 수 있다. README 의 안전장치 요약 표도 이에 맞춰 갱신했다.
// --permission-mode bypassPermissions : Bash 는 acceptEdits 로는 자동 승인되지 않아, 헤드리스(-p)
//                        모드에서 확인을 기다리다 멈추는 걸 피하려면 모든 도구 승인을 건너뛰어야 한다.
// --tools "...,Task" : Task(서브에이전트 위임) 허용 - 편의성 우선으로 오너가 명시적으로 선택함.
//                        executor 등 서브에이전트도 기본적으로 Bash 등 전체 도구를 쓸 수 있음.
// --tools "...,mcp__mcp-unity__*" : Unity 에디터 조작 허용 (오너 요청, 편의성 우선).
//                        주의: 이건 task별 격리 worktree가 아니라 실제 켜져있는 라이브 Unity 에디터를
//                        직접 건드림 (씬 삭제, 임의 메뉴 실행 포함). 여러 task가 동시에 에디터를
//                        건드리면 서로 충돌할 수 있고, 사람이 에디터에서 보던 화면이 예고 없이 바뀔 수 있음.
// --append-system-prompt : guard.js 의 방어 규칙 주입
// --max-budget-usd : 안전 목적. 디스코드 답장(reply)으로 같은 대화를 이어갈 수 있도록
// 세션은 디스크에 남겨두고(--no-session-persistence 제거), resumeSessionId가 있으면 --resume 으로 이어붙인다.
// onProgress(text) : 진행 중 사람이 읽을 수 있는 상태 문구를 전달하는 콜백 (선택)
async function runClaudeHeadless(promptText, worktreePath, onProgress, resumeSessionId) {
  await ensureWorkspaceTrusted(worktreePath);
  return new Promise((resolve) => {
    const args = [
      '-p', promptText,
      '--permission-mode', 'bypassPermissions',
      '--tools', 'Read,Edit,Write,Grep,Glob,Task,Bash,mcp__mcp-unity__*',
      '--append-system-prompt', SYSTEM_PROMPT_ADDENDUM,
      '--output-format', 'stream-json',
      '--verbose', // --print + --output-format=stream-json 조합에 필수
      '--max-budget-usd', String(config.claudeMaxBudgetUsd),
    ];
    if (resumeSessionId) {
      args.push('--resume', resumeSessionId);
    }

    // cross-spawn: Windows에서 claude.cmd 를 shell:true 없이도 안전하게 찾아 실행하고,
    // 인자별로 올바르게 이스케이프해서 셸 인젝션(<, >, &, | 등)을 방지한다.
    const child = spawn('claude', args, {
      cwd: worktreePath,
      windowsHide: true,
      stdio: ['ignore', 'pipe', 'pipe'], // stdin을 열어두면 claude가 입력을 기다리며 지연됨
    });

    let stdout = '';
    let stderr = '';
    let lineBuffer = '';
    let lastResultLine = null;
    let lastAssistantText = '';
    let sessionId = resumeSessionId || null;
    let timedOut = false;

    const timer = setTimeout(() => {
      timedOut = true;
      child.kill();
    }, config.claudeTimeoutMinutes * 60 * 1000);

    function handleLine(line) {
      if (!line.trim()) return;
      let evt;
      try {
        evt = JSON.parse(line);
      } catch (_) {
        return; // stream-json 이 아닌 잡음 라인은 무시
      }

      if (evt.session_id) sessionId = evt.session_id;

      if (evt.type === 'result') {
        lastResultLine = evt;
        return;
      }

      if (evt.type === 'assistant' && evt.message && Array.isArray(evt.message.content)) {
        for (const block of evt.message.content) {
          if (block.type === 'tool_use' && onProgress) {
            onProgress(describeToolUse(block));
          } else if (block.type === 'text' && block.text) {
            lastAssistantText = block.text;
          }
        }
      }
    }

    child.stdout.on('data', (d) => {
      const chunk = d.toString();
      stdout += chunk;
      lineBuffer += chunk;
      const lines = lineBuffer.split('\n');
      lineBuffer = lines.pop(); // 마지막 줄은 아직 끝나지 않았을 수 있으니 버퍼에 유지
      for (const line of lines) handleLine(line);
    });
    child.stderr.on('data', (d) => { stderr += d.toString(); });

    child.on('close', (code) => {
      clearTimeout(timer);
      if (lineBuffer.trim()) handleLine(lineBuffer);

      if (timedOut) {
        resolve({ success: false, resultText: `시간 초과 (${config.claudeTimeoutMinutes}분)로 중단되었습니다.`, raw: stderr, sessionId });
        return;
      }

      if (lastResultLine) {
        const resultText = lastResultLine.result || lastAssistantText || '(결과 텍스트 없음)';
        resolve({ success: !lastResultLine.is_error, resultText, raw: lastResultLine, sessionId });
        return;
      }

      if (code !== 0) {
        resolve({ success: false, resultText: `실행 실패 (exit ${code}): ${stderr.slice(0, 1500)}`, raw: stderr, sessionId });
        return;
      }

      resolve({ success: true, resultText: lastAssistantText || stdout.trim() || '(결과 텍스트 없음)', raw: stdout, sessionId });
    });

    child.on('error', (err) => {
      clearTimeout(timer);
      resolve({ success: false, resultText: `claude 실행 자체에 실패했습니다: ${err.message}`, raw: String(err), sessionId });
    });
  });
}

module.exports = { runClaudeHeadless };
