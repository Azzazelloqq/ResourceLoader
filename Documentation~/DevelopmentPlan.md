# ResourceLoader development plan

## Goal and boundaries

Keep Addressables as the loading, dependency, and reference-counting backend. Add explicit per-consumer ownership, convenient lifetime binding, and an Editor-only generator for grouped string addresses. Do not add asset-bundle building, remote delivery, object pooling, a resource cache, or a new scheduling system in this release.

## Compatibility contract

Existing `IResourceLoader` members and their signatures remain available. Existing bare-resource loads remain loader-owned and continue to be released by `ReleaseResource`, `ReleaseAllResources`, or loader disposal. Existing lease loads remain supported. New lifetime-owned leases are isolated from `ReleaseResource` value matching: a call with a resource value cannot take another consumer's lease. New overloads, rather than changed return types, expose lifetime ownership. Package and assembly names remain unchanged. New Editor code lives in an Editor-only assembly.

## Priority 1: ownership and lifetime

1. Maintain separate legacy and lease ownership records. One acquired Addressables handle belongs to one record; removing the record claims the right to release it exactly once. The loader disposes all records. A legacy value release searches only legacy records.
2. Introduce `ResourceLifetime`, a sealed reference type implementing `IDisposable`. It is created once per owner period. It registers individually acquired leases, removes leases that are disposed early, rejects registration after closing, and closes each remaining lease once. It must not retain every lease ever created over a long session.
3. Add lifetime overloads to the concrete loader and to a small additive interface so calls can be made through an abstraction without modifying the existing interface. Loading with a lifetime registers ownership before handing the result to the caller. If the owner closes while a load is in flight, the result is cleaned up and the caller receives cancellation rather than a resource that is already released.
4. Add a `GameObject` lifetime component and a public binding method. `OnDestroy` closes the lifetime. A plain C# owner can place one `ResourceLifetime` into the existing `Disposable.CompositeDisposable`; this needs no hard package dependency. `MonoBehaviourDisposable` owners may close their lifetime from their disposal hook. No `Observable` is introduced.
5. Instance leases destroy their instances when their owner/loader closes. Keep the prefab's Addressables hold until the instance has actually been destroyed. Handle cancellation, missing components, and repeated disposal without leaking or double-release. Existing bare `LoadAndCreateAsync` behavior is preserved for compatibility and documented as legacy.
6. Preserve existing cancellation semantics for callers while ensuring in-flight Addressables operations are released safely on completion. Keep Unity object operations on the Unity thread.

### Runtime verification

Test independent consumers of the same resource, early/late lifetime close, duplicate disposal, `ReleaseResource` isolation, loader disposal, instance destruction, and a long-lived owner whose leases are released one by one. Use operation-level tests and PlayMode tests for Unity destruction. Measure allocations and elapsed time for repeated acquisitions and disposals in Player before considering further optimization.

## Priority 2: string address generator

1. Add an Editor-only settings asset. It selects Addressables groups and defines one or more address separators (such as `/` and `-`), output namespace/class/path, and naming rules. Default to manual generation; no on/off feature switch is needed.
2. Read configured groups and addresses from Addressables Editor settings. Generate deterministic nested static C# classes and `const string` leaves. The leaf value is the original address; naming transformations affect identifiers only.
3. Resolve invalid identifier characters and C# keywords. Detect duplicate generated member paths, group/leaf collisions, empty path segments, and duplicated output members. Report exact conflicting addresses rather than silently renaming them. Escape string literals and write the file only if content changes.
4. Provide a small Editor window for selecting/editing settings, previewing output, displaying errors, and generating. Keep the generator in a separate Editor assembly. Do not modify Addressables group data.

### Generator verification

Test grouping, multiple separators, invalid identifiers, keywords, collisions, escaping, stable ordering, and unchanged-file behavior. Verify generated code compiles in the target Unity project and existing consumer code still compiles. Document the new APIs and migration path without requiring existing users to migrate.

## Release gate

Run the applicable Editor/PlayMode tests, inspect Unity compile and Console in the target project, and compare the dirty worktree before and after. Report exact passing counts, failed/unavailable checks, and Player performance evidence separately. Do not claim Player performance without a Player run.
