const { Client, GatewayIntentBits, Partials } = require('discord.js');
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const config = require('./config');
const { checkGuard } = require('./guard');
const { runClaudeHeadless } = require('./claudeRunner');
const {
  createTaskWorktree,
  commitIfChanged,
  mergeAndPush,
  syncWithLatestMain,
  hasUnmergedPaths,
  cleanupWorktree,
} = require('./gitWorktree');
const { notifyOwner } = require('./approval');
const { parseTossDeploy, handleTossDeploy } = require('./tossDeploy');
const { getUsageLine } = require('./usage');
const { parseProjectCommand, handleProjectCommand, resolveProjectFromText } = require('./projectCommands');
const { rememberProject } = require('./router');

// 여러 봇 인스턴스가 동시에 뜨면 같은 디스코드 메시지를 각자 중복 처리해서 작업이
// 그대로 여러 번 돌아버린다 (세션 재시작으로 이전 프로세스가 안 죽고 orphan으로 남는
// 경우가 반복 관찰됨). PID 락 파일로 한 번에 하나만 뜨도록 강제한다.
const LOCK_PATH = path.join(__dirname, 'bot.lock');
function acquireSingleInstanceLock() {
  if (fs.existsSync(LOCK_PATH)) {
    const oldPid = Number(fs.readFileSync(LOCK_PATH, 'utf8').trim());
    let stillAlive = false;
    if (oldPid) {
      try {
        process.kill(oldPid, 0); // 신호 0: 실제로 죽이지 않고 살아있는지만 확인
        stillAlive = true;
      } catch (_) {
        stillAlive = false; // 프로세스 없음 (죽은 락 파일)
      }
    }
    if (stillAlive) {
      console.error(`[discord-bot] 이미 다른 봇 인스턴스(pid ${oldPid})가 실행 중이라 종료합니다.`);
      process.exit(1);
    }
    console.log(`[discord-bot] 죽은 락 파일 발견 (pid ${oldPid} 없음), 정리하고 시작합니다.`);
    try { fs.unlinkSync(LOCK_PATH); } catch (_) {}
  }
  // wx: 파일이 이미 존재하면 실패하는 원자적 생성. read-then-write 사이의 경쟁 상태를 없앤다.
  try {
    fs.writeFileSync(LOCK_PATH, String(process.pid), { flag: 'wx' });
  } catch (err) {
    console.error(`[discord-bot] 락 파일 생성 실패(다른 인스턴스가 방금 선점함): ${err.message}`);
    process.exit(1);
  }
}
function releaseSingleInstanceLock() {
  try {
    if (fs.existsSync(LOCK_PATH) && Number(fs.readFileSync(LOCK_PATH, 'utf8').trim()) === process.pid) {
      fs.unlinkSync(LOCK_PATH);
    }
  } catch (_) {}
}
acquireSingleInstanceLock();
process.on('exit', releaseSingleInstanceLock);

// 어딘가에서 .catch()를 빼먹어도 봇 프로세스 전체가 죽지 않도록 하는 최종 안전망
// (예: Discord API가 "Unknown message" 등을 던졌을 때 unhandled rejection으로 process가 종료되는 것 방지)
process.on('unhandledRejection', (err) => {
  console.error('[discord-bot] unhandled rejection (ignored, process kept alive):', err && err.message ? err.message : err);
});

const client = new Client({
  intents: [
    GatewayIntentBits.Guilds,
    GatewayIntentBits.GuildMessages,
    GatewayIntentBits.MessageContent,
    GatewayIntentBits.GuildMessageReactions,
    GatewayIntentBits.DirectMessages,
    GatewayIntentBits.DirectMessageReactions,
  ],
  partials: [Partials.Channel, Partials.Message, Partials.Reaction],
});

// 탐색/수정 작업 자체는 task별로 독립된 worktree에서 동시에 돌아가도 안전하다.
// 다만 main 저장소로의 병합+push는 공유 자원(checkout)을 건드리므로 이 단계만 직렬 큐로 처리한다.
let mergeQueue = Promise.resolve();
function enqueueMerge(fn) {
  mergeQueue = mergeQueue.then(fn, fn);
  return mergeQueue;
}

// 여러 task가 동시에 같은 파일의 같은 부분을 건드리면 병합 충돌이 날 수 있다 (워크트리로 막을 수
// 없는, git 병합 자체의 정상적인 한계). 병렬 처리를 유지하면서 실패율을 낮추기 위해, 충돌이 나면
// 최신 main을 task 워크트리에 직접 받아와 충돌 마커를 만든 뒤 claude를 한 번 더 불러서 스스로
// 해결·커밋하게 하고 재병합을 시도한다. 그래도 안 되면(진짜 근본적으로 상충하는 수정 등) 포기하고
// 실패로 보고한다 - 무한 재시도로 비용이 새는 걸 막기 위해 1회로 제한.
const MAX_MERGE_CONFLICT_RETRIES = 1;
async function mergeWithConflictRetry(project, taskId, commandText, wtPath, onProgress, sessionId) {
  let currentSessionId = sessionId;
  for (let attempt = 0; ; attempt++) {
    try {
      const result = await enqueueMerge(() => mergeAndPush(project, taskId));
      return { ...result, sessionId: currentSessionId };
    } catch (err) {
      if (!err.isMergeConflict || attempt >= MAX_MERGE_CONFLICT_RETRIES) throw err;

      if (onProgress) onProgress('⚠️ 병합 충돌 발생, 최신 코드 기준으로 자동 해결 시도');
      const sync = await enqueueMerge(() => syncWithLatestMain(project, taskId));
      if (!sync.hadConflict) continue; // 그 사이 상황이 바뀌어 충돌이 사라졌을 수 있음 → 그냥 재시도

      const resolvePrompt = [
        'git 병합 충돌이 발생했습니다. 아래 파일들에 <<<<<<<, =======, >>>>>>> 충돌 마커가 남아있습니다:',
        sync.conflictFiles.join(', '),
        '',
        `원래 작업 목적("${commandText}")을 유지하면서, 최신 main 브랜치의 변경사항과 자연스럽게 합쳐지도록 충돌을 해결하세요.`,
        "해결 후 반드시 'git add -A' 로 충돌 해결된 파일을 스테이징하고 'git commit --no-edit' 으로 병합을 완료하세요.",
      ].join('\n');

      const resolveResult = await runClaudeHeadless(resolvePrompt, wtPath, onProgress, currentSessionId);
      currentSessionId = resolveResult.sessionId || currentSessionId;

      if (await hasUnmergedPaths(wtPath)) {
        const err = new Error('자동 충돌 해결에 실패했습니다 (여전히 병합되지 않은 파일이 있어요)');
        err.isMergeConflict = true;
        throw err;
      }
      // 충돌 해결 커밋이 만들어졌으니 루프 다시 돌면서 최신 main 기준으로 재병합 시도
    }
  }
}

// 봇 상태 메시지의 Discord message ID -> task 상태. 이 메시지에 답장하면 같은 세션을 이어간다.
// 프로세스 메모리에만 있으므로 봇 재시작하면 이어가기 맥락은 끊긴다 (새 작업으로 시작됨).
const taskRegistry = new Map();

// project: 작업 대상 프로젝트 ({ name, root, baseBranch }). 이어하기면 원래 task의 프로젝트를 그대로 쓴다.
async function handleCommand(message, project, commandText, resumeFrom, routeReason) {
  if (resumeFrom) project = resumeFrom.project;
  rememberProject(project);
  const routeNote = routeReason ? ` · ${routeReason}, 다른 프로젝트면 \`[이름] <할일>\`로 다시 말해줘요` : '';
  const isContinuation = !!(resumeFrom && !resumeFrom.running);
  const taskId = isContinuation && resumeFrom.wtPath
    ? resumeFrom.taskId
    : `${Date.now()}-${crypto.randomBytes(3).toString('hex')}`;

  const guardResult = checkGuard(commandText);
  if (guardResult.blocked) {
    await message.reply(`🚫 차단됨 (${guardResult.category}): ${guardResult.reason}`);
    return;
  }

  // 작업이 아직 도는 중에 답장으로 추가 요청이 오면 거절하지 않고 쌓아뒀다가, 현재 작업이 끝나는
  // 대로 같은 세션에서 이어서 처리한다 (오너 요청, 2026-10-09). 여러 개 쌓이면 한 번에 합쳐서 보낸다.
  if (resumeFrom && resumeFrom.running) {
    resumeFrom.followUps.push({ message, text: commandText });
    await message.reply(
      `📝 추가 요청 받았어요. 지금 작업 끝나면 이어서 바로 처리할게요. (대기 ${resumeFrom.followUps.length}건, task \`${resumeFrom.taskId}\`)`
    );
    return;
  }

  const statusMsg = await message.reply(
    isContinuation
      ? `🔁 [${project.name}] 이어서 할게요! (task \`${taskId}\`)`
      : `🚀 [${project.name}] 일 시작할게요! (task \`${taskId}\`)${routeNote}`
  );
  const registryEntry = {
    taskId, project, wtPath: null, sessionId: (resumeFrom && resumeFrom.sessionId) || null,
    cleaned: false, running: true, followUps: [], next: null,
  };
  // 예전 상태 메시지에 답장해도 항상 가장 최신 이어하기 작업으로 연결되도록 체인을 이어둔다.
  if (resumeFrom) resumeFrom.next = registryEntry;
  taskRegistry.set(statusMsg.id, registryEntry);

  // 진행상황 편집 스팸 방지: 마지막 편집 이후 최소 간격을 두고, 그 사이 내용은 최신 것만 반영
  let lastEditAt = 0;
  let pendingText = null;
  let editTimer = null;
  const EDIT_INTERVAL_MS = 3000;
  function flushProgress() {
    if (pendingText == null) return;
    const text = pendingText;
    pendingText = null;
    lastEditAt = Date.now();
    statusMsg.edit(`[${project.name}] ${text} 중이에요... (task \`${taskId}\`)`).catch(() => {});
  }
  function onProgress(text) {
    pendingText = text;
    const elapsed = Date.now() - lastEditAt;
    if (elapsed >= EDIT_INTERVAL_MS) {
      flushProgress();
    } else if (!editTimer) {
      editTimer = setTimeout(() => { editTimer = null; flushProgress(); }, EDIT_INTERVAL_MS - elapsed);
    }
  }

  let wtPath;
  try {
    const canReuseWorktree = isContinuation && resumeFrom.wtPath && fs.existsSync(resumeFrom.wtPath);
    if (canReuseWorktree) {
      wtPath = resumeFrom.wtPath;
    } else {
      const created = await createTaskWorktree(project, taskId);
      wtPath = created.wtPath;
    }
    registryEntry.wtPath = wtPath;

    const runResult = await runClaudeHeadless(commandText, wtPath, onProgress, registryEntry.sessionId);
    if (editTimer) { clearTimeout(editTimer); editTimer = null; flushProgress(); }
    registryEntry.sessionId = runResult.sessionId || registryEntry.sessionId;

    // claude 실행이 끝나자마자, 병합 대기열 없이 바로 add+status+commit을 한 번에 처리한다
    // (diff 확인과 실제 커밋 사이에 시간차를 두면, 그 사이 실제 변경사항을 놓치고 "변경 없음"으로
    // 잘못 보고하는 문제가 있었음 - gitWorktree.js의 commitIfChanged 주석 참고).
    const diff = await commitIfChanged(wtPath, `bot: ${commandText.slice(0, 72)} (task ${taskId})`);

    if (!diff.committed) {
      await cleanupWorktree(project, taskId, { deleteBranch: true });
      registryEntry.cleaned = true;
      registryEntry.wtPath = null;
      const resultText = runResult.resultText || '(결과 텍스트 없음)';
      const shown = resultText.length > 1500 ? resultText.slice(0, 1500) + '\n...(생략, 자세한 내용은 DM 확인)' : resultText;
      await statusMsg.edit(
        `✅ 작업은 실행됐지만 코드 변경 사항은 없습니다. (task \`${taskId}\`)\n결과: ${shown}\n\n(이 메시지에 답장하면 이어서 시킬 수 있어요)\n${await getUsageLine()}`
      );
      return;
    }

    await statusMsg.edit(`📦 변경사항 반영 중이에요... (task \`${taskId}\`)`);

    const mergeResult = await mergeWithConflictRetry(
      project,
      taskId,
      commandText,
      wtPath,
      onProgress,
      registryEntry.sessionId
    );
    registryEntry.sessionId = mergeResult.sessionId || registryEntry.sessionId;
    registryEntry.cleaned = true;
    registryEntry.wtPath = null;
    const trunc = (t, max) => (t && t.length > max ? t.slice(0, max) + '\n...(생략, 자세한 내용은 DM 확인)' : t || '(내용 없음)');
    // commitIfChanged가 이미 커밋을 확정지었기 때문에, 여기 도달했다면 mergeAndPush는
    // 성공(pushed: true)하거나 예외를 던지거나 둘 중 하나뿐이다 (실패 시 위 catch에서 처리됨).
    await statusMsg.edit(
      [
        `✅ [${project.name}] 작업 완료, \`${project.baseBranch}\`에 자동 반영/push 했어요. (task \`${taskId}\`)`,
        '**한 일 요약**',
        '```',
        trunc(runResult.resultText, 700),
        '```',
        '**변경 파일**',
        '```',
        trunc(diff.statText, 400),
        '```',
        '(이 메시지에 답장하면 이어서 시킬 수 있어요)',
        await getUsageLine(),
      ].join('\n')
    );

    notifyOwner(client, {
      taskId,
      projectName: project.name,
      requesterTag: message.author.tag,
      originalPrompt: commandText,
      resultText: runResult.resultText,
      diffStat: diff.statText,
      pushed: mergeResult.pushed,
    }).catch((err) => console.error('[discord-bot] failed to notify owner:', err.message));
  } catch (err) {
    try {
      await cleanupWorktree(project, taskId, { deleteBranch: true });
      registryEntry.cleaned = true;
      registryEntry.wtPath = null;
    } catch (_) {}
    await message.reply(`⚠️ 작업 중 오류가 발생했습니다: ${String(err.message || err).slice(0, 500)}\n${await getUsageLine()}`);
  } finally {
    registryEntry.running = false;
    // 진행 중에 쌓인 추가 요청이 있으면 같은 세션으로 바로 이어서 실행 (마지막 요청 메시지에 답장)
    const followUps = registryEntry.followUps.splice(0);
    if (followUps.length) {
      const combined = followUps.length === 1
        ? followUps[0].text
        : followUps.map((f, i) => `${i + 1}. ${f.text}`).join('\n');
      const last = followUps[followUps.length - 1].message;
      handleCommand(last, project, combined, registryEntry).catch((err) =>
        console.error('[discord-bot] unhandled error in follow-up handleCommand:', err.message)
      );
    }
  }
}

// "!ai" 같은 접두사 없이 "고수봇아~", "고수야", "봇아 이거해줘" 처럼 이름을 부르는 것도 명령으로 인식
const ADDRESS_PATTERN = /^(고수봇아|고수봇|고수야|고수|봇아)[~!,.\s]*/;

function extractCommand(content) {
  const matchedPrefix = config.commandPrefixes.find((p) => content.startsWith(p));
  if (matchedPrefix) return { rest: content.slice(matchedPrefix.length).trim(), matched: matchedPrefix };

  const m = content.match(ADDRESS_PATTERN);
  if (m) return { rest: content.slice(m[0].length).trim(), matched: m[1] };

  return null;
}

// 같은 메시지 ID를 두 번 처리하지 않기 위한 방어선. 원인을 아직 완전히 특정하지 못했지만
// (프로세스 1개, messageCreate 이벤트도 1번만 로그에 찍혔는데 handleCommand는 5~6번 실행됨),
// 메모리 Set은 이 상황을 못 막았다. 메시지 ID당 락 파일을 "wx"(이미 있으면 실패)로 원자적
// 생성해서, 어떤 경로로 몇 번을 다시 타든 실제로 통과하는 건 딱 한 번만 되도록 강제한다.
const PROCESSED_DIR = path.join(__dirname, 'processed-messages');
if (!fs.existsSync(PROCESSED_DIR)) fs.mkdirSync(PROCESSED_DIR);
function alreadyProcessed(id) {
  const lockFile = path.join(PROCESSED_DIR, `${id}.lock`);
  try {
    fs.writeFileSync(lockFile, String(Date.now()), { flag: 'wx' });
    return false; // 이번이 처음 → 통과
  } catch (err) {
    if (err.code === 'EEXIST') return true; // 이미 처리(또는 처리 중) → 차단
    console.error('[discord-bot] alreadyProcessed 체크 실패, 안전하게 통과시킴:', err.message);
    return false;
  }
}

client.on('messageCreate', (message) => {
  // 진단용: 왜 같은 요청이 여러 번 처리되는지 원인을 못 찾아서, 필터링 전에 모든 원본
  // 이벤트를 무조건 기록한다 (pid로 프로세스 중복인지, message.id로 이벤트 중복인지 구분 가능).
  console.log(`[discord-bot] RAW messageCreate pid=${process.pid} id=${message.id} author=${message.author && message.author.tag} content=${JSON.stringify((message.content || '').slice(0, 80))}`);

  if (message.author.bot) return;
  if (message.guildId !== config.guildId) return;
  if (!config.commandChannelIds.includes(message.channelId)) return;
  if (alreadyProcessed(message.id)) {
    console.log(`[discord-bot] 중복 이벤트 무시: ${message.id}`);
    return;
  }

  // "토스 배포해줘" 류는 claude를 거치지 않고 ci/toss/deploy-toss.ps1 을 직접 실행 (접두사 있어도/없어도 인식)
  {
    const m = extractCommand(message.content);
    const tossEnv = parseTossDeploy(m ? m.rest : message.content);
    if (tossEnv) {
      handleTossDeploy(message, tossEnv).catch((err) => {
        console.error('[discord-bot] unhandled error in handleTossDeploy:', err.message);
        message.reply(`⚠️ 토스 배포 처리 중 오류: ${String(err.message || err).slice(0, 300)}`).catch(() => {});
      });
      return;
    }
  }

  // "프로젝트 목록 / 프로젝트 등록 / 새 프로젝트" 관리 명령도 claude를 거치지 않고 봇이 직접 처리
  {
    const m = extractCommand(message.content);
    const projectCmd = m ? parseProjectCommand(m.rest) : null;
    if (projectCmd) {
      handleProjectCommand(message, projectCmd).catch((err) => {
        message.reply(`⚠️ 프로젝트 명령 처리 중 오류: ${String(err.message || err).slice(0, 500)}`).catch(() => {});
      });
      return;
    }
  }

  // 봇이 남긴 상태 메시지에 답장하면, 접두사/이름 없이도 이어서 시키는 걸로 인식
  const repliedToId = message.reference && message.reference.messageId;
  let resumeFrom = repliedToId ? taskRegistry.get(repliedToId) : null;
  while (resumeFrom && resumeFrom.next) resumeFrom = resumeFrom.next; // 가장 최신 이어하기 작업으로

  if (resumeFrom) {
    const prefixed = extractCommand(message.content);
    const commandText = (prefixed ? prefixed.rest : message.content).trim();
    if (!commandText) return;
    handleCommand(message, null, commandText, resumeFrom).catch((err) =>
      console.error('[discord-bot] unhandled error in handleCommand:', err.message)
    );
    return;
  }

  const matchResult = extractCommand(message.content);
  if (!matchResult) return;
  const { rest, matched: matchedPrefix } = matchResult;
  if (!rest || /^\[[^\]]+\]\s*$/.test(rest)) {
    message.reply(`사용법: \`${matchedPrefix} <시킬 작업 내용>\` 또는 "고수야 <할일>" 처럼 불러주세요.`).catch(() => {});
    return;
  }

  // "[프로젝트명] 할일" 이면 그 프로젝트, 없으면 내용 보고 자동 배정 (router.js)
  // 각 명령은 독립된 worktree에서 즉시 병렬로 처리됨 (병합 단계만 내부적으로 직렬화됨)
  resolveProjectFromText(rest)
    .then((resolved) => {
      if (resolved.error) return message.reply(resolved.error);
      return handleCommand(message, resolved.project, resolved.text, null, resolved.reason);
    })
    .catch((err) => console.error('[discord-bot] unhandled error in handleCommand:', err.message));
});

client.once('clientReady', async () => {
  console.log(`[discord-bot] logged in as ${client.user.tag}`);
  const prefixList = config.commandPrefixes.map((p) => `\`${p}\``).join(' 또는 ');
  for (const channelId of config.commandChannelIds) {
    try {
      const channel = await client.channels.fetch(channelId);
      await channel.send(`👋 온라인. ${prefixList} <할일> 로 명령해줘. 프로젝트는 내용 보고 자동으로 골라요 (지정하려면 \`[이름] <할일>\`, 목록은 \`프로젝트 목록\`).`);
    } catch (err) {
      console.error(`[discord-bot] failed to send startup greeting to ${channelId}:`, err.message);
    }
  }
});

let shuttingDown = false;
async function gracefulShutdown(signal) {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log(`[discord-bot] shutting down (${signal})`);
  for (const channelId of config.commandChannelIds) {
    try {
      const channel = await client.channels.fetch(channelId);
      await channel.send('💤 봇 꺼졌어요.');
    } catch (err) {
      console.error(`[discord-bot] failed to send shutdown notice to ${channelId}:`, err.message);
    }
  }
  client.destroy();
  process.exit(0);
}
process.on('SIGINT', () => gracefulShutdown('SIGINT'));
process.on('SIGTERM', () => gracefulShutdown('SIGTERM'));

client.login(config.discordToken);
