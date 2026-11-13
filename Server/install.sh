#!/usr/bin/env bash
# Install from a local checkout; dedicated venv for authentication dependencies.
set -euo pipefail
SOURCE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PORT=8177 BIND=127.0.0.1 ANON_MAX=500 ANON_JOBS=5 ANON_CHARS=2000 AUTH_MAX=10000 AUTH_JOBS=60 AUTH_CHARS=60000
DEVICE=p2 PREFIX=/opt/paperang-relay CONFIG=/etc/paperang-relay/config.json STATE=/var/lib/paperang-relay
INTERACTIVE=1 START=1 DRY=0 RECONFIGURE=0 ANONYMOUS=true
PROXIES=()
usage()
{
	cat <<'HELP'
Usage: sudo bash Server/install.sh [options]
  --port N                       Listening port (1024–65535; default 8177)
  --bind ADDRESS                 IPv4 bind address (default 127.0.0.1)
  --anonymous-max-length N       Characters per anonymous message (default 500)
  --anonymous-hourly-limit N     Anonymous jobs per IP per UTC hour (default 5)
  --anonymous-hourly-chars N     Anonymous total characters/IP/hour (default 2000)
  --authenticated-max-length N   Key-authenticated message length (default 10000)
  --authenticated-hourly-limit N Key-authenticated jobs/IP/hour (default 60)
  --authenticated-hourly-chars N Key-authenticated characters/IP/hour (default 60000)
  --disable-anonymous            Require a submit key for every message
  --trusted-proxy CIDR           Trust this proxy for X-Forwarded-For (repeatable)
  --device-id ID                 Target printer ID (default p2)
  --prefix PATH                  Application directory (default /opt/paperang-relay)
  --config PATH                  Config file (default /etc/paperang-relay/config.json)
  --state-dir PATH               SQLite/credential directory (default /var/lib/paperang-relay)
  --non-interactive              Use flags/defaults without prompts
  --reconfigure                  Replace existing config (backup retained)
  --no-start                     Install without enabling/starting the service
  --dry-run                      Validate and show plan; make no changes
  --help                         Show help

Existing configs/keys are preserved unless --reconfigure is supplied.
Keys are generated locally; raw keys are written once to a root-only credentials file.
HELP
}
need_value()
{
	[[ $# -ge 2 && -n "$2" ]] || { echo "Missing value for $1" >&2;exit 2; }; }
while (($#)); do
	case "$1" in
	--help|-h) usage;exit 0;;
	--non-interactive) INTERACTIVE=0;shift;; --no-start) START=0;shift;; --dry-run) DRY=1;INTERACTIVE=0;shift;;
	--reconfigure) RECONFIGURE=1;shift;; --disable-anonymous) ANONYMOUS=false;shift;;
	--port|--bind|--anonymous-max-length|--anonymous-hourly-limit|--anonymous-hourly-chars|--authenticated-max-length|--authenticated-hourly-limit|--authenticated-hourly-chars|--device-id|--prefix|--config|--state-dir|--trusted-proxy)
		need_value "$@"
		case "$1" in
			--port) PORT=$2;; --bind) BIND=$2;; --anonymous-max-length) ANON_MAX=$2;;
			--anonymous-hourly-limit) ANON_JOBS=$2;; --anonymous-hourly-chars) ANON_CHARS=$2;;
			--authenticated-max-length) AUTH_MAX=$2;; --authenticated-hourly-limit) AUTH_JOBS=$2;; --authenticated-hourly-chars) AUTH_CHARS=$2;;
			--device-id) DEVICE=$2;; --prefix) PREFIX=$2;; --config) CONFIG=$2;; --state-dir) STATE=$2;; --trusted-proxy) PROXIES+=("$2");;
		esac;shift 2;;
	*) echo "Unknown option: $1" >&2;exit 2;;
	esac
done
if [[ $INTERACTIVE == 1 && -t 0 ]];then
	read -r -p "Listening IPv4 address [$BIND]: " reply; BIND=${reply:-$BIND}
	read -r -p "Listening port [$PORT]: " reply; PORT=${reply:-$PORT}
	read -r -p "Anonymous max characters/message [$ANON_MAX]: " reply; ANON_MAX=${reply:-$ANON_MAX}
	read -r -p "Anonymous jobs/IP/hour [$ANON_JOBS]: " reply; ANON_JOBS=${reply:-$ANON_JOBS}
	read -r -p "Anonymous total characters/IP/hour [$ANON_CHARS]: " reply; ANON_CHARS=${reply:-$ANON_CHARS}
	read -r -p "Authenticated jobs/IP/hour [$AUTH_JOBS]: " reply; AUTH_JOBS=${reply:-$AUTH_JOBS}
	read -r -p "Printer device ID [$DEVICE]: " reply; DEVICE=${reply:-$DEVICE}
fi
command -v python3 >/dev/null || { echo 'Install Python 3.10+ first: sudo apt-get install python3' >&2;exit 1; }
PLAN=$(python3 - "$PORT" "$BIND" "$ANON_MAX" "$ANON_JOBS" "$ANON_CHARS" "$AUTH_MAX" "$AUTH_JOBS" "$AUTH_CHARS" "$DEVICE" "$PREFIX" "$CONFIG" "$STATE" "$ANONYMOUS" "${PROXIES[@]:-}" <<'PY'
def require(condition, message):
    if not condition:
        raise SystemExit(message)
import sys, json, ipaddress, re, os
p, b, am, aj, ac, tm, tj, tc, d, prefix, config, state, anon, *proxies = sys.argv[1:]
require(sys.version_info >= (3, 10), 'Python 3.10+ required')
nums = [int(v) for v in [p, am, aj, ac, tm, tj, tc]]
require(all((v > 0 for v in nums)) and 1024 <= nums[0] <= 65535 and (nums[1] <= 10000) and (nums[4] <= 10000), 'Invalid port or limit')
require(ipaddress.ip_address(b).version == 4, 'Bind must be an IPv4 address')
require(re.fullmatch('[A-Za-z0-9_-]{1,64}', d), 'Invalid device ID')
for path in [prefix, config, state]:
    require(path.startswith('/') and all((v not in path for v in ['\n', '\r', ' ', '"', "'", '%', '\\'])) and ('..' not in path.split('/')), 'Install paths must be absolute, without spaces, parent traversal or systemd escapes')
protected = {'/', '/etc', '/usr', '/usr/local', '/usr/local/bin', '/usr/bin', '/usr/lib', '/var', '/var/lib', '/var/log', '/opt', '/srv', '/home', '/root', '/tmp', '/run', '/bin', '/sbin', '/lib', '/boot', '/dev', '/proc', '/sys', '/etc/systemd', '/etc/systemd/system'}
directories = [os.path.normpath(v) for v in [prefix, os.path.dirname(config), state]]
require(config.endswith('.json') and (not any((v in protected for v in directories))), 'Use dedicated code/config/data directories; config must end in .json')
for i, left in enumerate(directories):
    for right in directories[i + 1:]:
        require(os.path.commonpath([left, right]) not in [left, right], 'Code/config/data directories must not overlap')
proxies = [net for net in proxies if net]
for net in proxies:
    ipaddress.ip_network(net)
print(json.dumps(dict(bind=b, port=nums[0], database=state + '/relay.sqlite3', device_id=d, allow_anonymous=anon == 'true', anonymous_max_length=nums[1], anonymous_hourly_limit=nums[2], anonymous_hourly_chars=nums[3], authenticated_max_length=nums[4], authenticated_hourly_limit=nums[5], authenticated_hourly_chars=nums[6], trusted_proxies=proxies, queue_limit=200, retention_days=7), indent=2))
PY
)
if [[ $DRY == 1 ]];then
	echo "$PLAN"
	echo "Install: $PREFIX; config: $CONFIG; state: $STATE; systemd: paperang-relay.service"
	exit 0
fi
[[ $(uname -s) == Linux && -f /etc/os-release ]] || { echo 'This installer targets Ubuntu/systemd Linux.' >&2;exit 1; }
[[ $EUID == 0 ]] || { echo 'Run with sudo.' >&2;exit 1; }
command -v systemctl >/dev/null || { echo 'systemd is required.' >&2;exit 1; }
[[ -f "$SOURCE/relay.py" && -d "$SOURCE/web" ]] || { echo 'Keep installer beside relay.py and web/.' >&2;exit 1; }
getent passwd paperang-relay >/dev/null || useradd --system --user-group --home-dir "$STATE" --shell /usr/sbin/nologin paperang-relay
install -d -m 0755 "$PREFIX" "$PREFIX/web"
install -m 0644 "$SOURCE/relay.py" "$SOURCE/security.py" "$SOURCE/api_v2.py" "$SOURCE/requirements.txt" "$PREFIX/"
python3 -m venv "$PREFIX/venv"
"$PREFIX/venv/bin/python" -m pip install -r "$PREFIX/requirements.txt"
install -m 0644 "$SOURCE/web/"* "$PREFIX/web/"
install -d -m 0750 -o paperang-relay -g paperang-relay "$STATE"
install -d -m 0750 -o root -g paperang-relay "$(dirname -- "$CONFIG")"
if [[ ! -f $CONFIG || $RECONFIGURE == 1 ]];then
	if [[ -f $CONFIG ]];then cp -p -- "$CONFIG" "$CONFIG.backup.$(date -u +%Y%m%dT%H%M%SZ)";fi
	umask 027
	echo "$PLAN" > "$CONFIG"
	chown root:paperang-relay "$CONFIG";chmod 0640 "$CONFIG"
else
	echo "Preserving existing configuration: $CONFIG (use --reconfigure to change it)."
fi
# Only first install bootstraps keys; upgrades never regenerate or disclose old credentials.
if [[ ! -f "$STATE/.bootstrapped" ]];then
	umask 077
	CREDS="$STATE/initial-credentials.jsonl"
	for role in C M B;do
		runuser -u paperang-relay -- "$PREFIX/venv/bin/python" -B "$PREFIX/relay.py" --config "$CONFIG" key-add --role "$role" --name "initial-$role" >> "$CREDS"
	done
	touch "$STATE/.bootstrapped"
	chown paperang-relay:paperang-relay "$STATE/.bootstrapped"
	chown root:root "$CREDS";chmod 0600 "$CREDS"
	echo "Initial credentials: $CREDS (root-readable; contains admin/device/submit keys)."
fi
cat > /etc/systemd/system/paperang-relay.service <<EOF
[Unit]
Description=Paperang Studio relay
After=network.target
[Service]
Type=simple
User=paperang-relay
Group=paperang-relay
UMask=0077
ExecStart=$PREFIX/venv/bin/python -B $PREFIX/relay.py --config $CONFIG serve
WorkingDirectory=$PREFIX
Restart=on-failure
RestartSec=3
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=read-only
ReadWritePaths=$STATE
RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX
[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
if [[ $START == 1 ]];then systemctl enable paperang-relay;systemctl restart paperang-relay;systemctl is-active --quiet paperang-relay;fi
echo "Installed. Check: systemctl status paperang-relay; journalctl -u paperang-relay"
echo "Web address uses the bind/port in $CONFIG. Use HTTPS at your reverse proxy for public access."
