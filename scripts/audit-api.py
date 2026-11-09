#!/usr/bin/env python3
"""Inventory version domains across the Stage 1 baseline's tracked text files."""
from pathlib import Path
import collections,json,re,subprocess
root=Path(__file__).resolve().parents[1]
start='ada72fac6e283409a3e28577c1d6bcb257ab4eae'
files=subprocess.check_output(['git','ls-tree','-r','--name-only','-z',start],cwd=root).decode().split('\0')
pattern=re.compile(r'/v[12]|api/v[12]|API_VERSION|localApi|relayApi|schemaVersion|deprecated|compatib',re.I)
rows=[]
for name in filter(None,files):
 try:content=(root/name).read_text()
 except (UnicodeError,IsADirectoryError):continue
 for line,text in enumerate(content.splitlines(),1):
  if not pattern.search(text):continue
  if 'schemaVersion' in text:category='data-schema (preserved)'
  elif '/v1' in text:category='HTTP v1 retirement' if name.startswith(('Server/','Sources/')) else ('HTTP v1 historical record' if any(x in name for x in ['UPGRADE','HANDOFF','VALIDATION','OPTIMIZATION','CHANGELOG','MACCLIENT','AUDIT','AUTH-v2']) else 'HTTP v1 documentation reference')
  elif '/v2' in text or 'relayApi' in text or 'localApi' in text:category='HTTP v2'
  elif name.startswith('Sources/ProtocolCore'):category='physical protocol (preserved)'
  else:category='compatibility/version/history review'
  rows.append(dict(file=name,line=line,category=category,text=text.strip()))
result=dict(startSHA=start,scope=f'All {len(list(filter(None,files)))} tracked baseline files read; binary assets excluded from text matching. Inventory generated after migration. New contract/tests reviewed separately.',counts=dict(collections.Counter(x['category'] for x in rows)),matches=rows)
(root/'doc/API-AUDIT-v0.3.2.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n')
print('PASS API audit inventory:',len(rows),'matches across baseline files')
