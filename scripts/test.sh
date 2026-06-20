#!/bin/zsh
# Runs dotnet test with Colima started and Docker env vars configured.
# Usage: ./scripts/test.sh [dotnet-test-args...]

set -e

./scripts/with-docker.sh dotnet test "$@"
