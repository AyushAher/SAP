#!/usr/bin/env bash
# Load key=value host env files without breaking on semicolons in values (e.g. DB_CONNECTION).

# Strip surrounding/mismatched quotes from .env values (e.g. REDIS_CONNECTION="...false").
strip_env_value() {
  local val="$1"
  val="${val//$'\ufeff'/}"
  val="${val#"${val%%[![:space:]]*}"}"
  val="${val%"${val##*[![:space:]]}"}"
  if [[ ${#val} -ge 2 ]]; then
    local first="${val:0:1}" last="${val: -1}"
    if [[ "$first" == "$last" ]] && { [[ "$first" == '"' ]] || [[ "$first" == "'" ]]; }; then
      val="${val:1:${#val}-2}"
    fi
  fi
  while [[ "$val" == \"* ]] || [[ "$val" == *\" ]] || [[ "$val" == \'* ]] || [[ "$val" == *\' ]]; do
    val="${val#\"}"; val="${val%\"}"
    val="${val#\'}"; val="${val%\'}"
  done
  printf '%s' "$val"
}

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
      local key="${BASH_REMATCH[1]}"
      local value
      value="$(strip_env_value "${BASH_REMATCH[2]}")"
      export "${key}=${value}"
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

  if [[ -n "${REDIS_CONNECTION:-}" ]]; then
    export REDIS_CONNECTION="$(normalize_redis_connection "$REDIS_CONNECTION")"
  fi
}

# Validate and normalize StackExchange.Redis connection strings from host .env files.
normalize_redis_connection() {
  local cs
  cs="$(strip_env_value "$1")"
  if [[ "$cs" =~ abortConnect=([^,;]+) ]]; then
    local raw_ac="${BASH_REMATCH[1]}"
    local ac
    ac="$(strip_env_value "$raw_ac")"
    if [[ "$ac" != "true" && "$ac" != "false" ]]; then
      echo "normalize_redis_connection: invalid abortConnect value '${ac}' (expected true or false)" >&2
      return 1
    fi
    cs="${cs/abortConnect=${raw_ac}/abortConnect=${ac}}"
  fi
  if [[ "$cs" == *\"* ]] || [[ "$cs" == *\'* ]]; then
    echo "normalize_redis_connection: connection string still contains quotes after normalization" >&2
    return 1
  fi
  printf '%s' "$cs"
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
