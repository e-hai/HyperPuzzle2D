#!/usr/bin/env bash
# Package HyperPuzzle2D for a target platform.
# Usage: tools/build.sh android|ios|mac
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/HyperPuzzle2D"
UNITY="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity}"
TARGET="${1:-android}"
LOG="$ROOT/Builds/build-${TARGET}.log"
mkdir -p "$ROOT/Builds"

case "$TARGET" in
  android) METHOD="HyperPuzzle2D.Editor.BuildPlayer.BuildAndroid" ;;
  ios) METHOD="HyperPuzzle2D.Editor.BuildPlayer.BuildIOS" ;;
  mac) METHOD="HyperPuzzle2D.Editor.BuildPlayer.BuildMac" ;;
  *) echo "Usage: $0 android|ios|mac"; exit 2 ;;
esac

if [[ ! -x "$UNITY" ]]; then
  echo "Unity editor not found at $UNITY"
  exit 1
fi

echo "Building $TARGET via $METHOD"
echo "Log: $LOG"
"$UNITY" -batchmode -nographics -quit \
  -projectPath "$PROJECT" \
  -executeMethod "$METHOD" \
  -logFile "$LOG"

echo "Done. See Builds/ and $LOG"
