#!/usr/bin/env bash

set -e

function generateSteamWorkshopChangelog() {
  changelogFile="${1}"
  echo 'Changes:'
  echo '[list]'
  
  while IFS='' read -r line || [ "${line}" ]; do
    [ -z "${line}" ] && continue
    echo "[*] ${line}"
  done < "${changelogFile}"
  
  echo '[/list]'
}

function generateModIoChangelog() {
  changelogFile="${1}"
  echo -n 'Changes:<br>'
  echo -n '<ul>'
  
  while IFS='' read -r line || [ "${line}" ]; do
    [ -z "${line}" ] && continue
    echo -n "<li>${line}</li>"
  done < "${changelogFile}"
  
  echo -n '</ul>'
}

steamAppId='244850'
modIoAppId='264'

for rawScriptMetadata in $("${GITHUB_WORKSPACE}/.github/scripts/for-each-script.bash"); do
  readarray -d ';' -t scriptMetadata <<< "${rawScriptMetadata}"
  repositoryScriptFile="${scriptMetadata[0]}"
  repositoryScriptPath="$(dirname "${repositoryScriptFile}")"
  scriptName="$(basename "${repositoryScriptFile}" '.cs')"
  workshopFileId="${scriptMetadata[2]}"
  modIoFileId="${scriptMetadata[3]}"
  scriptVersion="${scriptMetadata[1]}"
  workshopAppPath="${HOME}/Steam/steamapps/workshop/content/244850"
  workshopItemPath="${workshopAppPath}/${workshopFileId}"
  oldScriptFile="${workshopItemPath}/Script.cs"
  newScriptFile="${workshopItemPath}/NewScript.cs"
  oldThumbnailFile="${workshopItemPath}/thumb.png"
  newThumbnailFile="${repositoryScriptPath}/thumb.png"
  newScriptTitle="[UHI] ${scriptName} ${scriptVersion}"
  changelogFile="${repositoryScriptPath}/changelog.txt"

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
      steamWorkshopChangelog="$(generateSteamWorkshopChangelog "${changelogFile}")"
      vdfFilePath="${repositoryScriptPath}/upload_item.vdf"
      cat >> "${vdfFilePath}" <<EOF
"workshopitem"
{
 "appid" "${steamAppId}"
 "publishedfileid" "${workshopFileId}"
 "contentfolder" "${workshopItemPath}"
 "previewfile" "${repositoryScriptPath}/thumb.png"
 "title" "${newScriptTitle}"
 "changenote" "Deploy version ${scriptVersion} built from [url=${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}/commit/${GITHUB_SHA}]${GITHUB_SHA}[/url]

${steamWorkshopChangelog}"
}
EOF
      echo "VDF file generated $(cat "${vdfFilePath}")"

      echo "Starting upload to Steam Workshop"
      ls -lha "${workshopItemPath}"
      steamcmd +login "${STEAM_USERNAME}" +workshop_build_item "${vdfFilePath}" +exit

      compressedFilePath="${workshopAppPath}/${scriptName}-${scriptVersion}.zip"
      echo "Generating ZIP archive for mod.io: ${compressedFilePath}"
      zip -r -j "${compressedFilePath}" "${workshopItemPath}"

      echo "Starting upload to mod.io"
      modIoChangelog="$(generateModIoChangelog "${changelogFile}")"
      curl -X POST "https://api.mod.io/v1/games/${modIoAppId}/mods/${modIoFileId}/files" \
        -H "Authorization: Bearer ${MOD_IO_ACCESS_TOKEN}" \
        -H 'Content-Type: multipart/form-data' \
        -H 'Accept: application/json' \
        -F "filedata=@${compressedFilePath}" \
        -F "version=${scriptVersion}" \
        -F "changelog=Deploy version ${scriptVersion} built from <a href=\"${GITHUB_SERVER_URL}/${GITHUB_REPOSITORY}/commit/${GITHUB_SHA}\">${GITHUB_SHA}</a><br><br>${modIoChangelog}"

      echo "Upload complete, changing script name in mod.io to ${newScriptTitle}"
      curl -X PUT "https://api.mod.io/v1/games/${modIoAppId}/mods/${modIoFileId}" \
        -H "Authorization: Bearer ${MOD_IO_ACCESS_TOKEN}" \
        -H 'Content-Type: application/x-www-form-urlencoded' \
        -H 'Accept: application/json' \
        -d "name=${newScriptTitle}"

      echo "Successfully uploaded ${scriptName} version ${scriptVersion}!"
  fi
done