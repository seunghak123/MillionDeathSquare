const config = require('./config');

function truncate(text, max) {
  if (!text) return '(내용 없음)';
  return text.length > max ? text.slice(0, max) + '\n...(생략)' : text;
}

// 오너에게 DM으로 작업 요약 + diff stat 을 보고한다. 승인 대기 없이 정보 공유만 목적.
async function notifyOwner(client, { taskId, projectName, requesterTag, originalPrompt, resultText, diffStat, pushed }) {
  const owner = await client.users.fetch(config.ownerId);

  const status = pushed ? '✅ main에 자동 반영/push 완료' : 'ℹ️ 변경 사항 없음';

  const body = [
    `**[작업 완료 보고]** [${projectName}] task \`${taskId}\` - ${status}`,
    `요청자: ${requesterTag}`,
    ``,
    `**요청 내용**`,
    '```',
    truncate(originalPrompt, 500),
    '```',
    `**작업 요약**`,
    '```',
    truncate(resultText, 900),
    '```',
    `**변경 사항 (git diff --stat)**`,
    '```',
    truncate(diffStat, 500),
    '```',
  ].join('\n');

  await owner.send(body);
}

module.exports = { notifyOwner };
