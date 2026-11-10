"""Explicit native installer acceptance; preserves all existing per-user data.
Run only when installation/upgrade testing on this Windows user is authorized.
"""
import argparse, hashlib, os, subprocess, time, winreg
from pathlib import Path

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--setup', type=Path, required=True)
    parser.add_argument('--log-dir', type=Path, required=True)
    args=parser.parse_args();args.log_dir.mkdir(parents=True,exist_ok=True)
    local=Path(os.environ['LOCALAPPDATA']);data=local/'ELT/PaperangStudioWinLite'
    install=local/'Programs/Paperang Studio WinLite';app=install/'PaperangStudio.exe'
    registry=r'Software\Microsoft\Windows\CurrentVersion\Uninstall\{BF30D1A7-D03B-434C-A51B-718507439EC8}_is1'
    run=r'Software\Microsoft\Windows\CurrentVersion\Run'
    def snapshot():
        return {str(p.relative_to(data)):hashlib.sha256(p.read_bytes()).hexdigest() for p in data.rglob('*') if p.is_file()}
    def startup():
        try:
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER,run) as key:return winreg.QueryValueEx(key,'PaperangStudioWinLite')[0]
        except FileNotFoundError:return None
    original_startup=startup()
    assert not (data/'pending.dat').exists(),'Pending result must be reported before installer testing'
    before=snapshot()
    def setup(label):
        subprocess.run([str(args.setup.resolve()),'/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-','/LOG='+str((args.log_dir/(label+'.log')).resolve())],check=True,timeout=120)
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,registry) as key:
            assert winreg.QueryValueEx(key,'DisplayVersion')[0]==(Path(__file__).resolve().parents[2]/'Windows/VERSION').read_text(encoding='utf-8').strip()
        assert snapshot()==before,'Installer changed private data'
    def start():
        process=subprocess.Popen([str(app),'--background'],creationflags=subprocess.CREATE_NO_WINDOW)
        time.sleep(4);assert process.poll() is None,'Tray process exited'
        assert snapshot()==before,'Tray startup changed saved data'
        return process
    def stop(process):
        subprocess.run([str(app),'--shutdown'],check=True,timeout=15)
        assert process.wait(timeout=20)==0
    old=start()
    setup('upgrade-from-installed')
    assert old.wait(timeout=20)==0
    print('PASS existing installed client graceful upgrade; private data byte-identical')
    process=start();setup('overlay');assert process.wait(timeout=20)==0
    print('PASS running tray overlay and uninstall version registration')
    process=start()
    with winreg.OpenKey(winreg.HKEY_CURRENT_USER,registry) as key:
        uninstaller=winreg.QueryValueEx(key,'UninstallString')[0].strip('"')
    try:
        subprocess.run([uninstaller,'/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART'],check=True,timeout=120)
        process.wait(timeout=20)
        # Inno's uninstall launcher can exit before the spawned uninstaller finishes.
        deadline=time.monotonic()+30
        while time.monotonic()<deadline and (app.exists() or startup() is not None): time.sleep(.2)
        assert not app.exists() and snapshot()==before
        assert startup() is None
        print('PASS silent uninstall stops tray, removes startup registration, preserves all user data')
    finally:
        # Restore the installed payload and original startup preference even when an assertion fails.
        setup('clean-payload-reinstall')
        if original_startup is not None:
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER,run) as key:winreg.SetValueEx(key,'PaperangStudioWinLite',0,winreg.REG_SZ,original_startup)
    process=start();stop(process)
    assert startup()==original_startup and snapshot()==before
    print('PASS clean payload reinstall, restart and original startup preference retained')
    print('NOTE this is not a clean Windows user/VM; private user data intentionally retained')

if __name__=='__main__':main()
