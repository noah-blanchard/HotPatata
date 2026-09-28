# HotPatata

A 2–4 player cooperative first-person parkour game where the team finishes a course while throwing a live bomb
(a potato) between them. Hold it too long and it explodes. Throw it into the world and it explodes. Catch it and its
fuse resets. Every explosion sends the whole team back to the last checkpoint.

Windows PC (Linux and macOS builds best effort, web planned), friend-hosted online sessions (join by code), Unity 6.3 LTS.

## Status

MVP prototype. The local pass loop, networking, the online lobby and one ~680 m greybox course (3 acts, 7 checkpoints)
are done, together with a fluid-movement and cartoon-look pass. Next come UX/readability (M6), hardening (M7) and an
external playtest. See the status table in [`docs/MVP_TASKS.md`](docs/MVP_TASKS.md).

## Getting started

Requirements:

- Unity **6000.3.25f1** (6.3 LTS) with Windows build support;
- Git.

Packages (URP 17.3, Input System, Netcode for GameObjects, Multiplayer Services, glTFast) resolve on first open.
Online sessions use Unity Relay, and the project is linked to a Unity Cloud project.

1. Clone the repo and open the folder in Unity Hub.
2. Open `Assets/Scenes/Bootstrap.unity` and press Play.
3. Choose **Host Online** (shows a code to share), **Join** with a code, **Play Local**, or **Direct IP**. Then pick
   the Course or the Sandbox.

Offline, one keyboard drives one of two players at a time: **Tab** switches player and **Esc** frees the cursor.
At the finish, the host presses **R** for a rematch.

Prebuilt builds (Windows; Linux and macOS best effort) are published on `v*` tags as GitHub Releases and on
[itch.io](https://nblxrd.itch.io/hotpatata). See [`docs/TESTING.md`](docs/TESTING.md#4-ci-and-releases).

## Controls

| Action | Keyboard / Mouse | Controller |
|---|---|---|
| Move / Look | WASD / Mouse | Left stick / Right stick |
| Jump | Space | South |
| Sprint (hold) | Left Shift | Left stick press |
| Crouch / slide (hold) | Left Ctrl or C | East |
| Throw | Left click: hold to charge, release to throw | Right trigger |
| Catch | Right click, timed: press as the bomb arrives | Left trigger |

## Documentation

| Doc | What it is |
|---|---|
| [`docs/PROJECT_SPEC.md`](docs/PROJECT_SPEC.md) | Gameplay source of truth: rules, bomb states, tuning values, scope |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Technical structure: scenes, components, authority model, networking, look |
| [`docs/MVP_TASKS.md`](docs/MVP_TASKS.md) | Milestone plan with acceptance criteria and current status |
| [`docs/TESTING.md`](docs/TESTING.md) | Test suites, dev tools, multiplayer bots, latency simulation, CI |
| [`AGENTS.md`](AGENTS.md) | Rules for coding agents working in this repo (`CLAUDE.md` imports it) |
