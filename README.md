# NeonState
A Neon White mod that provides a Game State API for external programs to interact with the game.

> [!NOTE]
> This project is part of [`gym-neonwhite`](https://github.com/paulphys/gym-neonwhite), a Gymnasium environment for training agents to play Neon White.

## Game State

The game's state is exposed as JSON through a TCP socket on port 42069, with updates by default every 50ms.

### Player
```json
{
  "position": [-12.15, 50.32, -398.84],
  "velocity": [108.17, 0.00, -21.07], 
  "direction": [0.78, -0.57, -0.26],
  "is_alive": true,
  "is_dashing": true,
  "timer": 2616,
  "health": 3,
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
| is_dashing | bool | Whether the player is dashing (using Godspeed) |
| timer | long | Level timer in milliseconds |
| health | int | Health points |
| active_card | string | Selected card |
| distance_to_finish | float | Distance to level finish |
| enemies_remaining | int | Number of enemies left to defeat |
| pov | byte[] | Raw pixel data of player's view |

## Game Interaction
🚧 **Work in Progress** 🚧

The mod listens for incoming commands on the same port and maps them to in-game actions.

- `forward`,`left`, `back`, `right`, `jump`
- `switch`: Switch cards
- `mouse1`: Fire/Use card
- `mouse2`: Discard card
- `camera`: Camera movement

 Commands should be sent as JSON messages, separated by newlines:
 ```json
 {"jump": true}
 {"camera": [0.97774, 0.0047471, -0.2097646]}
 ```

### Limitations
Only configured for the level [Thrasher](https://neonwhite.fandom.com/wiki/Thrasher) right now.

## Installation
1. Download [MelonLoader](https://github.com/LavaGang/MelonLoader/releases/latest) and install it on your `Neon White.exe`.
2. Launch the game once to create required folders.
3. Download the **Mono** version of [UniverseLib](https://github.com/sinai-dev/UniverseLib) and place it in the `Mods` folder.
4. Download `NeonState.dll` from the [Releases](https://github.com/paulphys/NeonState/releases/latest) page and place it in the `Mods` folder.
