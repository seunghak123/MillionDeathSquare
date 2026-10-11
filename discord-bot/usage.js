// Claude 구독(Max) 5시간·주간 사용 한도 조회 → 작업 완료 메시지 끝에 붙일 한 줄.
// 이 PC의 Claude 로그인 토큰(~/.claude/.credentials.json)으로 Anthropic 사용량 엔드포인트를 호출한다
// (oh-my-claudecode HUD와 같은 방식). 공식 문서 API가 아니라 형식이 바뀔 수 있으므로,
// 어떤 실패에도 예외를 던지지 않고 짧은 안내 문구를 돌려준다 — 완료 메시지 전송을 막지 않기 위함.
const fs = require('fs');
const os = require('os');
const path = require('path');
const https = require('https');

const CREDENTIALS_PATH = path.join(os.homedir(), '.claude', '.credentials.json');
const TIMEOUT_MS = 5000;

function readAccessToken() {
  const data = JSON.parse(fs.readFileSync(CREDENTIALS_PATH, 'utf8'));
  return data && data.claudeAiOauth && data.claudeAiOauth.accessToken;
}

function fetchUsage(token) {
  return new Promise((resolve, reject) => {
    const req = https.request(
      {
        hostname: 'api.anthropic.com',
        path: '/api/oauth/usage',
        method: 'GET',
        headers: {
          Authorization: `Bearer ${token}`,
          'anthropic-beta': 'oauth-2025-04-20',
          'Content-Type': 'application/json',
        },
        timeout: TIMEOUT_MS,
      },
      (res) => {
        let body = '';
        res.on('data', (c) => (body += c));
        res.on('end', () => {
          if (res.statusCode !== 200) return reject(new Error(`HTTP ${res.statusCode}`));
          try {
            resolve(JSON.parse(body));
          } catch (e) {
            reject(e);
          }
        });
      }
    );
    req.on('timeout', () => req.destroy(new Error('timeout')));
    req.on('error', reject);
    req.end();
  });
}

// "10/9(금) 14:59" 형식, 한국 시간
function formatKst(iso) {
  if (!iso) return '?';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '?';
  const k = new Date(d.getTime() + 9 * 60 * 60 * 1000);
  const days = ['일', '월', '화', '수', '목', '금', '토'];
  const pad = (n) => String(n).padStart(2, '0');
  return `${k.getUTCMonth() + 1}/${k.getUTCDate()}(${days[k.getUTCDay()]}) ${pad(k.getUTCHours())}:${pad(k.getUTCMinutes())}`;
}

function remainingText(window) {
  if (!window || typeof window.utilization !== 'number') return '?';
  return `${Math.max(0, Math.round(100 - window.utilization))}%`;
}

// 사용량 엔드포인트는 호출이 잦으면 HTTP 429(요청 과다)를 돌려준다. 작업이 몰리면 완료 메시지마다
// 호출하게 되고, 같은 토큰을 쓰는 OMC HUD도 주기적으로 조회해서 쉽게 걸린다 (2026-10-09 관찰).
// 그래서 성공 결과를 CACHE_MS 동안 재사용하고, 실패하면 BACKOFF_MS 동안 재조회하지 않으며
// 그 사이엔 마지막 성공값을 "n분 전 기준"으로 보여준다.
const CACHE_MS = 3 * 60 * 1000;
const BACKOFF_MS = 5 * 60 * 1000;
let lastUsage = null; // { data, at }
let backoffUntil = 0;
let inflight = null;

function formatLine(u, staleAt) {
  const stale = staleAt ? ` · ${Math.max(1, Math.round((Date.now() - staleAt) / 60000))}분 전 기준` : '';
  return (
    `📊 남은 사용량 — 5시간 ${remainingText(u.five_hour)} (${formatKst(u.five_hour && u.five_hour.resets_at)} 초기화)` +
    ` · 주간 ${remainingText(u.seven_day)} (${formatKst(u.seven_day && u.seven_day.resets_at)} 초기화)${stale}`
  );
}

/** 예: "📊 남은 사용량 — 5시간 58% (10/3(금) 18:59 초기화) · 주간 87% (10/9(금) 14:59 초기화)" */
async function getUsageLine() {
  const now = Date.now();
  if (lastUsage && now - lastUsage.at < CACHE_MS) return formatLine(lastUsage.data);
  if (now < backoffUntil) {
    return lastUsage ? formatLine(lastUsage.data, lastUsage.at) : '📊 남은 사용량: 잠시 후 다시 조회해요 (조회 요청 과다)';
  }
  try {
    const token = readAccessToken();
    if (!token) return '📊 남은 사용량: 조회 불가 (Claude 로그인 토큰 없음)';
    // 동시에 여러 작업이 끝나도 실제 요청은 한 번만 나가게 한다
    if (!inflight) inflight = fetchUsage(token).finally(() => { inflight = null; });
    const data = await inflight;
    lastUsage = { data, at: Date.now() };
    return formatLine(data);
  } catch (err) {
    backoffUntil = Date.now() + BACKOFF_MS;
    if (lastUsage) return formatLine(lastUsage.data, lastUsage.at);
    const msg = String(err.message || err);
    return /429/.test(msg)
      ? '📊 남은 사용량: 잠시 후 다시 조회해요 (조회 요청 과다, 작업과는 무관)'
      : `📊 남은 사용량: 조회 실패 (${msg.slice(0, 60)})`;
  }
}

module.exports = { getUsageLine };
