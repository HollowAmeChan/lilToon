"""Stage specialization checks into an explicitly supplied, closed private project."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

p = argparse.ArgumentParser()
p.add_argument("--project", type=Path, required=True)
p.add_argument("--package", type=Path, required=True)
a = p.parse_args()
source = Path(__file__).resolve().parents[2] / "Assets/lilToon"
project, package = a.project.resolve(), a.package.resolve()
if (project / "Temp/UnityLockfile").exists():
    raise SystemExit("Close the isolated project first.")
if source.resolve() == package or source.resolve() in package.parents:
    raise SystemExit("Use a separate physical package copy.")
if not package.is_dir() or package.is_symlink() or package.is_junction():
    raise SystemExit("The existing private package must be an ordinary directory.")
manifest = json.loads((project / "Packages/manifest.json").read_text(encoding="utf-8-sig"))
if manifest["dependencies"]["jp.lilxyzw.liltoon"].replace("\\", "/") != "file:" + package.as_posix():
    raise SystemExit("Manifest does not resolve to the supplied private package.")
files = []
for path in source.rglob("*"):
    if not path.is_file():
        continue
    dest = package / path.relative_to(source)
    if dest.resolve() != dest.absolute():
        raise SystemExit("Linked destination: " + str(dest))
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, dest)
    files.append({"path": path.relative_to(source).as_posix(),
                  "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
# An overlay copy must explicitly remove known retired assets.
for name in ("Includes/openlit_core.hlsl", "Includes/lil_replace_keywords.hlsl", "ltspass_dummy.shader"):
    for suffix in ("", ".meta"):
        (package / "Shader" / (name + suffix)).unlink(missing_ok=True)
dest = project / "Assets/Editor/HoSpecializationTests"
dest.mkdir(parents=True, exist_ok=True)
for name in ("HoSpecializationValidation.cs", "HoSpecializationTests.asmdef"):
    shutil.copy2(Path(__file__).with_name(name), dest)
(dest / "source-manifest.json").write_text(json.dumps({"files": files}, indent=2) + "\n")
print(json.dumps({"files": len(files), "project": str(project), "package": str(package)}))
