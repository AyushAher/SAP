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

  # Host .env files sometimes omit the Npgsql Host= prefix (e.g. 192.168.0.5;Port=5432;...).
  if [[ -n "${DB_CONNECTION:-}" ]] && [[ ! "$DB_CONNECTION" =~ Host= ]]; then
    export DB_CONNECTION="Host=${DB_CONNECTION}"
  fi

  # Passwords with @ must be quoted or Npgsql fails to parse the connection string.
  if [[ -n "${DB_CONNECTION:-}" ]] && [[ "$DB_CONNECTION" =~ Password=([^;]+) ]]; then
    local pass="${BASH_REMATCH[1]}"
    if [[ "$pass" == *@* ]] && [[ "$pass" != "'"* ]]; then
      export DB_CONNECTION="${DB_CONNECTION//Password=${pass}/Password='${pass}'}"
    fi
  fi

  # Normalize legacy spaced Npgsql keys that break System.Data connection string parsing.
  DB_CONNECTION="${DB_CONNECTION//Connection Idle Lifetime=/ConnectionIdleLifetime=}"
  DB_CONNECTION="${DB_CONNECTION//Connection Pruning Interval=/ConnectionPruningInterval=}"
  export DB_CONNECTION
}

# Rebuild a minimal Npgsql connection string from key parts (avoids parser issues in host .env files).
normalize_db_connection() {
  local cs="$1"
  local host="" port="" db="" user="" pass=""
  if [[ "$cs" =~ Host=([^;]+) ]]; then host="${BASH_REMATCH[1]}"; fi
  if [[ "$cs" =~ Port=([^;]+) ]]; then port="${BASH_REMATCH[1]}"; fi
  if [[ "$cs" =~ Database=([^;]+) ]]; then db="${BASH_REMATCH[1]}"; fi
  if [[ "$cs" =~ Username=([^;]+) ]]; then user="${BASH_REMATCH[1]}"; fi
  if [[ "$cs" =~ Password=([^;]+) ]]; then
    pass="${BASH_REMATCH[1]}"
    pass="${pass#\'}"; pass="${pass%\'}"
    pass="${pass#\"}"; pass="${pass%\"}"
  fi
  if [[ -z "$host" || -z "$db" || -z "$user" ]]; then
    echo "$cs"
    return
  fi
  [[ -z "$port" ]] && port="5432"
  printf 'Host=%s;Port=%s;Database=%s;Username=%s;Password=%s' "$host" "$port" "$db" "$user" "$pass"
}
