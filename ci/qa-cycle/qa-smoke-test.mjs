// WebGL 빌드 QA 스모크 테스트.
// 로컬로 서빙 중인 Toss WebGL 빌드를 브라우저(Playwright/Chromium)로 열어
// 콘솔 에러, 캔버스 로딩 여부, 기본 방향키 조작 반응까지 확인한다.
//
// 사용법: node qa-smoke-test.mjs <url> <outFile>

import { chromium } from 'playwright';
import fs from 'fs';

const url = process.argv[2];
const outFile = process.argv[3];

if (!url || !outFile) {
  console.error('Usage: node qa-smoke-test.mjs <url> <outFile>');
  process.exit(2);
}

const result = {
  url,
  loaded: false,
  canvasFound: false,
  inputAttempted: false,
  errors: [],
};

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();

page.on('console', (msg) => {
  if (msg.type() === 'error') result.errors.push(`console: ${msg.text()}`);
});
page.on('pageerror', (err) => {
  result.errors.push(`pageerror: ${err.message}`);
});
page.on('requestfailed', (req) => {
  result.errors.push(`requestfailed: ${req.url()} — ${req.failure()?.errorText ?? 'unknown'}`);
});

try {
  await page.goto(url, { waitUntil: 'load', timeout: 60000 });
  result.loaded = true;

  // Unity WebGL 캔버스 생성 대기
  await page.waitForSelector('canvas', { timeout: 60000 });
  result.canvasFound = true;

  // Unity 런타임 초기화 여유 시간 (로딩바 완료까지)
  await page.waitForTimeout(10000);

  // 캔버스에 포커스 후 기본 조작 스모크 테스트
  const canvas = await page.$('canvas');
  if (canvas) await canvas.click({ timeout: 5000 }).catch(() => {});

  for (const key of ['ArrowUp', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowUp']) {
    await page.keyboard.press(key);
    await page.waitForTimeout(1200);
  }
  result.inputAttempted = true;
} catch (e) {
  result.errors.push(`playwright: ${e.message}`);
}

fs.writeFileSync(outFile, JSON.stringify(result, null, 2));
await browser.close();

process.exit(result.errors.length > 0 || !result.loaded || !result.canvasFound ? 1 : 0);
