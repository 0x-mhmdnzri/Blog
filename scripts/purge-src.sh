#!/usr/bin/env bash
# Removes the obsolete n-tier src/ tree. BlogApp is the only project.
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -d src ]]; then
  echo "Removing src/ (class libraries — not part of the monolith build)..."
  rm -rf src
  git add -A
  git status
  echo ""
  echo "Commit with:"
  echo "  git commit -m 'chore: remove src/ — pure BlogApp monolith only'"
  echo "  git push origin dev"
else
  echo "src/ already gone — pure monolith."
fi
