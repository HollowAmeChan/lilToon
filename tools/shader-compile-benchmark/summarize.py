"""Summarize a completed run without counting startup as shader compilation."""
import json
import statistics
import sys
from collections import defaultdict
from pathlib import Path

path = Path(sys.argv[1])
report = json.loads(path.read_text(encoding="utf-8-sig"))
groups = defaultdict(list)
for row in report["rows"]:
    groups[row["round"], row["profile"]].append(row)
print(f'Unity {report["unity"]}; {report["graphics"]}; {report["cpu"]}')
print(f'Completed: {report.get("completed", False)}; all requests valid: {report["success"]}; errors/invalid requests: {report["errors"]}')
print(f'Pipeline bootstrap: {report["bootstrapMs"] / 1000:.3f} s')
print(f'Force-import {report["shaderCount"]} shaders: {report["forceImportMs"] / 1000:.3f} s')
for (round_id, profile), rows in sorted(groups.items()):
    compile_ms = sum(r["compileMs"] for r in rows)
    set_ms = sum(r["setPassMs"] for r in rows)
    precompiled = sum(r["compiledBefore"] for r in rows)
    invalid = sum(not r["compiledAfter"] or not r["setPass"] for r in rows)
    print(f'Round {round_id} / {profile}: {len(rows)} requests, compile {compile_ms / 1000:.3f} s, SetPass {set_ms / 1000:.3f} s, already compiled {precompiled}/{len(rows)}, invalid {invalid}')
warm = [sum(r["compileMs"] + r["setPassMs"] for r in report["rows"] if r["round"] == i) for i in (1, 2, 3)]
print(f'Warm request workload median (includes invalid requests): {statistics.median(warm) / 1000:.6f} s; rounds ms: {warm}')
valid_warm = [sum(r["compileMs"] + r["setPassMs"] for r in report["rows"] if r["round"] == i and r["compiledAfter"] and r["setPass"]) for i in (1, 2, 3)]
print(f'Warm valid requests only, median: {statistics.median(valid_warm) / 1000:.6f} s')
print(f'No-op generation median: {statistics.median(report["noOpGenerationMs"]) / 1000:.3f} s')
print('Slowest first-round requests:')
for row in sorted((r for r in report["rows"] if r["round"] == 0), key=lambda r: r["compileMs"] + r["setPassMs"], reverse=True)[:10]:
    print(f'{row["compileMs"] / 1000:.3f} + {row["setPassMs"] / 1000:.3f} s / {row["profile"]} / {row["shader"]} / {row["pass"]} / {row["localKeywords"]}')
