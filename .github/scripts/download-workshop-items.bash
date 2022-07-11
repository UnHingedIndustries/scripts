#!/usr/bin/env bash

set -e

steamAppId='244850'

for rawScriptMetadata in $("${GITHUB_WORKSPACE}/.github/scripts/for-each-script.bash"); do
  readarray -d ';' -t scriptMetadata <<< "${rawScriptMetadata}"
  repositoryScriptFile="${scriptMetadata[0]}"
  scriptName="$(basename "${repositoryScriptFile}")"
  workshopFileId="${scriptMetadata[2]}"

  echo "Downloading script ${scriptName}"
  steamcmd +login "${STEAM_USERNAME}" +workshop_download_item "${steamAppId}" "${workshopFileId}" +exit
done