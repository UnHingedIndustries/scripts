#!/usr/bin/env bash

for directory in "${GITHUB_WORKSPACE}"/UnHingedIndustries/*/; do
  scriptFilePath="$(find "${directory}" -name '*.cs')"
  workshopItemId="$(grep -ohE 'WorkshopItemId = \"[0-9]+\"' "${scriptFilePath}" | grep -oE '[0-9]+')"
  modIoItemId="$(grep -ohE 'ModIoItemId = \"[0-9]+\"' "${scriptFilePath}" | grep -oE '[0-9]+')"
  scriptVersion="$(grep -ohE 'ScriptVersion = \"[0-9]+\.[0-9]+\.[0-9]+\"' "${scriptFilePath}" | grep -oE '[0-9]+\.[0-9]+\.[0-9]+')"
  echo "${scriptFilePath};${scriptVersion};${workshopItemId};${modIoItemId};"
done