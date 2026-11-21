"""Guard local route/OpenAPI inventory and keep independent version domains."""

import json, re
from pathlib import Path

r = Path(__file__).resolve().parents[1]
s = (r / "Sources/PaperangLab/APIRoutes.swift").read_text(encoding="utf-8")
d = json.loads((r / "doc/openapi.json").read_text(encoding="utf-8"))
paths = set(re.findall(r'case "(/v2/[^\"]+)"', s))
declared = set(d["paths"])
assert declared == paths | {"/v2/jobs/{id}", "/v2/jobs/{id}/cancel"}, (paths, declared)
assert all(p.startswith("/v2/") for p in declared)
assert d["components"]["schemas"]["Status"]["properties"]["apiVersion"]["enum"] == ["v2"]
for p in [r / "Sources/PaperangLab/APIRoutes.swift", *list((r / "Clients").glob("*"))]:
	if p.is_file():
		assert not re.search(r"[\"\x27](/v1/[^\"\x27]+)[\"\x27]", p.read_text(encoding="utf-8")), p
v = json.loads((r / "VERSIONS.json").read_text(encoding="utf-8"))
assert v["compatibility"]["contentSchemaVersion"] == v["compatibility"]["templateSchemaVersion"] == 1
assert (r / "VERSION").read_text(encoding="utf-8").strip() == d["info"]["version"]
assert v["baseline"] == "0.3.x"
assert v["compatibility"]["localApi"] == v["compatibility"]["relayApi"] == "v2"
print("PASS local implementation/OpenAPI route inventory, product/API/schema domains")

for p in (r / "Windows/src").rglob("*.cs"):
	# Self-checks intentionally call v1 to verify HTTP 410; dispatchers must also
	# recognize retired routes. Reject actual outbound calls in production code.
	if p.name.endswith("Checks.cs"):
		continue
	assert not re.search(r'(?:\b(?:Get|Post|RequestAsync|GetAsync|PostAsync|PutAsync|DeleteAsync)\s*\(|'
	                     r'\bnew\s+HttpRequestMessage\s*\(\s*HttpMethod\.\w+\s*,)\s*'
	                     r'(?:HttpMethod\.\w+\s*,\s*)?["\x27]/?v1/', p.read_text(encoding="utf-8")), p
print("PASS Windows runtime has no HTTP v1 route calls; schema 1 remains supported")

android_layout = (r / "Android/src/com/elt/pprstudio/adc/PaperRenderer.java").read_text(encoding="utf-8")
assert re.search(r"LayoutOptions DEFAULT = new LayoutOptions\(0, 0, WIDTH,", android_layout)
android_raster = (r / "Android/src/com/elt/pprstudio/adc/Raster.java").read_text(encoding="utf-8")
assert re.search(r"\bWIDTH\s*=\s*576\b", android_raster)
print("PASS Android default layout uses the full 576-dot width with zero margins")
