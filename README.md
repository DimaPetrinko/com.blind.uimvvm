# UI MVVM

UI Toolkit MVVM primitives for Unity 6: reactive properties, view binding, a `View<TViewModel>` base
that drives Unity's runtime data binding, and an awaitable screen-navigation stack.

No DI container required. Depends on built-in engine modules plus
[UniTask](https://github.com/Cysharp/UniTask), which navigation uses for awaitable transitions.

## Install

**Install [UniTask](https://github.com/Cysharp/UniTask) first.** This package's asmdef references it,
and UPM cannot declare it as a dependency because UPM only resolves dependencies from registries,
never from git URLs. Without UniTask present you will get a compile error rather than a resolve error.

Then add this package to `Packages/manifest.json`, pinned to a release tag:

```json
{
  "dependencies": {
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.blind.uimvvm": "https://github.com/DimaPetrinko/com.blind.uimvvm.git#0.4.0"
  }
}
```

Pin a tag rather than tracking a branch — UPM caches git dependencies aggressively, and an unpinned
URL makes "which version am I on" unanswerable.

Requires Unity 6000.0 or newer — `VisualElement.dataSource` runtime binding does not exist before it.

## What is in the box

| Type | Role |
|---|---|
| `IReactiveProperty<T>` / `IReadOnlyReactiveProperty<T>` / `ReactiveProperty<T>` | Change-notifying values. `Changed` only fires when the value actually differs. Handler exceptions are logged, not propagated. Both hot paths are allocation-free. |
| `.Subscribe(handler, fireImmediately)` / `CompositeDisposable` | Subscribe, prime and unsubscribe as one thing instead of three. |
| `IBindable<TVm>` | A view accepts a view model. |
| `ViewRoot` | `MonoBehaviour` base holding the serialized `Root Name` and resolving that element **relative to the nearest ancestor `ViewRoot`**, or the `UIDocument` root when there is none. |
| `View<TViewModel>` | `ViewRoot` subclass that assigns the resolved root's `dataSource` and undoes, on destroy, everything wired through its helpers. |
| `IViewBinder` / `ViewBinding.TryBind` | One screen's "bind my views" step, called by whatever opens the screen. |
| `IScreenOpener<TScreen>` / `IScreenService<TScreen>` / `ScreenService<TScreen>` | Awaitable screen navigation keyed by **your** enum. Each screen registers an opener; callers only name a screen. |
| `ScreenOpener<TScreen, TInstance>` + `ModalScreenOpener` / `PersistentScreenOpener` / `ScreenCover` | The opener skeleton, minus your container. Create, bind, destroy or cover. |
| `StubScreenOpener<TScreen>` | Placeholder opener for a screen that does not exist yet. |
| `IUssTransition` / `UssTransition` | Awaitable USS transitions that complete exactly once and never throw. |
| `SafeArea` | A `[UxmlElement]` that insets itself from `Screen.safeArea`. UI Toolkit has none of its own. |
| `UiToolkitConverters` | The `bool` → `StyleEnum<DisplayStyle>` converter every visibility binding needs. |
| `Blind.UiMvvm.Editor` | A `ViewRoot` inspector that turns `Root Name` into a list of the names actually in the UXML. |
| `Blind.UiMvvm.TestSupport` | `UxmlAudit`, `PrefabAudit`, `ViewAudit` - the UXML-to-C# links the compiler cannot see. |

## Building a panel

1. **UXML** — give the panel root a `name`, and bind properties declaratively:

   ```xml
   <ui:VisualElement name="CounterPanel">
       <ui:Label>
           <Bindings>
               <ui:DataBinding property="text" data-source-path="Counter" binding-mode="ToTarget"/>
           </Bindings>
       </ui:Label>
       <ui:Button name="Reset" focusable="false"/>
   </ui:VisualElement>
   ```

2. **View model** — expose already-formatted values with `[CreateProperty]`, and recompute them when
   a source changes. Never format or allocate inside a bound getter: bindings poll every frame.

   ```csharp
   internal class CounterViewModel : ICounterViewModel, IDisposable
   {
       private readonly CompositeDisposable mSubscriptions = new();

       public CounterViewModel(IReadOnlyReactiveProperty<int> score)
       {
           mSubscriptions.Add(score.Subscribe(OnScoreChanged));
       }

       [CreateProperty] public string Counter { get; private set; } = "0";

       public void OnResetClicked() { }

       public void Dispose() => mSubscriptions.Dispose();

       private void OnScoreChanged(int score) => Counter = score.ToString();
   }
   ```

   `Subscribe` primes the handler with the current value by default, because that is what a display
   wants and doing it by hand is the same work in two places - one of which gets forgotten silently.
   Pass `fireImmediately: false` where a view model seeds itself some other way.

3. **View** — element lookup and event wiring only. No formatting, no game-state conditionals.

   ```csharp
   internal class CounterView : View<ICounterViewModel>
   {
       public override void Bind(ICounterViewModel viewModel)
       {
           base.Bind(viewModel);

           OnClick("Reset", mViewModel.OnResetClicked);
       }
   }
   ```

   `OnClick`, `Subscribe`, `Track` and `Every` are undone for you when the view is destroyed.
   `Require<T>(name)` throws a named exception rather than handing back the null `Q` returns, and
   `Optional<T>` is there for an element a screen may legitimately be authored without. Override
   `OnUnbind` for anything the helpers do not cover.

   **Never declare `OnDestroy` in a view.** Unity dispatches the most derived one only, so yours
   hides this class's and nothing is ever unwired - it compiles, it runs, and every subscription
   outlives the screen. `ViewAudit.NoViewDeclaresOnDestroy` is a one-line test against that.

4. **Root name** — set the view's `Root Name` field in the Inspector to the UXML root's `name`
   (`CounterPanel` above). Scope one view per panel root so each panel's `data-source-path` values
   stay short and local.

   **Root lookup is scoped to the nearest ancestor view.** A view with no parent view resolves against
   `UIDocument.rootVisualElement`; a view whose GameObject sits under another view resolves inside
   *that* view's root. So mirror the UXML tree in the GameObject tree:

   ```
   UIDocument
   └─ ScreenView          Root Name = Screen
      ├─ FirstCounterView  Root Name = FirstCounter   (a <ui:Instance> of a shared template)
      │  └─ AdButtonView   Root Name = AdButton       (resolved inside FirstCounter only)
      └─ SecondCounterView Root Name = SecondCounter
         └─ AdButtonView   Root Name = AdButton
   ```

   This is what lets several instances of one template each be bound: a name only has to be unique
   inside its parent's subtree, not across the whole document. Resolution recurses up by name rather
   than reading a parent's bound root, so **bind order does not matter**.

5. **Wire it up** — implement `IViewBinder` for the screen and call it from whatever creates the
   screen, immediately after creating it:

   ```csharp
   internal sealed class CounterScreenBinder : IViewBinder
   {
       private readonly CounterView mView;
       private readonly ICounterViewModel mViewModel;

       public CounterScreenBinder(CounterView view, ICounterViewModel viewModel)
       {
           mView = view;
           mViewModel = viewModel;
       }

       public void Bind() => mView.Bind(mViewModel);
   }
   ```

   **Bind in the same call that creates the screen, not from a start/tick hook.** A screen is usually
   opened from an input event, which UI Toolkit dispatches late in the frame, while container start
   phases run early in the *next* one. Bind from a start hook and the screen renders one frame of
   whatever placeholder values you authored into the UXML. Creating the object has already run its
   `Awake` and `OnEnable`, so the container and the visual tree both exist right there:

   ```csharp
   // VContainer; equivalent hooks exist elsewhere
   var screen = parentScope.CreateChildFromPrefab(mScreenPrefab);
   screen.Container.Resolve<IViewBinder>().Bind();
   ```

   Deriving your opener from `ScreenOpener<TScreen, TInstance>` (see Navigation) makes that ordering
   structural rather than something to remember.

   Register view models with a scoped lifetime, not transient — most containers do not dispose
   transient instances, and an undisposed view model keeps its subscriptions alive.

6. **Test** — view-model tests need no Unity runtime. For the links the compiler cannot see -
   `data-source-path` to `[CreateProperty]`, looked-up names to elements, `Root Name` to the
   document - use `Blind.UiMvvm.TestSupport` (see Tests).

## Tests

The package ships EditMode tests covering `ReactiveProperty` and `View<TViewModel>` (scoped root
resolution, two instances of one template binding independently, nested views, `dataSource` hand-off,
and the failure modes when the root name or `UIDocument` is missing). They run in any project with the
Test Framework installed and need no scene setup.

The audits described in step 6 now ship as `Blind.UiMvvm.TestSupport`, an Editor assembly with no
test-framework reference of its own - it returns results rather than asserting, so your project keeps
its own assertion style and the same checks are usable from editor tooling:

```csharp
[TestCase("Assets/UI/Screens/Counter.uxml", typeof(ICounterViewModel))]
public void EveryBindingPath_Resolves(string uxml, Type viewModel)
{
    var result = UxmlAudit.BindingPathsResolve(uxml, viewModel);
    Assert.That(result.IsValid, result.Describe());
}
```

- `UxmlAudit.BindingPathsResolve` follows nested (`Player.Stats.Health`) and indexed (`Items[0]`)
  paths, checks fields as well as properties, and walks inherited interfaces - a view model is
  normally bound through one, and `GetProperty` on an interface does not see what it extends.
- `UxmlAudit.ElementsExist` / `NamesAreUnique` cover the names your views look up.
- `PrefabAudit.RootNamesResolve(prefab)` walks a screen prefab the way the runtime walks the scene
  and checks every view's `Root Name` in its own scope.
- `ViewAudit.NoViewDeclaresOnDestroy(assemblies)` catches the hidden-`OnDestroy` mistake above.

## Navigation

The screen *set* is project-specific, so `ScreenService<TScreen>` is generic over your enum and owns
only the dispatch:

```csharp
public enum ScreenId { None = 0, MainMenu = 1, Shop = 2 }   // yours

var service = new ScreenService<ScreenId>(new IScreenOpener<ScreenId>[]
{
    new ShopScreenOpener(),                       // your opener
    new StubScreenOpener<ScreenId>(ScreenId.MainMenu),
});

await service.Open(ScreenId.Shop);
```

`Open` and `Close` return `UniTask<bool>`, and the result says whether the transition actually
happened: an opener can decline, a screen can already be open, and a request arriving mid-transition
is refused. `IsOpen(screen)` reports anything in the stack, not just the top.

### Writing an opener

`ScreenOpener<TScreen, TInstance>` owns the shared skeleton - is there a live instance, create it,
bind it in that same call, report a refusal - and two subclasses own the lifetime:

| | Second `Open` | `Close` |
|---|---|---|
| `ModalScreenOpener` | refused | `DestroyScreen` |
| `PersistentScreenOpener` | reuses the instance | covers it, per its `ScreenCover` |

`ScreenCover.Display` collapses the screen; `ScreenCover.UssClass("hidden")` toggles a class, so the
stylesheet decides what covered looks like and can transition into it.

```csharp
internal class ShopScreenOpener : ModalScreenOpener<ScreenId, ShopScreen>
{
    public ShopScreenOpener(...) : base(ScreenId.Shop, logWarning, logError) { ... }

    protected override ShopScreen? CreateScreen() => /* your container */;
    protected override IViewBinder? ResolveBinder(ShopScreen screen) => /* from its scope */;
    protected override void DestroyScreen(ShopScreen screen) => screen.Dispose();
}
```

No container is named anywhere in the package. **`CreateScreen` is synchronous and `Prepare` is the
only place to await** - that is what makes "bind in the same call that created the screen" a property
of the API rather than a rule to remember. A screen that must load something awaits in `Prepare`,
then creates and binds atomically.

Returning `null` from `CreateScreen` is a fault and is logged; returning `false` from `Prepare` is a
decline and is silent. A missing binder logs but still opens - refusing would leave a visible,
unbound screen the stack does not know about and can therefore never close.

`Open` and `Close` return `UniTask`, so an opener can load what the screen needs before it reports
success. An opener with nothing to wait for should complete synchronously - the service then awaits
inline, and the screen is up within the same frame. A caller with nothing to sequence afterwards drops
the task with `Forget()`.

**One transition runs at a time.** A request arriving while another is still in flight is refused with
a warning rather than queued, because interleaving two transitions would push onto a stack whose top
is still moving.

**Give the enum a zero member such as `None`.** The default value of `TScreen` is reserved as "no
screen": opening it is reported as an error, and an opener that claims it is rejected at construction.
That is what stops an unset or defaulted field from silently naming a real screen.

Missing openers, duplicates and the two default-value cases are reported through optional
`Action<string>` delegates that fall back to `Debug.LogWarning`/`Debug.LogError`, so the package needs
no logging dependency and a project can route the messages into its own logger:

```csharp
new ScreenService<ScreenId>(openers,
    message => logger.LogWarning(message),
    message => logger.LogError(message));
```

## Nothing ticks

There is no driver and no per-frame contract. A view model recomputes only when a reactive source
fires, and Unity re-reads bound properties on its own. If a screen genuinely needs a tick, use your
container's own tick hook (VContainer's `ITickable`, or a plain `MonoBehaviour`) rather than routing it
through this package.

## Transitions

USS transitions, awaited. Two things about UI Toolkit make this more than a one-liner, and both are
encoded here rather than rediscovered:

- **A value that was never resolved has nothing to transition from.** Writing the start and end states
  in one frame snaps to the end and nothing animates, so the start is written with transitions
  suppressed and the end is deferred by one panel tick.
- **An interrupted transition reports a cancel, not an end** - and an element whose stylesheet
  declares no transition fires neither, so an event-based wait hangs forever. Completion is a timer
  armed from the resolved duration; no transition callbacks are registered at all.

```csharp
protected override UniTask PlayEnter() => mTransition.PlayFrom("screen--hidden").AsUniTask();
protected override UniTask PlayExit()  => mTransition.Play("screen--hidden", true).AsUniTask();
```

The task **always completes exactly once and never throws**, `OperationCanceledException` included -
these are awaited inside openers whose tasks callers routinely `Forget()`.

| | result | end state applied |
|---|---|---|
| the transition runs out, or none is declared | `true` | yes |
| the element has no panel, or leaves its panel mid-run | `true` | yes |
| superseded by a newer `Play`, `Cancel()`, or a cancelled token | `false` | no |

So `true` means the end state is on screen, and `await fade; Destroy();` is safe without the caller
having to know whether the panel survived.

Pass an explicit duration for a value that is data; otherwise it is read from the element's resolved
style, so the timing stays in USS. `ITransitionScheduler` is the seam the tests drive - callers never
name it.

**Do not give a gameplay HUD an enter animation.** The stack push waits on it.

## Caveats
- **`Root Name` is an untyped link to UXML**, and it is resolved within the parent view's scope. A
  mismatch throws `MissingComponentException` on bind — intentional, since it fails loudly instead of
  null-referencing later. The `Blind.UiMvvm.Editor` inspector turns the field into a list of the names
  actually in the document so the typo case cannot happen, and `PrefabAudit.RootNamesResolve` covers
  the rest in a test.
- **Bindings poll.** Unity re-reads bound properties every frame. Keep bound getters to field reads.
- **A view must not declare `OnDestroy`.** See "Building a panel", step 3.
