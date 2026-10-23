# LesLeave AR

LesLeave AR is an augmented reality fire evacuation drill for Android, made in Unity. You scan the floor, place a classroom and corridor scene in your room, and walk through it to get out safely before the time runs out.

## What it does

- Scan the floor and place the scene with a ring.
- Raise the fire alarm, test doors for heat and stay below the smoke.
- Use the stairs, not the elevator, and reach the assembly point.
- Get a score out of 100 at the end.
- The five most recent sessions are saved on the phone and shown in the app.

## Requirements

- Unity 6000.6.2f1
- Packages: AR Foundation 6.6.2, ARCore XR Plugin, Universal RP, Input System, Addressables
- An Android phone that supports ARCore (Android 8.0 or higher)

## How to run it

1. Clone the repo and open the folder in Unity Hub with the version above.
2. Open the scene Assets/Scenes/Drill.
3. Build the Addressables content: Window > Asset Management > Addressables > Groups, then Build > New Build > Default Build Script. Switch the platform to Android first.
4. In File > Build Profiles, make sure Drill is the first scene and the platform is Android, then build and run on the phone.

You can also press Play in the editor. It uses a keyboard and mouse test mode instead of the camera: WASD to walk, Shift to run, hold right mouse to look, C to crouch, T to toggle lost tracking, left click to tap.

## How to play

1. Press Start and move the phone slowly over the floor until it scans.
2. Point the ring at a clear floor area and tap to place the scene.
3. Press Start on the briefing screen. A three second countdown begins.
4. Tap the fire alarm, tap doors to test them for heat, and walk through the cool door.
5. Crouch when the smoke comes down. Do not use the elevator.
6. Walk to the stairs, then to the assembly point.

## Code structure

Assets/Scripts is split into folders:

- Core: GameManager, EventBus and events, AssetService (Addressables), object pool and helpers
- Data: scenario and session classes, saving and loading, scoring
- Managers: simulation, states, tasks, entities, audio and UI
- Entities: door, alarm, exit, elevator, stairwell, assembly point, fire and smoke
- AR: AR provider interface with a phone version and a PC test version

Scenarios are JSON files in Assets/StreamingAssets/scenarios.

## Design patterns used

- State: the drill phases (menu, scanning, placing, briefing, running, paused, completed, failed)
- Observer: the EventBus, so managers and entities never call each other directly
- Factory: EntityFactory builds entities from the scenario file
- Strategy: scoring classes behind one interface
- Singleton: GameManager
- Repository: sessions are saved through an interface to JSON files
- Object pool: fire, smoke and rubble are reused

