# R6 Planner

A planning tool for Rainbow Six Siege. Draw strategies, mark gadgets, and plan your rounds.
<img width="1919" height="1079" alt="image" src="https://github.com/user-attachments/assets/cb280b07-0ea2-4647-bf2d-e17e0127dafb" />

## Features

- Floor support (Bank, Clubhouse, Oregon, Chalet)
- Place attacker/defender tokens, drones, breach points, and objectives
- 15+ gadget types (Mute jammers, Kapkan traps, Bandit batteries, etc.)
- Draw lines of sight, arrows, rectangles, and freehand annotations
- Camera and spawn point editing
- Save/load plans as .r6plan files
- Undo/redo stuff
- Keyboard shortcuts for fastness

## Keyboard Shortcuts

### Tools
- `1-8` - Select tools (Select, LoS, Line, Arrow, Rectangle, Freehand, Text, Eraser)
- `F2` - Toggle spawn location edit mode
- `F3` - Toggle camera edit mode
- `Esc` - Select tool

### Colors
- `Q` - Yellow
- `W` - Red
- `E` - Blue
- `R` - Green
- `T` - Orange
- `A` - Purple
- `S` - Cyan
- `D` - White
- `F` - Deep Orange
- `G` - Bright Green

### Floors
- `NumPad 1-5` - Switch floors

### Actions
- `Ctrl+Z` - Undo
- `Ctrl+Y` - Redo
- `Ctrl+S` - Save plan
- `Delete` - Clear canvas
- `Ctrl+Scroll` - Cycle gadgets (when gadget tool active)
- `Middle Mouse + Drag` - Pan
- `Scroll` - Zoom

## Usage

1. Select a map from the left sidebar
2. Choose a floor
3. Pick a tool and color
4. Click or drag on the map to place/draw
5. Right click tokens to delete
6. Save your plan with Ctrl+S

## Requirements

- .NET 8.0
- Windows

## Building

```bash
start install.bat
dotnet build R6Planner.csproj
dotnet run
```
