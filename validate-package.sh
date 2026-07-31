#!/usr/bin/env bash
set -euo pipefail

missing=0
while IFS= read -r path; do
  case "$path" in
    ./.git/*|./.DS_Store|./validate-package.sh)
      continue
      ;;
    .|./.git|./.gitignore|./.gitignore.meta|./validate-package.sh.meta)
      continue
      ;;
    ./*.meta|./Editor/*.meta|./Runtime/*.meta)
      continue
      ;;
    *.pdb|*.pdb.meta)
      continue
      ;;
  esac

  if [ ! -e "$path.meta" ]; then
    echo "missing meta: ${path#./}"
    missing=1
  fi
done < <(find . -maxdepth 2 \( -type f -o -type d \) | sort)

if [ "$missing" -ne 0 ]; then
  exit 1
fi

grep -q "Editor: Editor" Editor/GpuParticle.Editor.dll.meta
grep -q "enabled: 1" Editor/GpuParticle.Editor.dll.meta
grep -q "Any:" Runtime/GpuParticle.Runtime.dll.meta
grep -q "enabled: 1" Runtime/GpuParticle.Runtime.dll.meta
echo "package validation passed"
