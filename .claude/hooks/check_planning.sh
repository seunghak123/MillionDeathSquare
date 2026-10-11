#!/bin/bash
# 5시간 마다 기획서 확인 + 작업 계속 진행 루틴
# SessionStart 훅에서 호출됨

TIMESTAMP_FILE="$HOME/.claude/projects/C--Users-dhtmd-Desktop-BaseProject/last_planning_check"
DOCS_FILE="$(dirname "$0")/planning_docs.txt"
CURRENT_TIME=$(date +%s)
FIVE_HOURS=18000

# 마지막 체크 후 5시간 미경과 시 조용히 종료
if [ -f "$TIMESTAMP_FILE" ]; then
    LAST_CHECK=$(cat "$TIMESTAMP_FILE")
    DIFF=$((CURRENT_TIME - LAST_CHECK))
    if [ $DIFF -lt $FIVE_HOURS ]; then
        exit 0
    fi
fi

# 타임스탬프 갱신
echo "$CURRENT_TIME" > "$TIMESTAMP_FILE"

# 기획서 링크 파일 존재 여부 확인
if [ ! -f "$DOCS_FILE" ] || [ ! -s "$DOCS_FILE" ]; then
    echo "[PLANNING_CHECK] 기획서 링크 파일($DOCS_FILE)이 비어있습니다. 링크를 추가하세요."
    exit 0
fi

echo "================================================================"
echo "[PLANNING_CHECK] 5시간 경과 — 기획서 자동 점검 시작"
echo "================================================================"
echo ""
echo "아래 기획서 링크들을 확인하고 미완료 항목의 작업을 계속 진행하세요:"
echo ""
cat "$DOCS_FILE"
echo ""
echo "현재 git 변경사항:"
cd "$(dirname "$0")/../.." && git status --short 2>/dev/null | head -20
echo "================================================================"
