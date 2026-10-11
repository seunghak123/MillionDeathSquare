const {
  listProjects,
  findProject,
  defaultProject,
  registerExistingProject,
  createNewProject,
  setProjectMeta,
  NEW_PROJECTS_ROOT,
} = require('./projects');
const { routeProject } = require('./router');

// 디스코드 메시지(접두사 뗀 뒤)에서 프로젝트 관리 명령을 인식한다. 아니면 null.
//   프로젝트 목록
//   프로젝트 등록 <이름> <경로>                (이미 PC에 있는 git 저장소)
//   새 프로젝트 <이름> <git 원격 URL> [설명]   (NEW_PROJECTS_ROOT\<이름> 에 clone)
//   프로젝트 설명 <이름> <설명>                (자동 라우팅에 쓰이는 설명)
//   프로젝트 별칭 <이름> <별칭1,별칭2>         (메시지에 이 단어가 나오면 그 프로젝트로)
function parseProjectCommand(text) {
  const t = (text || '').trim();
  if (/^프로젝트\s*(목록|리스트|list)\s*$/i.test(t)) return { type: 'list' };

  let m = t.match(/^프로젝트\s*등록\s+(\S+)\s+(.+)$/);
  if (m) return { type: 'register', name: m[1], root: stripWrap(m[2]) };

  m = t.match(/^(?:새\s*프로젝트|프로젝트\s*(?:생성|만들기))\s+(\S+)\s+(\S+)(?:\s+([\s\S]+))?$/);
  if (m) return { type: 'create', name: m[1], gitUrl: stripWrap(m[2]), description: m[3] ? m[3].trim() : null };

  m = t.match(/^프로젝트\s*설명\s+(\S+)\s+([\s\S]+)$/);
  if (m) return { type: 'describe', name: m[1], description: m[2].trim() };

  m = t.match(/^프로젝트\s*별칭\s+(\S+)\s+(.+)$/);
  // 쉼표가 있으면 쉼표로만 나눈다 ("길건너 친구들, 고투홈" 처럼 띄어쓰기 있는 별칭 허용)
  if (m) {
    const sep = m[2].includes(',') ? /\s*,\s*/ : /\s+/;
    return { type: 'alias', name: m[1], aliases: m[2].split(sep).map((a) => a.trim()).filter(Boolean) };
  }

  if (/^(새\s*프로젝트|프로젝트\s*(등록|생성|만들기|설명|별칭))/.test(t)) return { type: 'usage' };
  return null;
}

// 디스코드가 링크를 <...> 로 감싸거나 경로를 따옴표로 감싸는 경우 제거
function stripWrap(s) {
  return s.trim().replace(/^[<"'`]+|[>"'`]+$/g, '');
}

const USAGE = [
  '**프로젝트 명령**',
  '`프로젝트 목록` — 관리 중인 프로젝트 보기',
  '`프로젝트 등록 <이름> <경로>` — PC에 이미 있는 git 저장소 등록 (origin 원격 필요)',
  `\`새 프로젝트 <이름> <git URL> [설명]\` — \`${NEW_PROJECTS_ROOT}\\<이름>\` 에 clone 해서 새로 시작 (빈 레포면 초기 커밋 후 push)`,
  '`프로젝트 설명 <이름> <설명>` / `프로젝트 별칭 <이름> <별칭,...>` — 자동 배정 정확도 올리기',
  '그냥 `<할일>` 만 말하면 내용 보고 프로젝트를 자동으로 골라요. 확실히 하려면 `[이름] <할일>`',
].join('\n');

function formatList() {
  const def = defaultProject().name;
  return listProjects()
    .map((p) => {
      const extra = [
        p.aliases && p.aliases.length ? `별칭: ${p.aliases.join(', ')}` : null,
        p.description ? `설명: ${p.description.slice(0, 80)}` : null,
      ].filter(Boolean).join(' / ');
      return `• **${p.name}**${p.name === def ? ' (기본)' : ''} — \`${p.root}\` (${p.baseBranch})${extra ? `\n   ${extra}` : ''}`;
    })
    .join('\n');
}

async function handleProjectCommand(message, cmd) {
  if (cmd.type === 'usage') {
    await message.reply(USAGE);
    return;
  }
  if (cmd.type === 'list') {
    await message.reply(`📁 **프로젝트 목록**\n${formatList()}\n\n${USAGE}`);
    return;
  }
  if (cmd.type === 'describe' || cmd.type === 'alias') {
    const patch = cmd.type === 'describe' ? { description: cmd.description } : { aliases: cmd.aliases };
    const p = setProjectMeta(cmd.name, patch);
    await message.reply(`✅ **${p.name}** ${cmd.type === 'describe' ? '설명' : '별칭'} 저장했어요.`);
    return;
  }
  const status = await message.reply(
    cmd.type === 'create' ? `🛠️ 새 프로젝트 \`${cmd.name}\` 만드는 중이에요...` : `🛠️ 프로젝트 \`${cmd.name}\` 등록 중이에요...`
  );
  try {
    const p = cmd.type === 'create'
      ? await createNewProject(cmd.name, cmd.gitUrl, cmd.description)
      : await registerExistingProject(cmd.name, cmd.root);
    await status.edit(
      `✅ 프로젝트 **${p.name}** ${cmd.type === 'create' ? '생성' : '등록'} 완료 — \`${p.root}\` (${p.baseBranch})\n` +
      `이제 그냥 시키면 내용 보고 자동으로 배정돼요. 정확도를 올리려면 \`프로젝트 설명 ${p.name} <무슨 프로젝트인지>\``
    );
  } catch (err) {
    await status.edit(`⚠️ 실패: ${String(err.message || err).slice(0, 800)}`);
  }
}

// "[이름] 할일" 형식이면 해당 프로젝트를, 아니면 내용을 보고 자동 배정한다 (router.js).
// 반환: { project, text, reason } 또는 { error }
async function resolveProjectFromText(text) {
  const m = (text || '').match(/^\[([^\]]+)\]\s*/);
  if (!m) {
    const { project, reason } = await routeProject(text || '');
    return { project, text, reason };
  }
  const project = findProject(m[1]);
  if (!project) {
    return { error: `❓ \`${m[1].trim()}\` 프로젝트를 모르겠어요.\n${formatList()}` };
  }
  return { project, text: text.slice(m[0].length).trim(), reason: null };
}

module.exports = { parseProjectCommand, handleProjectCommand, resolveProjectFromText };
