# Polished Semantic Syntax Colorizer

[![Visual Studio | Marketplace](https://img.shields.io/badge/Visual%20Studio%20|%20Marketplace-1.4.3-green.svg?colorB=00a84b&logo=data%3Aimage%2Fpng%3Bbase64%2CiVBORw0KGgoAAAANSUhEUgAAABwAAAAcCAYAAAByDd%2BUAAAAGXRFWHRTb2Z0d2FyZQBBZG9iZSBJbWFnZVJlYWR5ccllPAAAAgtJREFUeNq01s9LlEEcx3F3i1gUIkEsWJEiiZR2A0tJXIKuS5cW9GyE%2FgEZCOGC4MEfGB1ClD20Fy%2B6RURQQR4NikBBg06FINopjdZSWNun9%2BA8MD3OjM%2BzPg68Ls%2FM83yY7zwzzxNxHKcq5JbBLeSwcqBXBIboJtac%2FfYdeaQRc8eEHfbD0bdlDOFSJKSSpvAc9YeM%2BxQNIawDz3yEiXZKDYzjesCwdrzAWZ%2Fjy6L2UWTwDbvo97lmbVh3grVFceOgpuMpzljCWpW3MVCgKOl5zdTv4g2uaPquyjI2GMr2GdummorAkqHvBublRnZbUoY1Gu7Jowd7xlVkmlPKlMU6Fj1l%2BIsBWcavlnJNyHJfwG%2FbGqqBk%2BhGSTPY9JA93FfW9zK2TYEnPROOYQ418ixU%2B6s1BSriHgp%2B95F340eUteiVDzS1L0gHCdMFqk28MFuWfvE2LgQ9lkwzFFvlteVtFK0Lr1B3lMA%2FOIeXSPi4%2FzbeoqnSwIvyIE56rn%2FAuOEZ1%2FAOne5OsyZ6toWuzaJWvvIPLON%2Byo9tXLOX%2F9uH04bOkjxnvefogCV0B4%2BxaQvMaTo2cMdyeD88pCplW2ACBeXiezT7%2BDxlK%2FlaqA%2FowxOcDvAfM3SUwEoNBwhcCuOfJotRn2NPhPmbOGaZ2S%2FMIBX2j%2FCEJ2gVI2g5jh9h1yN8RC%2FqvP3%2FBBgAP9DoY%2BErIbwAAAAASUVORK5CYII%3D)](https://marketplace.visualstudio.com/items?itemName=LinkingMountains.PolishedSemanticColorizer)

## Overview

**Polished Semantic Colorizer** is a maintained fork of the archived Semantic Colorizer extension for Visual Studio. It retains all original Roslyn-based semantic highlighting features.

## Key Improvements

This fork resolves a known stability issue from the original project:
- Fixed an issue that caused VS2022 to hang during "Preparing Solution"

## Compatibility

Supports Visual Studio 2017 and newer (2019 / 2022 / 2026). Visual Studio 2015 is no longer supported.

## Highlight Coverage

This extension provides semantic highlighting for the following code elements:
- Class fields & Enum fields
- Static methods & Regular instance methods
- Constructors
- Type parameters & Method parameters
- Namespaces & Class properties
- Local variables & Special built-in types
- Local methods, Declaration methods & Built-in methods

## Customization

All semantic colors can be customized in Visual Studio via **Tools > Options > Environment > Fonts and Colors**. Look for display items starting with `Semantic *`.

## Recommended Companion Extensions

These extensions pair well with this tool for enhanced code coloring:
- **Viasfora**: Adds color highlighting for access modifiers and control flow keywords
- Visual Studio built-in **User Types** items: Customize display colors for classes, enums, delegates and other custom user types

## License

Licensed under Apache License 2.0. See the included `LICENSE` file for details.

## Contribute & Feedback

- Star the repository if you find this extension useful
- Report bugs or suggestions via issues
- Pull requests are welcome for further improvements
