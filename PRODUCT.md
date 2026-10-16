# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Existing codebase: WPF (XAML) desktop app on .NET 10 (`net10.0-windows`), MVVM (View/ViewModel/Command). Not a web surface; the `web` platform value is the schema default, as no native desktop value exists.

## Users

Advanced / technical Windows users who want to free RAM on demand or automatically (tray/idle), and are comfortable with options like standby list and modified page list.

## Product Purpose

Optimizes Windows memory areas (working set, standby list, modified page list, file cache, etc.) through the native API. Success: memory is freed reliably with minimal CPU/RAM overhead of the tool itself.

## Positioning

Personal fork of IgorMundstein/WinMemoryCleaner: lightweight and reliable. .NET 10, low CPU/memory at idle, no busy-wait loops, concurrency fixes, secure updater (SHA-256 verification).

## Operating Context

Runs elevated (manifest `requireAdministrator`), Windows 10 1607+, optionally as a Windows service installed via `sc.exe`. Releases are published from this repository.

## Capabilities and Constraints

- Requires administrator; requires .NET 10 Desktop runtime.
- Auto-update points to this fork's releases.
- GPL-3.0; original code by Igor Mundstein.
- Docs and comments in pt-br; code in English.

## Brand Commitments

None binding. The user stated there are no visual constraints; the UI may be redesigned freely.

## Evidence on Hand

README.md, CHANGELOG.md, docs/. No testimonials, benchmarks or user metrics exist; do not fabricate.

## Product Principles

- Do the job with the smallest possible footprint.
- Be trustworthy: correct, safe, and honest about what was freed.
- Serve power users: expose real options without hiding them.
- Stay out of the way: idle quietly, act when needed.

## Accessibility & Inclusion

No product-specific requirement established.
