# AZ-5 Button — Antigravity Project Guide

## Overview
**AZ-5 Button** is a standalone, retro-styled emergency SCRAM button quick launcher for Windows 10 & 11 built with C# and Windows Forms.

- **Repository**: [https://github.com/sheeshfr/AZ-5_Button](https://github.com/sheeshfr/AZ-5_Button)
- **License**: GNU General Public License v3.0 (GPL-3.0)
- **Author**: SheeshFr

## Architecture & Conventions
- **Zero External Dependencies**: Pure C# using built-in Windows Forms and GDI+ rendering.
- **Entry Point & UI**: [`Program.cs`](./Program.cs) handles all rendering, slide animations, target management, and Win32 message filtering.
- **Visual Assets**:
  - `az5_button.png`: Embedded background casing resource.
  - `app.ico`: Embedded multi-resolution application icon.
- **Popout Menus & Trays**:
  - **Left Drawer**: 4 target slots + editable title header (toggled via bottom-left arrow button).
  - **Right Drawer**: 4 target slots + editable title header (toggled via bottom-right arrow button).
  - **Bottom Tray**: Active target file/folder path (permanently visible at all times).
  - **Top Drawer**: Quick instructions & help card (toggled via top-left spinning radiation button).
  - **Slot Rearranging**: Users can click and drag slots within or between drawers to rearrange them (clears original spot on move, swaps if dropping onto an occupied slot).
- **Transparency**: Uses `Color.Magenta` transparency key. Drawer outlines and chassis edges use `SmoothingMode.None` against transparent bounds to prevent pink anti-aliasing artifacts on Windows.

## Building & Running
- **Compile Command**:
  ```powershell
  powershell -ExecutionPolicy Bypass -File .\compile.ps1
  ```
- **Output Executable**: `AZ-5 Button.exe`
