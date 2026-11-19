from pathlib import Path
from sys import argv

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont


root = Path(argv[1])
source = Path(argv[2])
destination = Path(argv[3])
characters = set(range(0x20, 0x7F))
for folder in ("Editor", "Shader"):
    for path in (root / folder).rglob("*"):
        if path.suffix in (".cs", ".shader") or path.name in ("en-US.po", "zh-Hans.po"):
            characters.update(ord(char) for char in path.read_text(encoding="utf-8-sig") if ord(char) > 127)

font = TTFont(source, recalcTimestamp=False)
font = instantiateVariableFont(font, {"wght": 400}, inplace=True)
family = "Ho lilToon Inspector Sans"
for name in list(font["name"].names):
    text = {
        1: family,
        2: "Regular",
        3: "HoLilToonInspectorSans-Regular-1.0",
        4: family + " Regular",
        6: "HoLilToonInspectorSans-Regular",
        16: family,
        17: "Regular",
    }.get(name.nameID)
    if text is not None:
        font["name"].setName(text, name.nameID, name.platformID, name.platEncID, name.langID)

options = subset.Options()
options.layout_features = []
options.drop_tables += ["GPOS", "GSUB", "GDEF", "BASE", "kern", "STAT"]
options.name_IDs = ["*"]
font_subset = subset.Subsetter(options=options)
font_subset.populate(unicodes=characters)
font_subset.subset(font)
font.save(destination)
print(f"{destination.stat().st_size:,} bytes, {len(font.getBestCmap())} characters")
