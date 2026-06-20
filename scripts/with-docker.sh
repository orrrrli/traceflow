#!/bin/zsh
# Ensures Colima is running, exports Docker/Testcontainers env vars, and executes the given command.
# Usage: ./scripts/with-docker.sh <command> [args...]

set -e

if ! colima status >/dev/null 2>&1; then
  echo "Colima is not running. Starting it now..."
  colima start
fi

export DOCKER_HOST="unix://${HOME}/.colima/default/docker.sock"
export TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE="/var/run/docker.sock"

exec "$@"
