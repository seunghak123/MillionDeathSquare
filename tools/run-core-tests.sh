#!/usr/bin/env bash
# 결정적 코어(Assets/Scripts/Game/MillionDeathSquare/Core) + EditMode 테스트를 Unity 없이 컴파일·실행한다.
#   필요: mono (mcs, mono)   사용: tools/run-core-tests.sh [이름 필터]
# NUnit 대신 tools/core-test-runner/NUnitShim.cs의 최소 구현을 쓴다.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${TMPDIR:-/tmp}/mds-core-tests"
mkdir -p "$OUT"

CORE=("$ROOT"/Assets/Scripts/Game/MillionDeathSquare/Core/*.cs)
TESTS=("$ROOT"/Assets/Tests/EditMode/*.cs)
RUNNER=("$ROOT"/tools/core-test-runner/*.cs)

# 1) 코어만 따로 컴파일 — UnityEngine 없이, C# 7 문법으로 빌드되는지 확인
mcs -langversion:7 -target:library -nowarn:1591 -out:"$OUT/MillionDeathSquare.Core.dll" "${CORE[@]}"

# 2) 테스트 + 러너
mcs -langversion:7 -target:exe -nowarn:1591 -r:"$OUT/MillionDeathSquare.Core.dll" -out:"$OUT/CoreTests.exe" "${TESTS[@]}" "${RUNNER[@]}"

cd "$OUT"
mono CoreTests.exe "$@"
