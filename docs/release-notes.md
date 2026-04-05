﻿[← back to readme](README.md)

# Release notes — Preexisting Relationship Redux
## 2.0.0
Rewritten for Stardew Valley 1.6+ and SMAPI 4.0+ by tbonehunter.  
Renamed from *Preexisting Relationship* (originally by spacechase0) to *Preexisting Relationship Redux*.

* Removed SpaceCore / SpaceShared dependency — mod is now fully standalone.
* Rewrote marriage selection menu using vanilla IClickableMenu components.
* Updated target framework from .NET 5 to .NET 6.
* Fixed deprecated API call: `Game1.getFarmer` → `Game1.GetPlayer`.
* Fixed `FarmHouse.getSpouseBedSpot` call to use NPC name (1.6 signature).
* Preserved all existing translations and multiplayer support.

## 1.0.6
Released 09 January 2022 for SMAPI 3.13.0 or later. Updated by Pathoschild.

* Improved translations. Thanks to wally232 (added Korean)!

## 1.0.5
Released 24 December 2021 for SMAPI 3.13.0 or later. Updated by Pathoschild.

* Updated for Stardew Valley 1.5.5.
* Improved translations. Thanks to ellipszist (added Thai)!

## 1.0.4
Released 15 October 2021 for SMAPI 3.12.5 or later. Updated by Pathoschild.

* Fixed controller not usable in the spouse selection menu.

## 1.0.3
Released 04 September 2021 for SMAPI 3.12.6 or later. Updated by Pathoschild.

* Improved translations. Thanks to Evelyon (added Spanish) and mcBegins2Snow (added Chinese)!

## 1.0.2
Released 10 July 2021 for SMAPI 3.9.5 or later. Updated by Pathoschild.

* Fixed marriage menu using the wrong NPC in some cases.

## 1.0.1
Released 19 June 2021 for SMAPI 3.9.5 or later. Updated by Pathoschild.

* Fixed compatibility with [unofficial 64-bit mode](https://stardewvalleywiki.com/Modding:Migrate_to_64-bit_on_Windows).
* Fixed manifest description and update keys (thanks to rikai!).
* Improved documentation.

## 1.0.0
Released 16 January 2021 for Stardew Valley 1.5.

* Initial release.
