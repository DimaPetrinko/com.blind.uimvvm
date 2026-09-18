# Changelog

## 0.5.0

Reactive properties leave the UI assembly, so domain code can use them without compiling against
UI Toolkit.

### Changed - breaking

- **`ReactiveProperties` and `Disposables` moved to a new `Blind.Reactive` assembly**, and their
  namespaces moved with them. An assembly that only needs change-notifying values now references
  `Blind.Reactive` and pulls in neither UI Toolkit nor UniTask. `Blind.UiMvvm` references it, so a
  view keeps working unchanged once its `using` lines are updated.

  | 0.4.0 | 0.5.0 |
  |---|---|
  | `Blind.UiMvvm.ReactiveProperties` | `Blind.Reactive` |
  | `Blind.UiMvvm.ReactiveProperties.Implementation` | `Blind.Reactive.Implementation` |
  | `Blind.UiMvvm.Disposables` | `Blind.Reactive.Disposables` |

  The `ReactiveProperties` segment is gone because `Blind.Reactive.IReactiveProperty<T>` does not
  stutter, and because leaving `UiMvvm` in the name of the one assembly that exists in order not to
  be UI defeats the split. Nothing about the types themselves changed.

- **`ViewBinding.TryBind` constrains to `IBindable<TViewModel>` rather than `View<TViewModel>`.**
  Its body only ever called `Bind`, so the view constraint was narrower than the code needed and made
  `Binding/` depend on `Views/` for nothing - and it locked out the bindable parts of a screen that
  are plain `MonoBehaviour`s rather than UI Toolkit views. The null check is written out rather than
  left as `view != null`, because on a type parameter that operator does not reach
  `UnityEngine.Object`'s overload and the fake-null guard, which is the whole point of the method,
  would have silently stopped working. Call sites still infer both type arguments.

### Added

- `ScreenRoot`, a concrete `ViewRoot` for a screen that scopes child views but has no view model of
  its own. `ViewRoot` is abstract and `View<TViewModel>` demands a view model, so a GameObject that
  exists purely to name a scope previously had nothing to put on it.

### Tests

- `Blind.Reactive.Tests` splits out of `Blind.UiMvvm.Tests` alongside the runtime split, carrying
  `ReactivePropertyTests`, `ReactivePropertyIntegrationTests` and `SubscriptionTests`.
  `ViewLifecycleTests` stays put - it is a `View<T>` test that merely uses a `ReactiveProperty`.
- New coverage for `ViewBinding.TryBind`, which had none, including the destroyed-component case the
  constraint change turns on, and for a `View<T>` resolving through a `ScreenRoot`.

## 0.4.0

Everything a consuming project was writing by hand around this package, plus four defects.

### Fixed

- **`ScreenService.Open` pushed duplicate stack entries.** It only refused reopening the screen on
  *top*, so opening one already lower in the stack gave a single opener two slots and sent it
  `Open`/`SetCovered`/`Close` for both. It now refuses anywhere in the stack.
- **`Current` lied during a close.** The stack was popped before `opener.Close()` was awaited, so
  `Current` named the screen underneath while the closing one was still on screen. Harmless while
  every opener was synchronous; wrong the moment a close is animated. The close is awaited first, and
  the screen below is uncovered *before* it, so an exit animation plays over something.
- **`ReactiveProperty` allocated on both hot paths.** Equality went through static `object.Equals`,
  which boxes both operands for a value type, and every notification allocated the array
  `Delegate.GetInvocationList` returns. Now `EqualityComparer<T>.Default` and a reused snapshot.
- **`ViewRoot` cached its root element forever.** A `UIDocument` rebuilds its visual tree when it is
  re-enabled, so the cached element was an orphan from the old tree and the view silently drove
  nothing. It re-resolves when the cached element has left its panel, which is what lets a screen be
  hidden and shown rather than destroyed and recreated.

### Added

- `IReadOnlyReactiveProperty<T>.Subscribe(handler, fireImmediately = true)` returning `IDisposable`,
  and `CompositeDisposable`. Replaces the subscribe / prime / unsubscribe triple, where the half that
  gets forgotten is silent.
- `ReactiveProperty<T>.ForceNotify()` for a reference type whose contents changed but whose reference
  did not.
- `View<TViewModel>`: `Require<T>`/`Optional<T>` element lookup, `OnClick`, `Subscribe`, `Track`,
  `Every`, and an `Unbind` that undoes all of it and is called from `OnDestroy`. Views override
  `OnUnbind` for anything left. Re-binding after unbinding is supported.
- `ScreenOpener<TScreen, TInstance>` with `ModalScreenOpener` and `PersistentScreenOpener` subclasses
  and a `ScreenCover` value (`Display` or `UssClass`). Container-agnostic: a project supplies
  `CreateScreen`, `ResolveBinder` and `ResolveRoot`. `CreateScreen` is synchronous and `Prepare` is
  the only place to await, so binding provably happens in the same call that created the screen.
- `IUssTransition` / `UssTransition`: awaitable USS transitions that always complete exactly once and
  never throw. Encodes the two traps - a start state needs a frame to resolve before the end state
  lands, and completion is a timer rather than `TransitionEndEvent`, which reports a cancel when
  interrupted and never fires at all when no transition is declared.
- `SafeArea`, a `[UxmlElement]` that insets itself from `Screen.safeArea`. UI Toolkit has none of its
  own.
- `UiToolkitConverters`, registering the `bool` → `StyleEnum<DisplayStyle>` binding converter every
  visibility binding needs.
- `ViewBinding.TryBind`, which reports an unassigned view reference by name instead of taking the
  rest of the screen down with it.
- **`Blind.UiMvvm.Editor`**: a `ViewRoot` inspector that replaces the free-text `Root Name` with the
  element names actually in the UXML, scoped the way the runtime scopes them, and flags one that no
  longer resolves.
- **`Blind.UiMvvm.TestSupport`**: `UxmlAudit` (binding paths resolve to `[CreateProperty]` members,
  including nested and indexed paths and inherited interface members; named elements exist; names are
  unique), `PrefabAudit.RootNamesResolve`, and `ViewAudit.NoViewDeclaresOnDestroy`. Returns results
  rather than asserting, so it needs no test framework and a project keeps its own assertion style.

### Changed — breaking

- `IScreenService.Open`/`Close` return `UniTask<bool>` rather than `UniTask`, so a caller can tell a
  refused transition from a completed one. `.Forget()` call sites are unaffected.
- `IScreenService` gains `bool IsOpen(TScreen)`.
- `View<TViewModel>` is constrained to `where TViewModel : class`; a struct view model was boxing on
  every `dataSource` assignment.
- **`View<TViewModel>` now declares `protected virtual void OnDestroy()`.** A subclass declaring its
  own `OnDestroy` *hides* it rather than overriding, Unity dispatches the most derived one only, and
  nothing the view wired is ever unwired. It compiles and it runs. Override `OnUnbind` instead, and
  see `ViewAudit.NoViewDeclaresOnDestroy`.

### Packaging

Licensed MIT. `package.json` gains `license` and `keywords`, and the install instructions now cover
the UniTask prerequisite explicitly — UPM resolves dependencies only from registries, never from git
URLs, so UniTask cannot be declared and its absence shows up as a compile error.

### Tests

`ScreenService` and `StubScreenOpener` coverage moved into the package, where it belongs, keyed on a
local test enum. New suites for subscriptions, view lifecycle, screen openers, transitions and the
safe-area maths. 135 EditMode tests, no PlayMode requirement.
