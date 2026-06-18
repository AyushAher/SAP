#!/usr/bin/env bash
# Load key=value host env files without breaking on semicolons in values (e.g. DB_CONNECTION).
load_host_env_file() {
  local file="$1"
  if [ ! -f "$file" ]; then
    echo "load_host_env_file: file not found: $file" >&2
    return 1
  fi

  while IFS= read -r line || [ -n "$line" ]; do
    line="${line%$'\r'}"
    [[ "$line" =~ ^[[:space:]]*# ]] && continue
    [[ -z "${line//[[:space:]]/}" ]] && continue
    if [[ "$line" =~ ^([A-Za-z_][A-Za-z0-9_]*)=(.*)$ ]]; then
      export "${BASH_REMATCH[1]}=${BASH_REMATCH[2]}"
    fi
  done < "$file"
}
