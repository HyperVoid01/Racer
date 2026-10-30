# Mzansi Rush – refactor, bug fixes, game feel & mobile support

Branch: `mobile-refactor`. Scenes and prefabs keep working as-is: every script kept its GUID, every
serialized reference still resolves, and every button binding still points at an existing method.
Runtime-only systems (touch controls, camera feel, world scroller, save system) create themselves, so
**no scene setup is required**.

> Not yet play-tested in the Unity editor. All 36 scripts compile at C# 9 against Unity API signatures
> (default, Android, iOS and Editor configurations). Open the project, let it import, and play through a few runs.

## Code structure

Scripts are grouped by responsibility:

| Folder | Contents |
|---|---|
| `Core/` | `GameManager` (run state + events), `Timer`, `Statistics`, `UpgradeManager`, `SaveSystem`, `Bootstrap` |
| `Taxi/` | `TaxiController`, `TaxiVisuals`, `TaxiFeel` (body lean/bounce), `TaxiEngineAudio` |
| `World/` | `WorldScroller`, `ScrollingObject`, `PrefabPool`, `Platform`, `Pothole`, `TaxiStop`, spawners |
| `Vehicles/` | `Vehicle`, `CarSlowDown`, `VehicleSpawner` |
| `Input/` | `TaxiInput` (one input layer for keyboard, gamepad and touch), `MobileControls`, `TouchButton` |
| `Feedback/` | `CameraFeel`, `Haptics`, `UIPunch` |
| `Audio/` | `AudioManager`, `Sound`, `Radio` |
| `UI/` | `UIController`, `MainMenuController`, `UpgradeMenuUI`, `SafeAreaFitter`, `RuntimeUI` |

Architecture changes:

- `GameManager` is a small state machine (`Driving`, `AtStop`, `GameOver` + pause) with C# events
  (`ScoreChanged`, `PassengersChanged`, `PauseChanged`, `GameOverStarted`, `GameOverScreenRequested`, `Impact`).
  Systems react to events instead of six scripts polling `gameOver` every frame.
- Score and passengers change through methods (`AddScore`, `TryAddPassenger`, …) rather than public fields.
- `UpgradeManager` stores only upgrade levels; stat modifiers and prices are derived from them. It no longer
  calls into UI code (the UI listens to its `Upgraded` event).
- `AudioManager` has a static, null-safe API (`AudioManager.Play("…")`), dictionary lookup instead of
  `Array.Find` per call, and volume categories (Music / Radio / SFX) inferred from sound names.
- All input goes through `TaxiInput`, so gameplay never reads `Input` directly.
- Singletons clear themselves in `OnDestroy`; managers needed everywhere (`Statistics`, `UpgradeManager`)
  are created on demand, so **GameLoop can be played directly in the editor** without errors.

## Bugs fixed

1. **Taxi stops only dropped off half the passengers** – the loop compared against a count it was decrementing.
2. **High score never saved** – the score was zeroed before being compared to the high score.
3. **No saving at all** – cash, upgrades, high score and settings were lost on quit. Now saved with PlayerPrefs
   (also flushed when the app is backgrounded on mobile).
4. **Possible softlock at a stop** – per-passenger delay divided by a count that could reach zero
   (`WaitForSeconds(Infinity)`).
5. **First two road pieces overlapped** (z-fighting) – the spawner used its own child as the first connection point.
6. **Radio “previous” played silence** once every five presses (wrapped at `< 0` instead of `< 1`).
7. **Resuming the game un-paused a radio the player had paused.**
8. **Street whistles stopped forever** after the first pause.
9. **Wall flags could get stuck** when sliding from one road piece’s wall to the next (now counted per contact).
10. **`CarSlowDown` could throw `NullReferenceException`** (`GetComponent` on a child collider).
11. **Cars could spawn on top of each other**, and traffic could block every lane – now checks lane clearance
    and always leaves one lane open.
12. **Potholes piled up while parked at a stop** (time-based spawning) – now spawned by distance driven.
13. **Crash physics applied a random force every frame** after game over, and the taxi fell through the road
    (its collider was a trigger) – now a single impulse + tumble on a solid collider.
14. **`StopCoroutine(StopTaxi())`** stopped nothing (new enumerator) – stop logic rewritten.
15. **Rims purchase reset the showroom rotation even when the purchase failed.**
16. **Showroom spin wrote directly into a quaternion component** (unnormalised rotation) – same angles, done properly.
17. **Sound lookups by a missing name threw exceptions** – now a single warning.
18. **Taxi stop seat limit was hard-coded** (`== 10`) instead of using `maxPassengers`.

## Optimisation

- **Object pooling** (`PrefabPool`) for road pieces, taxi stops, potholes and cars – no Instantiate/Destroy churn.
- **One `WorldScroller` update** moves the whole world, replacing one coroutine per object.
- Objects despawn just behind the camera instead of 250 units behind it (fewer live objects and colliders).
- Moving road pieces get kinematic rigidbodies (moving static colliders is expensive in PhysX); cars are kinematic.
- HUD text only updates when the value changes, using TMP `SetText` with numbers – **zero garbage per frame**
  (the old HUD allocated several strings every frame).
- Settings sliders are event-driven (previously every label every frame and every volume every physics step).
- **Audio import settings**: radio and menu music now *stream* (they were ~44 MB of WAV decompressed into memory);
  SFX are mono and preloaded, so the crash sound no longer hitches on first play.
- The old Destroyer trigger blocks are disabled at runtime (they generated physics contacts with the road).

## Game feel

- Smooth lane changes with acceleration instead of instant velocity; taxi **leans, yaws, squats and dives**.
- **Camera**: FOV widens and pulls back with speed, follows the taxi slightly, **screen shake** on potholes,
  wall scrapes and crashes.
- **Crash**: short slow-motion beat, physical tumble, then the game over screen with a **run summary**
  (reason, fare, best / NEW BEST!).
- **Engine audio**: idle pitch follows speed, acceleration loop fades in while throttling, brake screech only
  when actually braking hard.
- Taxi **eases to a halt** at stops; ramps back up to minimum speed instead of jumping to it.
- Passengers shout “stop here” when a stop is coming up and you have passengers.
- HUD: **+R / -R popups**, score and passenger punches, timer turns yellow and ticks under 5 s, red when late,
  pulsing “Pickup Passengers” notice.
- Garage: stat bars animate, prices turn red when unaffordable, “not enough cash” shake + flash, taxi model
  “pops” when rims are upgraded, smoothed showroom spin.
- Light difficulty curve: traffic and potholes get denser over ~6 km.
- Stops are quicker: at most 0.4 s per passenger (`maxSecondsPerPassenger` on the TaxiStop prefab) instead of
  always 5 s to drop off and 5 s to pick up.
- Pick-ups are now `minPassengers..maxPassengers` inclusive (was 5–9 because of an exclusive `Random.Range`).

## Mobile

- **On-screen controls** built from code: steering arrows (bottom-left), GAS / BRAKE pedals (bottom-right),
  HOOT and pause. Multi-touch, and you can slide a thumb between buttons. Sprites are generated at runtime.
- **Safe area** support (notches/cut-outs) for the touch controls.
- Canvas scalers set to **Expand**, so the 1920×1080 layout always fits wide phones (with “match width” the
  top HUD row was cut off on 20:9 screens).
- **Landscape only**, **60 fps** target, screen never sleeps during a run, auto-pause when the app is backgrounded,
  Android back button pauses.
- **Haptics** (short vibrations on Android, crash buzz on iOS); toggle stored in the save (`hapticsEnabled`).
- How To Play swaps keyboard instructions for touch instructions on mobile; Quit button hidden on iOS/WebGL.
- Keyboard now also supports arrow keys; gamepads work through the default Horizontal/Vertical axes.

### Testing touch controls in the editor
Open **Window > General > Device Simulator** and pick a phone. Unity reports a mobile platform there, so the touch
controls appear and can be used with the mouse. You can also force them with `TaxiInput.TouchControlsOverride = true`.

### Before publishing to Android
- Set a package name and app name in Player Settings (they are still `DefaultCompany` / `Racer`).
- IL2CPP + ARM64 is already selected (required by Google Play).
- The legacy `Destroyer` objects and the unused `Assets/Old` folder can be deleted.

## Update 2 – editable UI and touch controls in the editor

- **Run summary, score popups and touch controls are now normal scene objects you can lay out yourself.**
  With GameLoop open, run **Tools > Mzansi Rush > Create Editable Game UI** once, then move/restyle the new
  objects and save the scene. They are assigned on the UIController (new optional fields). Anything left
  unassigned still falls back to the runtime default.
  - Run summary: separate *Reason*, *Fare* and *Best* texts under the Death Screen (grouped under `RunSummary`),
    plus an optional *New Best Badge* object. Wording is editable on the UIController
    (`crashedMessage`, `gaveUpMessage`, `fareFormat`, `bestFormat`, `newBestMessage`).
  - Score popups: `ScorePopupTemplate` next to the score – put it where popups should start.
  - Touch controls: a `MobileControls` object in the UI canvas. Button sprites are saved to
    `Assets/UI/TouchControls` so you can replace them; each button's Image colour is its idle colour and
    `TouchButton.pressedColor` its pressed colour.
- **Fixed: touch controls didn't appear in the Device Simulator.** The simulator only simulates the
  `UnityEngine.Device` APIs, so mobile detection and the safe area now use those.
- **Show In Editor** toggle on `MobileControls` shows the controls in the normal Game view (usable with the mouse).
- Touch buttons now read input state through the Input System as well, so holding works in the simulator.
