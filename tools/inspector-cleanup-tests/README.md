# Inspector cleanup qualification

Run `LilInspectorCleanupQualification.Run` from a disposable Unity project.
Install the C# file in that project's `Assets/Editor` and reference a physical
copy of the candidate lilToon package. The runner writes
`InspectorCleanupQualification.txt` and exits with code 0 on success.

The runner draws real MaterialEditors in an EditorWindow. It covers English and
Simplified Chinese, both Inspector implementations, and opaque, cutout and
two-pass transparent outline shaders. All relevant foldouts are expanded. The
new Inspector's four pages are drawn separately.

Each draw compares the complete serialized material before and after viewing.
Inputs include custom queues, stencil masks, GI flags, main/outline HSV values
and enabled parallax. GUI errors and leaked GUI fonts fail the run. Main and
outline baking methods must be absent, while the shared `RunBake` API remains.

Optional arguments extend the checks for later cleanup steps.

- `-qualifyLilStaticUv` checks retired UV properties and retained static texture,
  outline-vector UV and parallax properties.
- `-qualifyLilSoftening` checks the concise Chinese labels.

## Results

2026-11-19, Unity 6000.3.15f1, HoURP 17.3, Windows, D3D12.

| Step | Result |
| --- | --- |
| Remove GI sections, main/outline baking and Stencil recipes | 30 GUI repaints passed, no material changes or GUI errors |

The raw step-one output is
`D:/Temp/liltoon-inspector-a9fcae14/Project/InspectorCleanupQualification.txt`;
the Editor log is
`D:/Temp/liltoon-inspector-a9fcae14/CleanupStep1.log`.

These checks qualify Inspector behavior. They do not measure performance or
qualify production-scene rendering.
