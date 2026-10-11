#!/usr/bin/env bash
# 캠페인 맵을 MapSolver로 풀어 0데스 입력 대본을 출력한다 (CampaignSolutions.cs에 붙여 넣기).
#   필요: mono   사용: tools/solve-campaign.sh [월드] [스테이지]
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${TMPDIR:-/tmp}/mds-solver"
mkdir -p "$OUT"
mcs -langversion:7 -optimize+ -nowarn:1591 -out:"$OUT/SolveCampaign.exe" \
    "$ROOT"/Assets/Scripts/Game/MillionDeathSquare/Core/*.cs "$ROOT"/tools/campaign-solver/SolveCampaign.cs
mono "$OUT/SolveCampaign.exe" "$@"
