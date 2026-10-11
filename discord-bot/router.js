const spawn = require('cross-spawn');
const os = require('os');
const { listProjects, findProject, defaultProject, projectSummary } = require('./projects');

// 메시지에 [프로젝트명]이 없을 때 어느 프로젝트 작업인지 자동으로 고른다 (오너 요청, 2026-10-09).
// 순서: 이름/별칭 언급 → haiku 분류(설명 + 직전 프로젝트 힌트) → 직전 프로젝트 → 기본 프로젝트.
// 직전 프로젝트는 "같은 얘기를 이어서 하는" 경우가 많아서 애매할 때의 기준점으로 쓴다 (프로세스 메모리).
let lastProjectName = null;

function rememberProject(project) {
  lastProjectName = project.name;
}

function lastOrDefault() {
  return (lastProjectName && findProject(lastProjectName)) || defaultProject();
}

// 이름/별칭이 메시지에 들어 있으면 그 프로젝트. 두 개 이상 언급되면 판단을 분류기에 넘긴다.
// 띄어쓰기는 무시하고 비교한다 ("길건너친구들" == "길건너 친구들").
function normalize(s) {
  return String(s || '').toLowerCase().replace(/\s+/g, '');
}

function matchByMention(text, projects) {
  const body = normalize(text);
  const hits = projects.filter((p) =>
    [p.name, ...(p.aliases || [])].some((term) => normalize(term).length >= 2 && body.includes(normalize(term)))
  );
  return hits.length === 1 ? hits[0] : null;
}

const CLASSIFIER_TIMEOUT_MS = 30000;

// 도구/설정/MCP 없이 최소 프롬프트로 haiku를 불러서 호출당 비용을 1센트 미만으로 유지한다.
function classifyWithHaiku(text, projects, lastName) {
  const list = projects.map((p) => `- ${p.name}: ${projectSummary(p) || '(설명 없음)'}`).join('\n');
  const prompt = [
    '디스코드로 들어온 개발 작업 요청을 아래 프로젝트 중 하나에 배정하라.',
    '',
    '[프로젝트 목록]',
    list,
    '',
    `[직전에 작업한 프로젝트] ${lastName || '(없음)'}`,
    '요청만으로 판단이 애매하면 직전 프로젝트를 고른다.',
    '',
    '[요청]',
    text.slice(0, 1500),
    '',
    '프로젝트 이름 하나만 그대로 출력하라. 다른 말은 쓰지 마라.',
  ].join('\n');

  return new Promise((resolve) => {
    const child = spawn('claude', [
      '-p', prompt,
      '--model', 'haiku',
      '--tools', '',
      '--system-prompt', 'You route requests to projects. Output only the project name.',
      '--setting-sources', '',
      '--strict-mcp-config',
      '--output-format', 'json',
    ], { cwd: os.tmpdir(), windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });

    let out = '';
    const timer = setTimeout(() => child.kill(), CLASSIFIER_TIMEOUT_MS);
    child.stdout.on('data', (d) => { out += d.toString(); });
    child.on('error', () => { clearTimeout(timer); resolve(null); });
    child.on('close', () => {
      clearTimeout(timer);
      try {
        const answer = String(JSON.parse(out).result || '').trim().replace(/^[\[`"']+|[\]`"'.]+$/g, '');
        resolve(findProject(answer));
      } catch (_) {
        resolve(null);
      }
    });
  });
}

// 반환: { project, reason }  reason은 상태 메시지에 붙일 짧은 설명 (어떻게 골랐는지)
async function routeProject(text) {
  const projects = listProjects();
  if (projects.length === 1) return { project: projects[0], reason: null };

  const mentioned = matchByMention(text, projects);
  if (mentioned) return { project: mentioned, reason: '이름 언급' };

  const classified = await classifyWithHaiku(text, projects, lastProjectName);
  if (classified) return { project: classified, reason: '자동 판단' };

  return { project: lastOrDefault(), reason: '직전 프로젝트' };
}

module.exports = { routeProject, rememberProject };
