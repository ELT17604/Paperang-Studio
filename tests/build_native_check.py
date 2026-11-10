"""Compile actual application sources into the isolated native integration harness."""
from pathlib import Path
import subprocess, tempfile
r=Path(__file__).resolve().parents[1];b=r/'work/native/build/release';d=r/'work/native-check';d.mkdir(parents=True,exist_ok=True)
for p in (r/'Sources/PaperangLab').glob('*.swift'):
 s=p.read_text()
 if p.name=='App.swift':s=s.replace('@main enum PaperangEntry','enum PaperangEntry')
 (d/p.name).write_text(s)
(d/'Check.swift').write_text((r/'tests/native_check.swift').read_text())
args=['swiftc','-O','-whole-module-optimization','-parse-as-library','-swift-version','5','-module-cache-path',str(d/'modulecache'),'-I',str(b/'Modules'),'-I',str(b/'PTYBridge.build')]+list(map(str,d.glob('*.swift')))
for module in ['IntegrationCore','PrintCore','ProtocolCore','TerminalCore','PTYBridge']:args+=list(map(str,(b/(module+'.build')).glob('*.o')))
args+=['-o',str(d/'check')];subprocess.run(args,check=True)
print('PASS compiled actual Mac sources for isolated integration harness')
