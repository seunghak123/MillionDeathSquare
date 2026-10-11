const { execFile } = require('child_process');
const fs = require('fs');
const path = require('path');
const { promisify } = require('util');
const execFileP = promisify(execFile);

const config = require('./config');

// 봇이 관리하는 프로젝트 목록. BaseProject(PROJECT_ROOT)는 항상 기본 프로젝트로 포함되고,
// 나머지는 "새 프로젝트"/"프로젝트 등록" 명령으로 projects.json 에 추가된다 (PC 로컬 설정이라 gitignore).
const REGISTRY_PATH = path.join(__dirname, 'projects.json');

// 봇이 새로 만드는 프로젝트가 생기는 전용 폴더 (오너 선택, 2026-10-09)
const NEW_PROJECTS_ROOT = process.env.NEW_PROJECTS_ROOT || 'C:\\Users\\dhtmd\\Projects';

const DEFAULT_PROJECT_NAME = path.basename(config.projectRoot);

// 폴더명/브랜치명/worktree 경로에 그대로 쓰이므로 경로 조작 문자는 허용하지 않는다.
const NAME_PATTERN = /^[A-Za-z0-9가-힣_.-]{1,40}$/;

async function git(args, cwd) {
  return execFileP('git', args, { cwd, maxBuffer: 1024 * 1024 * 32 });
}

function loadRegistry() {
  try {
    return JSON.parse(fs.readFileSync(REGISTRY_PATH, 'utf8'));
  } catch (_) {
    return { projects: [] };
  }
}

function saveRegistry(data) {
  fs.writeFileSync(REGISTRY_PATH, JSON.stringify(data, null, 2));
}

function defaultProject() {
  return { name: DEFAULT_PROJECT_NAME, root: config.projectRoot, baseBranch: config.baseBranch };
}

// meta: 프로젝트 이름(소문자) -> { description, aliases }. 기본 프로젝트에도 붙일 수 있도록
// projects 배열과 분리해 둔다. 자동 라우팅(router.js)이 이 정보로 메시지를 프로젝트에 매핑한다.
function listProjects() {
  const data = loadRegistry();
  const meta = data.meta || {};
  return [defaultProject(), ...data.projects].map((p) => ({ ...p, ...(meta[p.name.toLowerCase()] || {}) }));
}

function setProjectMeta(name, patch) {
  const project = findProject(name);
  if (!project) throw new Error(`\`${name}\` 프로젝트를 모르겠어요.`);
  const data = loadRegistry();
  data.meta = data.meta || {};
  const key = project.name.toLowerCase();
  data.meta[key] = { ...(data.meta[key] || {}), ...patch };
  saveRegistry(data);
  return { ...project, ...data.meta[key] };
}

// 설명을 따로 안 적어둔 프로젝트는 CLAUDE.md / README.md 앞부분을 요약 재료로 쓴다.
const summaryCache = new Map();
function projectSummary(p) {
  if (p.description) return p.description;
  if (summaryCache.has(p.root)) return summaryCache.get(p.root);
  let text = '';
  for (const f of ['CLAUDE.md', 'README.md']) {
    try {
      text = fs.readFileSync(path.join(p.root, f), 'utf8');
      break;
    } catch (_) {}
  }
  const summary = text.replace(/```[\s\S]*?```/g, ' ').replace(/[#|*`>-]+/g, ' ').replace(/\s+/g, ' ').trim().slice(0, 400);
  summaryCache.set(p.root, summary);
  return summary;
}

// 이름 또는 별칭으로 찾는다 (대소문자·띄어쓰기 무시). 예: "[고투홈] ..." → BaseProject
function findProject(name) {
  const norm = (s) => String(s || '').toLowerCase().replace(/\s+/g, '');
  const key = norm(name);
  if (!key) return null;
  const projects = listProjects();
  return projects.find((p) => norm(p.name) === key)
    || projects.find((p) => (p.aliases || []).some((a) => norm(a) === key))
    || null;
}

function validateName(name) {
  if (!NAME_PATTERN.test(name || '')) {
    throw new Error('프로젝트 이름은 한글/영문/숫자/_ . - 만, 40자 이내로 써주세요.');
  }
  if (findProject(name)) throw new Error(`이미 \`${name}\` 이름의 프로젝트가 있어요.`);
}

function addToRegistry(project) {
  const data = loadRegistry();
  data.projects.push(project);
  saveRegistry(data);
  return project;
}

// 봇의 worktree 흐름(origin/<branch> 기준 분기 → 병합 → push)이 동작하려면 origin 원격과
// 원격 기본 브랜치가 있어야 한다. 기존 프로젝트 등록 시 이를 확인하고 기본 브랜치를 알아낸다.
async function detectBaseBranch(root) {
  await git(['remote', 'get-url', 'origin'], root).catch(() => {
    throw new Error('origin 원격이 없는 저장소예요. git remote add origin <URL> 후 다시 등록해주세요.');
  });
  await git(['fetch', 'origin'], root);
  try {
    const { stdout } = await git(['symbolic-ref', '--short', 'refs/remotes/origin/HEAD'], root);
    return stdout.trim().replace(/^origin\//, '');
  } catch (_) {
    // origin/HEAD 가 설정 안 된 경우: main → master 순으로 원격에 있는지 확인
    for (const b of ['main', 'master']) {
      try {
        await git(['rev-parse', '--verify', `origin/${b}`], root);
        return b;
      } catch (_) {}
    }
    throw new Error('원격 기본 브랜치(main/master)를 찾지 못했어요. 먼저 한 번 push 해주세요.');
  }
}

async function registerExistingProject(name, root) {
  validateName(name);
  const resolved = path.resolve(root);
  if (!fs.existsSync(resolved)) throw new Error(`경로가 없어요: ${resolved}`);
  try {
    const { stdout } = await git(['rev-parse', '--show-toplevel'], resolved);
    // Windows 경로는 대소문자를 구분하지 않고, git은 드라이브/폴더 대소문자를 다르게 돌려줄 수 있다.
    if (path.resolve(stdout.trim()).toLowerCase() !== resolved.toLowerCase()) {
      throw new Error(`git 저장소 루트가 아니에요. 루트 경로로 등록해주세요: ${stdout.trim()}`);
    }
  } catch (err) {
    if (/루트 경로/.test(err.message)) throw err;
    throw new Error(`git 저장소가 아니에요: ${resolved}`);
  }
  const baseBranch = await detectBaseBranch(resolved);
  return addToRegistry({ name, root: resolved, baseBranch });
}

// 오너가 준 원격 레포(빈 레포 또는 기존 레포)를 전용 폴더에 clone 해서 새 프로젝트로 등록한다.
// 빈 레포면 README 초기 커밋을 만들어 push 해서 이후 worktree 흐름이 기댈 origin/<branch> 를 만든다.
async function createNewProject(name, gitUrl, description) {
  validateName(name);
  const root = path.join(NEW_PROJECTS_ROOT, name);
  if (fs.existsSync(root)) throw new Error(`폴더가 이미 있어요: ${root} (기존 폴더면 "프로젝트 등록"을 써주세요)`);
  fs.mkdirSync(NEW_PROJECTS_ROOT, { recursive: true });

  await git(['clone', gitUrl, root], NEW_PROJECTS_ROOT);

  let isEmpty = false;
  try {
    await git(['rev-parse', '--verify', 'HEAD'], root);
  } catch (_) {
    isEmpty = true;
  }

  let baseBranch;
  if (isEmpty) {
    baseBranch = 'main';
    await git(['symbolic-ref', 'HEAD', `refs/heads/${baseBranch}`], root);
    fs.writeFileSync(path.join(root, 'README.md'), `# ${name}\n`);
    await git(['add', 'README.md'], root);
    await git(['commit', '-m', 'Initial commit'], root);
    await git(['push', '-u', 'origin', baseBranch], root);
  } else {
    baseBranch = await detectBaseBranch(root);
  }

  return addToRegistry(description ? { name, root, baseBranch, description } : { name, root, baseBranch });
}

module.exports = {
  listProjects,
  findProject,
  defaultProject,
  registerExistingProject,
  createNewProject,
  setProjectMeta,
  projectSummary,
  NEW_PROJECTS_ROOT,
};
