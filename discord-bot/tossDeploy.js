// "토스 배포해줘" 류 메시지를 claude로 넘기지 않고 ci/toss/deploy-toss.ps1 을 직접 실행하는 전용 핸들러.
// - 환경 해석: dev만 언급 → dev / prod만 언급 → prod / 없음 또는 둘 다 → all (dev 성공 후 prod)
// - 빌드는 수십 분 걸릴 수 있어 타임아웃을 두고, 동시에 하나만 실행한다 (이미 배포 중이면 거부).
// - 스크립트가 비밀값(배포 키)을 로그/출력에서 마스킹하므로 여기서는 출력을 그대로 중계한다.
const { spawn, execFile } = require('child_process');
const fs = require('fs');
const path = require('path');

const config = require('./config');
const { getUsageLine } = require('./usage');

const TIMEOUT_MINUTES = Number(process.env.TOSS_DEPLOY_TIMEOUT_MINUTES || 90);
const EDIT_INTERVAL_MS = 4000;

let running = null; // { env, startedAt, pid }

// 빌드 요청도 배포와 동일하게 처리한다: 빌드 → ait 업로드까지 무조건 진행 (업로드 없는 빌드는 없음).
//   - "토스 배포해줘" (환경 없음)      → all (dev 성공 후 prod)
//   - "토스 빌드해줘" / "빌드 올려줘"  → dev (환경 없는 빌드는 dev)
//   - "dev 빌드해줘", "prod 배포해줘" 처럼 환경을 말하면 "토스"가 없어도 인식
function parseTossDeploy(text) {
  const t = (text || '').trim();
  if (!t || t.length > 80) return null; // 긴 문장은 일반 작업 지시일 가능성이 높음
  if (/(하지\s*마|말고|취소|방법|어떻게|왜|\?)/.test(t)) return null; // 질문/부정은 일반 처리로 넘김
  if (/(에러|오류|고쳐|수정|실패|깨|안\s*돼|안돼)/.test(t)) return null; // "빌드 에러 고쳐줘" 류는 일반 작업

  const isDeploy = /(배포|deploy|업로드|올려)/i.test(t);
  const isBuild = /(빌드|build)/i.test(t);
  if (!isDeploy && !isBuild) return null;

  const hasProd = /(prod|프로드|프로덕션|라이브|실서비스|실제)/i.test(t);
  const hasDev = /(\bdev\b|데브|개발|테스트)/i.test(t);
  const hasToss = /(토스|toss)/i.test(t);
  // "토스"도 환경도 없는 문장은, 짧은 "빌드해줘"/"빌드 해" 같은 단독 빌드 요청만 인식
  if (!hasToss && !hasProd && !hasDev && !/^(빌드|build)(\s*(좀|해|해줘|해주세요|부탁해|ㄱ|고))*\s*[.!]*$/i.test(t)) return null;

  if (hasProd && !hasDev) return 'prod';
  if (hasDev && !hasProd) return 'dev';
  if (isBuild) return 'dev'; // 환경 없는 빌드 요청은 dev ("빌드 올려줘" 포함)
  return 'all';
}

function trunc(text, max) {
  if (text.length <= max) return text;
  return '...' + text.slice(text.length - max);
}

function killTree(pid) {
  execFile('taskkill', ['/PID', String(pid), '/T', '/F'], () => {});
}

async function handleTossDeploy(message, env) {
  if (message.author.id !== config.ownerId) {
    await message.reply('🚫 토스 배포는 오너만 실행할 수 있어요.');
    return;
  }
  if (running) {
    const mins = Math.round((Date.now() - running.startedAt) / 60000);
    await message.reply(`⏳ 이미 토스 배포(\`${running.env}\`)가 진행 중이에요 (${mins}분 경과). 끝난 뒤 다시 요청해주세요.`);
    return;
  }

  const script = path.join(config.projectRoot, 'ci', 'toss', 'deploy-toss.ps1');
  if (!fs.existsSync(script)) {
    await message.reply(`⚠️ 배포 스크립트가 없어요: \`${script}\` (main에 반영됐는지 확인)`);
    return;
  }

  const label = env === 'all' ? 'dev → prod' : env;
  const statusMsg = await message.reply(`🚀 토스 빌드 → ait 업로드 시작: \`${label}\` (10분 이상 걸릴 수 있어요)`);

  const child = spawn(
    'powershell.exe',
    // 봇 빌드는 이 PC에 열려 있는 Unity 에디터를 강제로 닫고 진행한다 (열려 있으면 배치 빌드 불가)
    ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', script, '-Env', env, '-ForceCloseEditor'],
    { cwd: config.projectRoot, windowsHide: true }
  );
  running = { env, startedAt: Date.now(), pid: child.pid };

  const lines = [];
  let phase = '준비';
  let lastEditAt = 0;
  let editTimer = null;
  const pushLines = (buf) => {
    for (const raw of buf.toString('utf8').split(/\r?\n/)) {
      const l = raw.trim();
      if (!l) continue;
      lines.push(l);
      if (lines.length > 400) lines.shift();
      const m = l.match(/=== \[(dev|prod)\] (start|OK|FAILED)/);
      if (m) phase = `${m[1]} ${m[2] === 'start' ? '진행' : m[2] === 'OK' ? '완료' : '실패'}`;
      else if (/Unity \[(\w+) (set symbols|build)\]/.test(l)) {
        const mm = l.match(/Unity \[(\w+) (set symbols|build)\]/);
        phase = `${mm[1]} ${mm[2] === 'build' ? 'Unity 빌드/패키징' : '심볼 설정'}`;
      } else if (/\] \[(dev|prod)\] deploy:/.test(l)) {
        phase = `${l.match(/\[(dev|prod)\] deploy/)[1]} ait deploy`;
      }
    }
    scheduleEdit();
  };
  const flush = () => {
    editTimer = null;
    lastEditAt = Date.now();
    const mins = Math.round((Date.now() - running.startedAt) / 60000);
    statusMsg.edit(`🔧 토스 배포 \`${label}\` 진행 중 — ${phase} (${mins}분 경과)`).catch(() => {});
  };
  const scheduleEdit = () => {
    if (editTimer) return;
    const wait = Math.max(0, EDIT_INTERVAL_MS - (Date.now() - lastEditAt));
    editTimer = setTimeout(flush, wait);
  };

  child.stdout.on('data', pushLines);
  child.stderr.on('data', pushLines);

  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    killTree(child.pid);
  }, TIMEOUT_MINUTES * 60 * 1000);

  let finished = false;
  const finish = async (code, spawnError) => {
    if (finished) return;
    finished = true;
    clearTimeout(timer);
    if (editTimer) { clearTimeout(editTimer); editTimer = null; }
    const mins = Math.round((Date.now() - running.startedAt) / 60000);
    running = null;

    const summaryIdx = lines.findIndex((l) => l.includes('SUMMARY'));
    const summary = summaryIdx >= 0 ? lines.slice(summaryIdx + 1).join('\n') : '';
    if (code === 0 && !timedOut) {
      await statusMsg.edit(`✅ 토스 배포 완료 \`${label}\` (${mins}분)`).catch(() => {});
      await message.reply(`✅ 토스 배포 \`${label}\` 성공 (${mins}분)\n\`\`\`\n${trunc(summary || '(요약 없음)', 1500)}\n\`\`\`\n${await getUsageLine()}`);
    } else {
      const reason = spawnError ? `실행 실패: ${spawnError.message}` : timedOut ? `타임아웃(${TIMEOUT_MINUTES}분)으로 중단` : `종료 코드 ${code}`;
      await statusMsg.edit(`❌ 토스 배포 실패 \`${label}\` (${reason})`).catch(() => {});
      const tail = lines.slice(-25).join('\n');
      await message.reply(`❌ 토스 배포 \`${label}\` 실패 — ${reason} (${mins}분)\n\`\`\`\n${trunc(tail || '(출력 없음)', 1550)}\n\`\`\`\n전체 로그: \`ci/toss/logs/\`\n${await getUsageLine()}`);
    }
  };

  child.on('error', (err) => finish(-1, err).catch(() => {}));
  child.on('close', (code) => finish(code).catch((e) => console.error('[discord-bot] tossDeploy finish error:', e.message)));
}

module.exports = { parseTossDeploy, handleTossDeploy };
