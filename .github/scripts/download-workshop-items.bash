#!/usr/bin/env bash

set -e

for rawScriptMetadata in $("${GITHUB_WORKSPACE}/.github/scripts/for-each-script.bash"); do
  readarray -d ';' -t scriptMetadata <<< "${rawScriptMetadata}"
  repositoryScriptFile="${scriptMetadata[0]}"
  scriptName="$(basename "${repositoryScriptFile}")"
  workshopFileId="${scriptMetadata[1]}"
  echo "Generating steam auth code"
  steamAuthCode="$(node "${GITHUB_WORKSPACE}/.github/scripts/generate-code.js")"

  echo "Downloading script ${scriptName}"
  steamcmd +login "${STEAM_USERNAME}" "${STEAM_PASSWORD}" "${steamAuthCode}" +workshop_download_item 244850 "${workshopFileId}" +exit
done