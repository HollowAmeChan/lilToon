# lilToon editor compilation timing

Run `Measure-ShaderCompile.ps1` from PowerShell. It creates a unique disposable project and lilToon package copy; it does not clear the active project's Library, modify its materials, or edit live HLSL. Unity uses D3D11, a real Forward+ renderer, and synchronous `ShaderUtil.CompilePass(material, pass, true)`. The color space is copied from BREAK_URP by default, and the report records it. Extensions is frozen at committed HEAD; unrelated uncommitted edits are excluded and recorded in Run.json. The installed dependencies of CharacterShadowValidation are reused as local packages to exclude registry downloads and extraction failures. A fresh project still needs an initial C# compile and asset import.

```powershell
& D:/Unity_Fork/lilToon/tools/shader-compile-benchmark/Measure-ShaderCompile.ps1
python D:/Unity_Fork/lilToon/tools/shader-compile-benchmark/summarize.py <Project/CompileBenchmark.json>
```

The fixture covers opaque, cutout, transparent, outline, and hair; every selected URP pass plus supported GD tension/outline consumers. It rejects the fallback subshader. Two keyword profiles cover minimal Forward+ and a configured Forward+ combination of cascaded main shadows, additional light shadows, high quality soft shadows, cookies, light layers, clustered lighting, APV L2, and reflection probe features. Forward+ additional lights use the cluster loop; the legacy _ADDITIONAL_LIGHTS and _ADDITIONAL_LIGHTS_VERTEX keywords remain disabled, matching HoUrp17.3.0/Runtime/ForwardLights.cs. This is a controlled workload using the project's supported capabilities, not a capture of all material and global states in BREAK_URP.

The output separates these measurements:

- `Run.json/processSeconds`: the entire process, including package resolution, C# compilation, initial import, setup, benchmark and shutdown. **Not shader-only time.** A nonzero exit means the measurement did not finish; exclude that run. A completed measurement exits zero even if some sampled passes fail, so also inspect report.completed, report.success and individual compiledAfter/setPass results. Never label a failed request as a valid/renderable performance sample.
- `bootstrapMs`: renderer initialization and a small render request; this can compile unrelated URP shaders.
- `generationPreparationMs`: one full generation in the private copy before timing, ensuring copied templates and generated shader outputs agree. Recorded separately from compilation.
- `forceImportMs`: sequential forced synchronous imports of all current lilToon shader assets. This includes importer/AssetDatabase work and can reuse importer/compiler caches; it is not a guaranteed cold compilation of every variant.
- Round 0 per-request `compileMs`: first synchronous requests in this fresh process/project. A fresh project-local cache does not guarantee an empty global/compiler/driver cache. `compiledBefore` distinguishes an already resident compiled pass. A false value does not prove a disk-cache miss.
- `setPassMs`: the subsequent native SetPass call, including applicable driver setup; reported separately from CompilePass.
- Rounds 1–3: repeat the same requests with the same materials and keyword profiles. Use their median for the warm request workload.
- `noOpGenerationMs`: three unchanged full-generation calls after a normalization call, including the generator's remaining AssetDatabase operations. This is not a GPU compilation measure.

No-op reimport or an added HLSL comment is not a reliable cold test. To measure the actual five-minute iteration, use a copied representative scene/project, change effective shader code in its private package copy, and time from that edit/import until the scene's requested variants finish compiling. Keep the same material/global keyword states, renderer, graphics API and settings for both revisions. Repeat at least three independent revisions and report a median; record errors, cache behavior, and background workload. Do not use the 701→367 program count as an elapsed-time speedup.

For a before/after comparison, export two complete package revisions into separate copies and feed each to `-PackageSource`, with identical external dependencies and fixtures. Regenerate each revision's shaders with its own templates/settings before measurement. Current fixture expectations apply to the pruned version; update the expected pass set for the historical version rather than stripping its passes just to reuse the current test. This tool currently measures only the current version.

References: [Unity 6.3 shader compilation](https://docs.unity3d.com/6000.3/Documentation/Manual/shader-compilation.html), [CompilePass](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ShaderUtil.CompilePass.html), [IsPassCompiled](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ShaderUtil.IsPassCompiled.html).
