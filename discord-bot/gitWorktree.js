const { execFile } = require('child_process');
const fs = require('fs');
const path = require('path');
const { promisify } = require('util');
const execFileP = promisify(execFile);

// 모든 함수는 project({ name, root, baseBranch }, projects.js 참고)를 받아 해당 저장소에서 동작한다.
// 작업용 worktree들은 프로젝트 폴더 형제 디렉토리에 둔다 (Unity가 Assets로 스캔하지 않도록).
// BaseProject 기준 경로는 기존과 같다: BaseProject-bot-worktrees / BaseProject-bot-integration
function worktreeRoot(project) {
  return path.resolve(project.root, '..', `${path.basename(project.root)}-bot-worktrees`);
}

// 병합 전용 별도 worktree. 사람이 절대 손대지 않는 폴더라서, 여기서는
// "메인 작업 디렉토리가 지저분해서 병합 중단" 문제가 구조적으로 발생할 수 없다.
function integrationPath(project) {
  return path.resolve(project.root, '..', `${path.basename(project.root)}-bot-integration`);
}

function branchName(taskId) {
  return `bot/task-${taskId}`;
}

function worktreePath(project, taskId) {
  return path.join(worktreeRoot(project), taskId);
}

async function git(args, cwd) {
  return execFileP('git', args, { cwd, maxBuffer: 1024 * 1024 * 32 });
}

// worktree add/remove, branch 삭제, fetch, merge, push 등은 전부 같은 .git 관리 파일을
// 공유해서 건드린다 (worktree들은 저장소 객체DB/refs를 공유함). Windows는 여러 git 프로세스가
// 동시에 이 파일들에 접근하면 "Permission denied" 류 에러를 낸다. 그래서 이런 "관리용" git
// 호출은 전부 이 큐를 통해 한 번에 하나씩만 실행한다. task별 작업 파일 자체(add/status/diff/
// commit, 각자 worktree 안에서만 일어남)는 여기 안 걸고 병렬로 둔다.
let gitAdminQueue = Promise.resolve();
function withGitAdminLock(fn) {
  const next = gitAdminQueue.then(fn, fn);
  gitAdminQueue = next.catch(() => {}); // 큐 자체는 이전 실패로 막히면 안 됨
  return next;
}

async function ensureIntegrationWorktree(project) {
  const INTEGRATION_PATH = integrationPath(project);
  if (fs.existsSync(INTEGRATION_PATH)) return;
  return withGitAdminLock(async () => {
    if (fs.existsSync(INTEGRATION_PATH)) return; // 락 대기 중 다른 호출이 이미 만들었을 수 있음
    await git(['fetch', 'origin', project.baseBranch], project.root);
    // --detach: 로컬 main 브랜치를 절대 물지 않는다. main은 이미 메인 작업 디렉토리에
    // 체크아웃돼 있어서, 같은 브랜치를 여기서 또 체크아웃하면 git이 거부한다.
    await git(
      ['worktree', 'add', '--detach', INTEGRATION_PATH, `origin/${project.baseBranch}`],
      project.root
    );
  });
}

async function createTaskWorktree(project, taskId) {
  const branch = branchName(taskId);
  const wtPath = worktreePath(project, taskId);
  const INTEGRATION_PATH = integrationPath(project);

  await ensureIntegrationWorktree(project);
  return withGitAdminLock(async () => {
    // project.root(사람이 직접 커밋/푸시하는 메인 폴더)는 절대 건드리지 않는다.
    // 봇의 모든 git 관리 작업은 INTEGRATION_PATH 안에서만 일어나서, 사람이 메인 폴더에서
    // 뭘 하든 (fetch/pull/push) refs 충돌이 구조적으로 발생할 수 없다.
    await git(['fetch', 'origin', project.baseBranch], INTEGRATION_PATH);
    await git(
      ['worktree', 'add', '-b', branch, wtPath, `origin/${project.baseBranch}`],
      INTEGRATION_PATH
    );
    return { branch, wtPath };
  });
}

// add -A + status + commit을 한 번에 붙여서 처리한다. 예전엔 "diff 확인용 add -A/status"와
// "커밋용 add -A/status"를 따로(시간차를 두고, 병합 큐 대기 후) 두 번 실행했는데, 그 사이 시간차 때문에
// Claude가 실제로 만든 변경사항을 뒤늦은 두 번째 체크에서 놓쳐 "변경 사항 없음"으로 잘못 보고하는
// 사례가 발생했다 (2026-09-19). Claude 실행이 끝나자마자, 대기열 없이 바로 커밋해서 이 시간차를 없앤다.
async function commitIfChanged(wtPath, message) {
  await git(['add', '-A'], wtPath);
  const { stdout: statusOut } = await git(['status', '--porcelain'], wtPath);
  if (!statusOut.trim()) {
    return { committed: false, statText: '(변경 없음)' };
  }
  const { stdout: diffOut } = await git(['diff', '--cached', '--stat'], wtPath);
  await git(['commit', '-m', message], wtPath);
  return { committed: true, statText: diffOut.trim() || statusOut.trim() };
}

async function mergeAndPush(project, taskId) {
  const branch = branchName(taskId);
  const INTEGRATION_PATH = integrationPath(project);

  await ensureIntegrationWorktree(project);
  // 로컬 main 브랜치를 물지 않고 매번 origin/main 최신 커밋으로 detached HEAD를 갱신한 뒤,
  // 병합 결과를 HEAD:main 으로 직접 push한다 (메인 작업 디렉토리의 main 체크아웃과 절대 충돌하지 않음).
  await withGitAdminLock(async () => {
    await git(['fetch', 'origin', project.baseBranch], INTEGRATION_PATH);
    await git(['checkout', '--detach', `origin/${project.baseBranch}`], INTEGRATION_PATH);
    try {
      await git(['merge', '--no-ff', branch, '-m', `Merge ${branch}`], INTEGRATION_PATH);
    } catch (err) {
      // 두 task가 같은 파일의 같은 부분을 건드리면 병합 충돌이 날 수 있다 (워크트리 격리로는
      // 막을 수 없는, git 병합 자체의 정상적인 한계). 여기서 abort 안 하면 INTEGRATION_PATH가
      // unmerged 상태로 계속 남아서, 이 충돌과 무관한 이후 모든 작업의 병합까지 연쇄로 막힌다.
      await git(['merge', '--abort'], INTEGRATION_PATH).catch(() => {});
      // isMergeConflict 플래그로 표시해서, 호출자(index.js)가 이 경우에만 자동 충돌 해결을 시도하게 한다.
      const conflictErr = new Error(err.message);
      conflictErr.isMergeConflict = true;
      throw conflictErr;
    }
    await git(['push', 'origin', `HEAD:${project.baseBranch}`], INTEGRATION_PATH);
  });

  await cleanupWorktree(project, taskId, { deleteBranch: true });
  return { pushed: true };
}

// 병합 충돌이 난 task의 워크트리 안에서, 최신 main을 직접 병합해서 충돌 마커를 만들어준다.
// (실제 충돌 해결은 여기서 하지 않는다 - claude를 다시 불러서 처리하도록 index.js에 맡김)
async function syncWithLatestMain(project, taskId) {
  const wtPath = worktreePath(project, taskId);
  return withGitAdminLock(async () => {
    await git(['fetch', 'origin', project.baseBranch], wtPath);
    try {
      await git(
        ['merge', '--no-ff', `origin/${project.baseBranch}`, '-m', `Merge origin/${project.baseBranch} (충돌 자동 해결용)`],
        wtPath
      );
      return { hadConflict: false };
    } catch (_) {
      const { stdout } = await git(['diff', '--name-only', '--diff-filter=U'], wtPath);
      return { hadConflict: true, conflictFiles: stdout.split('\n').map((s) => s.trim()).filter(Boolean) };
    }
  });
}

async function hasUnmergedPaths(wtPath) {
  const { stdout } = await git(['diff', '--name-only', '--diff-filter=U'], wtPath);
  return stdout.trim().length > 0;
}

async function cleanupWorktree(project, taskId, { deleteBranch }) {
  const branch = branchName(taskId);
  const wtPath = worktreePath(project, taskId);
  const INTEGRATION_PATH = integrationPath(project);
  await withGitAdminLock(async () => {
    try {
      await git(['worktree', 'remove', wtPath, '--force'], project.root);
    } catch (_) {
      // ignore, worktree may already be gone
    }
    if (deleteBranch) {
      try {
        // 병합용 worktree(INTEGRATION_PATH)에서 브랜치를 지운다 - 메인 체크아웃과 무관하게 항상 가능
        if (!fs.existsSync(INTEGRATION_PATH)) {
          await git(['fetch', 'origin', project.baseBranch], project.root);
          await git(['worktree', 'add', '--detach', INTEGRATION_PATH, `origin/${project.baseBranch}`], project.root);
        }
        await git(['branch', '-D', branch], INTEGRATION_PATH);
      } catch (_) {
        // ignore
      }
    }
  });
}

module.exports = {
  createTaskWorktree,
  commitIfChanged,
  mergeAndPush,
  syncWithLatestMain,
  hasUnmergedPaths,
  cleanupWorktree,
  worktreePath,
  branchName,
};
