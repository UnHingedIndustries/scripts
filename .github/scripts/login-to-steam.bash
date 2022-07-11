#!/usr/bin/env bash

set -e

echo "Generating steam auth code"
steamAuthCode="$(node "${GITHUB_WORKSPACE}/.github/scripts/generate-code.js")"

echo "Logging in to Steam"
steamcmd +login "${STEAM_USERNAME}" "${STEAM_PASSWORD}" "${steamAuthCode}" +exit