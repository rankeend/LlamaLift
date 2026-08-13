# LlamaLift Apple-inspired Desktop Design System

**Product:** LlamaLift — 本地模型，一键起飞。
**Stack:** Windows WinForms / .NET Framework 4.8 / AntdUI 2.4.4
**Direction:** Apple-inspired, restrained and professional without imitating macOS window chrome
**Dials:** variance 3/10 · motion 4/10 · density 7/10 · 4/8 px spacing rhythm

## Product principles

- Keep native Windows title bar, resizing, taskbar, tray, keyboard and accessibility behavior.
- Use hierarchy, whitespace, softened corners, low-saturation surfaces and a single accent inside the content area.
- Keep one visual primary action per page. Monitoring prioritizes legibility over decoration.
- All fonts, icons, charts and resources must work offline.
- Use semantic tokens; test light and dark palettes independently.
- Prefer Dock, Percent, AutoSize and vertical reflow. Never require horizontal scrolling.

## Semantic color tokens

| Role | Light | Dark |
|---|---|---|
| Page background | `#F5F5F7` | `#1C1C1E` |
| Primary surface | `#FFFFFF` | `#242426` |
| Secondary surface | `#FAFAFC` | `#2C2C2E` |
| Sidebar | `#EEEEF2` | `#202022` |
| Selected sidebar | `#FFFFFF` | `#3A3A3C` |
| Primary text | `#1D1D1F` | `#F5F5F7` |
| Secondary text | `#6E6E73` | `#A1A1A6` |
| Border / chart grid | `#D2D2D7` | `#3A3A3C` |
| Primary action | `#007AFF` | `#0A84FF` |
| Success | `#30B85A` | `#30B85A` |
| Warning | `#FF9F0A` | `#FF9F0A` |
| Error | `#FF453A` | `#FF453A` |
| Console | `#1C1C1E` | `#141416` |

## Typography

- Chinese UI: Microsoft YaHei UI, regular body and semibold/bold hierarchy.
- English and live metrics: Segoe UI.
- Commands, parameters and logs: Cascadia Mono, falling back to Cascadia Code or Consolas.
- Type scale: 8 / 8.5 / 9 / 9.5 / 11 / 14 / 18 pt.
- Never bundle, download or claim to use SF Pro.

## Components

- Cards: 16 px radius, 1 px semantic border, no heavy black outline or floating animation.
- Inputs: 10 px radius, 38 px target height, accent focus border and no opaque square backing.
- Buttons: 10 px radius, 38 px height, stable hover/pressed feedback with no layout shift.
- Sidebar: low-saturation background, white/neutral selected capsule, bold selected label.
- Charts: 2 px rounded line, 90-second ring buffer, subtle 0–28% area fill, dotted semantic grid.
- Every live chart also has a large current value, text title, unit, hover sample and pause control.
- Code and log regions stay dark in both themes and use explicit text severity labels.

## Monitoring information architecture

1. Live state, refresh cadence and pause/resume.
2. System KPI grid: CPU, memory, GPU, VRAM, server CPU/memory, disk and network.
3. System time series and detailed hardware/process text fallback.
4. Model KPI grid: prompt/generation speed, processing/deferred requests, context, tokens, slots and uptime.
5. Model time series and endpoint capability explanation.

Use `/metrics` when enabled and `/slots` as a compatible fallback. Missing counters display “不可用/待启用” rather than fabricated zeroes.

## Motion and interaction

- Keep feedback within 150–250 ms where the control library supports transitions.
- Animate only button feedback and chart progression; never animate layout bounds or page scroll.
- Provide pause/resume for continuously updating charts.
- Hover exposes history, but current values and status must remain visible without hover.
- Keyboard focus follows visual order and remains clearly visible.

## Responsive and DPI rules

- Minimum window: 940×600; standard validation: 1320×840.
- Validate 100%, 125%, 150%, 175% and 200% scaling.
- Four-column KPI grids keep compact labels and vertically scroll with the page.
- Model/program and runtime-parameter cards have independent vertical scroll regions.
- Horizontal AutoScrollMinSize remains zero. No title, input, button or rounded edge may be clipped.

## Prohibited

- Fake macOS traffic-light buttons, online fonts, network UI assets or decorative emoji.
- Excessive glass, glow, gradient backgrounds, large shadows or generic purple branding.
- Layout-shifting hover/press effects, continuous decorative animation or flashing live data.
- Color-only status, hover-only values, hidden focus states or unlabeled metric units.
- Fixed widths that cause horizontal scrolling or clipped right radii.

## Pre-delivery checklist

- [ ] Brand is consistently “LlamaLift — 本地模型，一键起飞。”
- [ ] Light/dark text and chart contrast validated independently.
- [ ] One primary action maximum per page.
- [ ] Monitoring can be paused and has visible numeric fallbacks.
- [ ] No emoji icons, online fonts, horizontal scroll or clipped rounded corners.
- [ ] 940×600, 1320×840 and 125/150/175/200% DPI screenshots pass.
- [ ] Offline tests, build, UI audit, portable archive and installer all pass.
