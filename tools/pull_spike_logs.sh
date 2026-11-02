#!/usr/bin/env bash
# Kept for the Day 1 runbook: same as tools/pull_logs.sh spike [tester]
exec "$(dirname "$0")/pull_logs.sh" spike "$@"
