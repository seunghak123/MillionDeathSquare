require('dotenv').config();

function required(name) {
  const v = process.env[name];
  if (!v) throw new Error(`Missing required env var: ${name}`);
  return v;
}

module.exports = {
  discordToken: required('DISCORD_TOKEN'),
  guildId: required('GUILD_ID'),
  // 쉼표로 여러 채널 지정 가능. 첫 번째 채널은 기존 단일 채널 코드와의 호환용.
  commandChannelIds: required('COMMAND_CHANNEL_ID').split(',').map((s) => s.trim()).filter(Boolean),
  get commandChannelId() { return this.commandChannelIds[0]; },
  ownerId: required('OWNER_ID'),
  commandPrefixes: (process.env.COMMAND_PREFIXES || process.env.COMMAND_PREFIX || '!ai,!수민')
    .split(',')
    .map((s) => s.trim())
    .filter(Boolean),
  approvalTimeoutMinutes: Number(process.env.APPROVAL_TIMEOUT_MINUTES || 30),
  projectRoot: required('PROJECT_ROOT'),
  baseBranch: process.env.BASE_BRANCH || 'main',
  claudeMaxBudgetUsd: Number(process.env.CLAUDE_MAX_BUDGET_USD || 2),
  claudeTimeoutMinutes: Number(process.env.CLAUDE_TIMEOUT_MINUTES || 15),
};
