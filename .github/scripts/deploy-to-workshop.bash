#!/usr/bin/env bash

set -e

for rawScriptMetadata in $("${GITHUB_WORKSPACE}/.github/scripts/for-each-script.bash"); do
  readarray -d ';' -t scriptMetadata <<< "${rawScriptMetadata}"
  repositoryScriptFile="${scriptMetadata[0]}"
  repositoryScriptPath="$(dirname "${repositoryScriptFile}")"
  scriptName="$(basename "${repositoryScriptFile}" '.cs')"
  workshopFileId="${scriptMetadata[1]}"
  scriptVersion="${scriptMetadata[2]}"
  workshopItemPath="${HOME}/Steam/steamapps/workshop/content/244850/${workshopFileId}"
  oldScriptFile="${workshopItemPath}/Script.cs"
  newScriptFile="${workshopItemPath}/NewScript.cs"
  oldThumbnailFile="${workshopItemPath}/thumb.png"
  newThumbnailFile="${repositoryScriptPath}/thumb.png"

  # Take only the part of the script that is needed in Programmable Block
  grep '        ' "${repositoryScriptFile}" | cut -c 9- > "${newScriptFile}" 
  
  if cmp -s "${oldScriptFile}" "${newScriptFile}"; then
      echo "No changes detected for ${repositoryScriptFile}, skipping deployment"
  else
      echo "Changes detected for ${repositoryScriptFile}, deploying"
      
      echo "Replacing script file ${oldScriptFile} with ${newScriptFile}"
      rm "${oldScriptFile}"
      mv "${newScriptFile}" "${oldScriptFile}"

      echo "Replacing thumbnail file ${oldThumbnailFile} with ${newThumbnailFile}"
      rm "${oldThumbnailFile}"
      cp "${newThumbnailFile}" "${oldThumbnailFile}"

      echo "Generating VDF file"
      vdfFilePath="${repositoryScriptPath}/upload_item.vdf"
      cat >> "${vdfFilePath}" <<EOF
"workshopitem"
{
 "appid" "244850"
 "publishedfileid" "${workshopFileId}"
 "contentfolder" "${workshopItemPath}"
 "previewfile" "${repositoryScriptPath}/thumb.png"
 "title" "[UHI] ${scriptName} ${scriptVersion}"
 "changenote" "Deploy version ${scriptVersion} from GitHub Actions ${GITHUB_SHA}"
}
EOF
      echo "VDF file generated $(cat "${vdfFilePath}")"

      echo "Generating steam auth code"
      steamAuthCode="$(node "${GITHUB_WORKSPACE}/.github/scripts/generate-code.js")"

      echo "Starting upload to Steam Workshop"
      ls -lha "${workshopItemPath}"
      steamcmd +login "${STEAM_USERNAME}" "${STEAM_PASSWORD}" "${steamAuthCode}" +workshop_build_item "${vdfFilePath}" +exit
  fi
done