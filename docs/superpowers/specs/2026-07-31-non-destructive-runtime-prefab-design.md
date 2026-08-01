# Non-destructive Runtime Prefab Generation

## Goal
Change the GPU particle baker so that it creates a brand-new runtime prefab for GPU playback instead of modifying the original particle prefab.

## Current Behavior
`GpuParticleBakePipeline.BakePrefab()` and `BakePrefabGroup()` currently:
1. Generate VAT textures, mesh, material, clip and a VAT prefab under `Assets/GpuParticleGenerated/<PrefabName>/...`.
2. Call `GpuParticleBindingWriter.WriteBinding()` to load the **original** prefab contents, add `GpuParticleBinding` + `GpuParticlePlayer` to the matching child transforms, and save the original prefab back.
3. Add `GpuParticleGroupPlayer` to the root of the **original** prefab.

This overwrites the original prefab and makes it impossible to keep editing the native particle effect without the GPU playback components interfering.

## Desired Behavior
After baking, the original prefab stays untouched. A new runtime prefab is generated that mirrors the original hierarchy and hosts all GPU playback components.

## Design

### Asset Output Layout
For an input prefab at `Assets/Prefabs/MyEffect.prefab`:

```
Assets/GpuParticleGenerated/MyEffect/
├── MyEffect_Runtime.prefab          # NEW: runtime playback prefab
├── SystemPath/
│   ├── SystemName.gpuparticle.asset
│   ├── SystemName_VAT.prefab
│   ├── SystemName_PositionSize.asset
│   ├── SystemName_Color.asset
│   ├── SystemName_Rotation.asset
│   ├── SystemName_VelocityLifetime.asset
│   ├── SystemName_VATMesh.asset
│   └── SystemName_VATMaterial.mat
```

### Runtime Prefab Structure
`MyEffect_Runtime.prefab` is created by duplicating the original prefab and then stripping/transforming it:

1. **Preserve**: GameObject hierarchy, names, transforms, and any non-particle components (scripts, colliders, lights, etc.).
2. **Remove**: all `ParticleSystem` and `ParticleSystemRenderer` components.
3. **Add per baked system**:
   - `GpuParticleBinding` on the GameObject at the same transform path as the original particle system.
   - `GpuParticlePlayer` on the same GameObject.
4. **Add at root**:
   - `GpuParticleGroupPlayer` (always, so single and multi-system bakes behave consistently).

The runtime prefab is a standalone asset and is **not** a prefab variant of the original. Nested prefab connections from the original will be flattened, which is acceptable for a runtime visualization prefab.

### Pipeline Changes

#### `GpuParticleBakePipeline`
Both `BakePrefab` (single system) and `BakePrefabGroup` (multi system) will follow the same non-destructive flow:

- After VAT assets are written, instead of calling `GpuParticleBindingWriter.WriteBinding(prefab, ...)` on the original prefab, call a new helper:
  ```csharp
  GameObject runtimePrefab = GpuParticleRuntimePrefabBuilder.Build(
      prefab,
      settings,
      bakedSystems);
  ```
- `AddGroupPlayer(prefab)` becomes `AddGroupPlayer(runtimePrefab)`.
- The original prefab is no longer imported/saved by the baker.
- Return the runtime prefab reference in `GpuParticleValidationResult`.

#### New `GpuParticleRuntimePrefabBuilder`
Responsibilities:
1. Determine output path: `{settings.OutputRoot.TrimEnd('/')}/{prefab.name}/{prefab.name}_Runtime.prefab`.
2. Delete existing runtime prefab at that path if present.
3. Instantiate the original prefab in a preview/prefab isolation context.
4. Walk the instance and remove `ParticleSystem` and `ParticleSystemRenderer` components.
5. For each baked system (identified by transform path relative to root):
   - Find matching transform in the duplicated instance.
   - Add `GpuParticleBinding` and `GpuParticlePlayer`.
   - Configure binding with the matching clip and captured native system/renderer state.
6. Save duplicated instance as the new runtime prefab via `PrefabUtility.SaveAsPrefabAsset`.
7. Destroy the temporary instance.

#### `GpuParticleBindingWriter`
- Add an overload that writes to an in-memory `GameObject` (the prefab contents root) instead of loading from a prefab path:
  ```csharp
  public static void WriteBinding(
      GameObject root,
      string transformPath,
      GpuParticleBakeStatus status,
      GpuParticleClip clip,
      string failureCode,
      GpuParticleNativeSystemState[] systemStates,
      GpuParticleNativeRendererState[] rendererStates,
      bool addPlayer = false)
  ```
- Keep existing path-based overloads for backward compatibility / other callers.

#### `GpuParticleValidationResult`
Add a new field:
```csharp
public readonly GameObject RuntimePrefab;
```
So callers (editor UI, tests, batch tools) can know where the playable prefab was created.

### Native State Capture
The native particle system/renderer states are still captured from the preview scene instance during baking. These states are stored in the runtime prefab's `GpuParticleBinding` so that `RevertToNative` can restore the original behavior if needed.

### Edge Cases
- **Existing runtime prefab**: overwrite it.
- **Missing transform path in duplicated instance**: log warning and skip that system.
- **Original prefab scripts referencing ParticleSystem**: references will be missing in the runtime prefab. This is expected; the runtime prefab is for visualization playback only. The original prefab remains editable.
- **Single system bake**: still create a `_Runtime.prefab`; `GpuParticleGroupPlayer` is always added at root for consistency.

### `RevertToNative` Behavior
Because the original prefab is no longer modified, `RevertToNative(prefab)` changes meaning:

- It no longer writes a `Native` binding back into the original prefab.
- It deletes the generated `_Runtime.prefab` for that source prefab if one exists.
- It leaves the original prefab and the VAT texture/mesh/clip assets untouched.
- The menu item label may be updated to "Clear Baked Runtime Prefab" to reflect the new behavior.

### Backward Compatibility
This is a behavior change. Old baked prefabs that already have GPU components written into them will continue to work. New bakes will not modify the source prefab anymore. No migration is required.

### Testing
- Unit test: bake a prefab with nested particle systems and assert that the original prefab has no `GpuParticleBinding`/`GpuParticlePlayer`.
- Unit test: assert that the runtime prefab exists and has the expected components at the correct transform paths.
- Unit test: assert `GpuParticleValidationResult.RuntimePrefab` is non-null after a successful bake.
