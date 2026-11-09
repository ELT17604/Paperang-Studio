"""Guard local route/OpenAPI inventory and keep independent version domains."""
import json,re
from pathlib import Path
r=Path(__file__).resolve().parents[1]
s=(r/'Sources/PaperangLab/APIRoutes.swift').read_text();d=json.loads((r/'doc/openapi.json').read_text())
paths=set(re.findall(r'case "(/v2/[^\"]+)"',s));declared=set(d['paths'])
assert declared==paths|{'/v2/jobs/{id}','/v2/jobs/{id}/cancel'},(paths,declared)
assert all(p.startswith('/v2/') for p in declared)
assert d['components']['schemas']['Status']['properties']['apiVersion']['enum']==['v2']
for p in [r/'Sources/PaperangLab/APIRoutes.swift',*list((r/'Clients').glob('*'))]:
 if p.is_file():assert not re.search(r'[\"\x27](/v1/[^\"\x27]+)[\"\x27]',p.read_text()),p
v=json.loads((r/'VERSIONS.json').read_text());assert v['compatibility']['contentSchemaVersion']==v['compatibility']['templateSchemaVersion']==1
assert (r/'VERSION').read_text().strip()==d['info']['version']==v['baseline']=='0.3.2'
assert v['compatibility']['localApi']==v['compatibility']['relayApi']=='v2'
print('PASS local implementation/OpenAPI route inventory, product/API/schema domains')
