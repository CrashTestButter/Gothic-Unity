# Architecture

How Gothic-Unity is built, which principles new code follows, and why. `CLAUDE.md` has the setup and the module
overview; `Docs/save-game.md` is an example of a documented area.

## In one paragraph

Gothic-Unity is a modular monolith. `Gothic.Core` is the engine. Game version modules (`Gothic1`, `Gothic2`) and
platform modules (`Gothic-VR`, `Gothic-Flat`) plug into it through interfaces and never know each other. Most of the
game's rules aren't ours: they live in Gothic's Daedalus scripts, which ZenKit runs. Our C# is the engine around
them: loading worlds, the externals the scripts call, animation, physics, VR. The game state lives in ZenKit's
objects (VOBs, NPC instances, script symbols), which is also what Gothic's save format stores.

## The big picture

```
              Gothic installation (.zen, .mds, scripts, ...)
                                |
                             ZenKit   (file formats, Daedalus VM, save format)
                                |
    +---------------------- Gothic.Core -----------------------+
    |  Models  <-  Domain  <-  Services  <-  Adapters (Unity)  |
    |  areas: Npc, Vobs, Vm, SaveGames/World, Player, UI, ...  |
    +-----^-------------^----------------^-------------^-------+
          |             |                |             |
       Gothic1       Gothic2         Gothic-VR     Gothic-Flat
       (game version modules)        (platform modules)
```

An arrow means "depends on". Modules depend on Core only (star topology).

## Principles

### 1. Scripts first

**Rule:** If Gothic's scripts decide something, let them decide. Implement the missing external, or call the existing
`B_*`/`C_*` helper (`vm.Call`), instead of writing the rule again in C#.

**Why:** Mods change the scripts. A C# copy of a script rule breaks mods and drifts away from the original game.

**Example:** XP, guild attitudes and dialog conditions come from the scripts. The `daedalus-bridge` skill shows how
C# talks to the VM.

### 2. ZenKit objects are the state

**Rule:** Keep game state in the ZenKit object Gothic keeps it in: VOB fields, the NPC instance, script symbols.
C# fields only cache or derive from it.

**Why:** One source of truth means nothing to keep in sync, and saving works without extra code, because Gothic's
save format is made of exactly these objects.

**Example:** remaining trigger activations live in `CountCanBeActivated`, a moving gate in the `zCMover` key frame
fields: both are saved for free. Counter-example: mob states were read from `State`, which is Gothic's *number* of
states (1 for every door). The current state now has its own, documented place (`core.mobStates`).

### 3. One rule, one place, next to its data

**Rule:** A rule about one thing (an NPC, a world, an item list) lives on that thing: a property or method on its
model, or an extension method in `Extensions/`. Everybody else calls it.

**Why:** The same rule written twice drifts apart, and when it changes, nobody finds all copies.

**Example:** `NpcProperties.IsDead`, `NpcContainerExtension.IsUsingMob()`, `ToGothicWorldName()`,
`ContentItemExtension.PackItems()`. Before, "is the NPC dead?" was written in 8 places with two different rules.

### 4. Extend by registration, not by `if`

**Rule:** When G1, G2, VR, Flat or a mod behave differently, Core defines an interface and each module registers its
implementation. Avoid `if (IsGothic2())` and `#if` in Core.

**Why:** A new module or mod plugs in without editing Core, and each module's differences are in its own files
instead of spread over Core.

**Example:** `IContextGameVersionService` (G1's `G1ContextService` is set in
`G1ContextBootstrap.RegisterGameVersionModule()`, e.g. for `CutsceneFileSuffix`), `IContextScreenshotService`
(VR left eye vs. flat camera), `ISaveStateSection` (every feature registers its own save data).

### 5. Fix ZenKit once, at the edge

**Rule:** Work around a ZenKit quirk in one place (`Extensions/ZenKit*.cs` or the domain class of its area) and
document it there. Callers use the clean method.

**Why:** A workaround repeated at every call site is forgotten at the next one.

**Example:** `ZenKitNpcExtension.TrySetAiVars()` (ZenKit's setter copies bytes instead of ints), the pitfall table
in `Docs/save-game.md`.

### 6. Speak Gothic

**Rule:** Name things after Gothic's concepts: VOB, Mob, oCNpc, AI state (`ZS_*`), perception, waynet, routine, the
name of the external. Split big classes along Gothic's own families.

**Why:** Whoever knows Gothic, its scripts or OpenGothic finds the code by name. `MobInteractionService` says more
than `InteractableManager`.

**Example:** `VmExternalDomain` (1,440 lines) would split by the externals' prefixes: `Npc_*`, `AI_*`, `Wld_*`,
`Mdl_*`, `Log_*`/`Info_*`.

### 7. Events for facts, calls for sequences

**Rule:** Use `GlobalEventDispatcher` to announce facts other areas react to ("world loaded", "NPC attribute
changed"). When steps must run in a fixed order, call them explicitly.

**Why:** The order of event listeners is invisible in the code. A sequence that depends on it breaks as soon as
somebody adds a listener.

**Example:** `SaveGameService` calls `Reset()` and then `Restore()` on every save section. Before, each section reset
itself on a different event, and one forgot to.

### 8. Small files, written down

**Rule:** A class has one reason to change. Aim for less than ~400 lines per class and ~50 per method. A non-trivial
area gets a short doc in `Docs/`.

**Why:** People and AI agents understand a change by reading a few small files. A 1,000-line class costs everybody
time on every change.

**Example:** `SaveGameService` orchestrates, the details live in `Domain/SaveGames/*`, the design in
`Docs/save-game.md`.

### 9. Leave it better, don't rewrite

**Rule:** Improve the code your change touches anyway. No big-bang refactorings.

**Why:** Rewrites conflict with every open branch and bring nothing a player notices. Small steps keep the game
working.

## Layers inside Core

| Layer | Folder | Holds | Rules |
|---|---|---|---|
| Models | `Models/` | data: containers (`NpcContainer`, `VobContainer`), proxies, value types | no services, no Unity lifecycle |
| Domain | `Domain/` | Gothic logic, often the helper of one service (`*Domain`, `*Creator`) | holds no game state, may call services |
| Services | `Services/` | use cases and state lifetime, registered in Reflex | thin orchestration |
| Adapters | `Adapters/` | MonoBehaviours bridging Unity and services | no rules, the only layer with Unity lifecycle |

Dependency rules (reviews check them):

1. Nothing outside `Adapters/` uses the `Adapters` namespace. Plain data classes there (`NpcProperties`,
   `InteractiveProperties`) belong in `Models/`.
2. Models don't use services.
3. Modules use Core only, never each other.

Today's code breaks rules 1 and 2 in places (about 30 files import `Adapters`, `NpcProxy` injects services). New code
follows the rules, old code gets fixed when it's touched (principle 9).

## Where does my code go?

| You want to ... | Put it into |
|---|---|
| implement a Daedalus external | `Domain/Vm/VmExternalDomain` registers it, the service of its area implements it (e.g. `NpcAiService`) |
| add a rule about an NPC, item or VOB | a property on its model or an extension in `Extensions/` |
| add a use case with state (loading, saving, dialogs) | a service in `Services/<Area>/`, registered in `ReflexProjectInstaller` |
| move a big piece of logic out of a service | `Domain/<Area>/<Name>Domain.cs`, created by the service |
| react to Unity (Update, collisions, input) | an adapter in `Adapters/<Area>/` that calls a service |
| behave differently in G1/G2/VR/Flat | an interface in `Services/Context/`, the implementation in the module, registered in its `*ContextBootstrap` |
| save state Gothic doesn't store | an `ISaveStateSection` (`Docs/save-game.md`) |
| work around a ZenKit bug | `Extensions/ZenKit*.cs`, plus a line in the pitfalls of the area's doc |

## Smells

- the same check written a second time
- `if (IsGothic1())` or `#if` in Core
- a C# field mirroring a ZenKit field
- a class over ~400 lines, a method over ~50, nesting deeper than 3
- names like `*Manager`, `*Helper`, `*Util`
- an event listener that only works if it runs before or after another one
- a ZenKit workaround at a call site

## Domain-Driven Design, mapped

The terms reviews use, for developers new to DDD:

| DDD term | Meaning | Here |
|---|---|---|
| Bounded context | an area with its own model and words | the area folders: `Npc`, `Vobs`, `Vm`, `SaveGames`, ... |
| Ubiquitous language | the same words in code, docs and conversation | Gothic's terms (principle 6) |
| Entity | a thing with an identity and a lifetime | an NPC (`NpcContainer`), a VOB |
| Value object | a small value, compared by its content | world names, item lists (`ContentItem`) |
| Domain service | logic that belongs to no single entity | `Domain/*Domain` |
| Application service | runs a use case, owns the state's lifetime | `Services/*Service` |
| Anti-corruption layer | translates a foreign model and hides its quirks | the ZenKit extensions (principle 5) |
| Domain event | a fact other contexts react to | `GlobalEventDispatcher` (principle 7) |
| Ports and adapters | the core defines interfaces, the edges implement them | `IContext*Service` + modules, Unity adapters |

DDD is a toolbox here, not a framework: no repositories or aggregates for their own sake. Most of Gothic's domain
lives in the scripts (principle 1), so bounded contexts, the ZenKit boundary and "one rule, one place" matter more
than rich models.

## Tools

- `architecture-reviewer` agent (`.claude/agents/`): reviews a diff or a folder against this doc, e.g. "use
  architecture-reviewer on my branch".
- `gothic-reviewer` agent: Gothic pitfalls and coding style.
- `feature-checklist` skill: config flags, logging, DI wiring, save/load impact.
