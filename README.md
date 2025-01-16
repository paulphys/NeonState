# NeonState
A Neon White mod that provides a Game State API for external programs to interact with the game.

> [!NOTE]
> This project is part of [`gym-neonwhite`](https://github.com/paulphys/gym-neonwhite), a Gymnasium environment for training agents to play Neon White.

## Game State

The game's state is exposed as JSON through a TCP socket on port 42069.

### Player

When the player is alive, the state will be:
```json
{
  "position": [-12.15, 50.32, -398.84],
  "velocity": [108.17, 0.00, -21.07], 
  "direction": [0.78, -0.57, -0.26],
  "is_alive": true,
  "is_dashing": true,
  "health": 3,
  "timer": 238,
  "active_card": "katana",
  "distance_to_finish": 376.88,
  "enemies_remaining": 13,
  "pov": []
}
```

| Field | Type | Description |
|-------|------|-------------|
| position | Vector3 | Player's position in world coordinates |
| velocity | Vector3 | Player's velocity vector |
| direction | Vector3 | Player's view direction |
| is_alive | bool | Whether the player is alive  |
| is_dashing | bool | Whether the player is dashing (using Godspeed card) |
| health | int | Health points |
| timer | long | Level timer in milliseconds |
| active_card | string | Selected card |
| distance_to_finish | float | Distance to level finish |
| enemies_remaining | int | Number of enemies left to defeat |
| pov | byte[] | Raw pixel data of player's view |

## Installation
1. Download [MelonLoader](https://github.com/LavaGang/MelonLoader/releases/latest) and install it on your `Neon White.exe`.
2. Launch the game once to create required folders.
3. Download the **Mono** version of [UniverseLib](https://github.com/sinai-dev/UniverseLib) and place it in the `Mods` folder.
4. Download `NeonState.dll` from the [Releases](https://github.com/paulphys/NeonState/releases/latest) page and place it in the `Mods` folder.
