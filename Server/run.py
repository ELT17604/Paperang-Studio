#!/usr/bin/env python3
"""Foreground temporary relay; no system installation, persistent keys or daemon."""
import argparse,json,signal,tempfile,threading,ipaddress,sys
from pathlib import Path
from relay import Store,Server,DEFAULTS,valid_device

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--port',type=int,default=8177);p.add_argument('--bind',default='127.0.0.1');p.add_argument('--device-id',default='p2')
    p.add_argument('--disable-anonymous',action='store_true');p.add_argument('--trusted-proxy',action='append',default=[])
    for name in ['anonymous-max-length','anonymous-hourly-limit','anonymous-hourly-chars','authenticated-max-length','authenticated-hourly-limit','authenticated-hourly-chars']:
        p.add_argument('--'+name,type=int,default=DEFAULTS[name.replace('-','_')])
    p.add_argument('--dry-run',action='store_true',help='Show settings without opening a port or generating keys')
    a=p.parse_args()
    if sys.version_info<(3,10):p.error('Python 3.10+ required')
    if not 1024<=a.port<=65535 or not valid_device(a.device_id):p.error('Invalid port or device ID')
    try:
        if ipaddress.ip_address(a.bind).version!=4:raise ValueError()
        for net in a.trusted_proxy:ipaddress.ip_network(net)
    except ValueError:p.error('Invalid IPv4 bind address or proxy CIDR')
    for name in ['anonymous_max_length','anonymous_hourly_limit','anonymous_hourly_chars','authenticated_max_length','authenticated_hourly_limit','authenticated_hourly_chars']:
        value=getattr(a,name)
        if value<1 or (name.endswith('max_length') and value>10000):p.error('Invalid '+name)
    config=vars(a).copy();config.pop('dry_run');config['allow_anonymous']=not config.pop('disable_anonymous');config['trusted_proxies']=config.pop('trusted_proxy')
    if a.dry_run:
        print(json.dumps(config,indent=2));return
    with tempfile.TemporaryDirectory(prefix='paperang-relay-') as directory:
        config['database']=directory+'/relay.sqlite3';store=Store(config)
        credentials=[store.key_add('temporary-'+role,role,config['device_id']) for role in ['admin','device','submit']]
        path=Path(directory)/'credentials.keys.json';path.write_text(json.dumps(credentials,indent=2));path.chmod(0o600)
        try:server=Server(store)
        except OSError as e:raise SystemExit(f'Cannot listen: {e}. Choose another --port.')
        def stop(*_):threading.Thread(target=server.shutdown,daemon=True).start()
        for name in ['SIGTERM','SIGHUP']:
            if hasattr(signal,name):signal.signal(getattr(signal,name),stop)
        print(f'Temporary webpage: http://{config["bind"]}:{config["port"]}',flush=True)
        print('Device key (enter in the Mac app): '+credentials[1]['token'],flush=True)
        print('Submission key (optional for a named sender): '+credentials[2]['token'],flush=True)
        print(f'All temporary credentials: {path}\nPress Ctrl-C or close this terminal to stop. Data/keys are discarded.',flush=True)
        try:server.serve_forever(poll_interval=.25)
        except KeyboardInterrupt:pass
        finally:server.server_close()
    print('Temporary relay stopped; temporary state removed.',flush=True)
if __name__=='__main__':main()
