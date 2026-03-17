# Flurry Plugin for Frosty Toolsuite

This is a plugin designed for v1.0.6.3 of FrostyToolsuite.
Custom forks or other versions of Frosty are not guaranteed to be supported.

The point of this fork is to port features from my fork of Frosty into a plugin that can be used on the vanilla build of Frosty.

## Feature List

(This list used to also include planned features. Those have been moved to the Issues tab.)

### Editor

- [x] Added a counter to the `View Instances` button that shows how many root objects an asset has.
- [x] Add an export button to the toolbar (next to New Project, Open Project, Save Project, and Save Project As). <!-- Maybe also FrostySdk; see changelog -->
- [x] Add context menu options to bookmarks tab.
- [x] Port/modify the launch window for Kyber from Mophead's fork.
- [x] Allow plugins to register icons for submenu folders.
- [x] Add `Open in Blueprint Editor` button to the pointer ref dropdown in the property grid.
- [x] Add "Open in Blueprint Editor" ToolbarItem to default list.
- [x] Add `Open in Blueprint Editor` context menu option.
- [x] Add `Copy GUID` context menu option.
- [x] Add images to context menu options that did not previously have them.
- [x] Add `Copy file path` context menu option.
- [x] Add counter for how many files are referenced.
- [x] Add counter for how many bundles the asset is in. Displays as `Bundles (<default> + <added>)`.
- [x] Autosave on export.
- [x] Fix the bug in the Bundles tab that prevents it from functioning for the first click.

### Mod Manager

- [x] Add "applied" state filter from @Skylark13's PR (CadeEvs/FrostyToolsuite#346).
- [x] Toggle selected button (with other functions; hover over it for details).

### Both

*Coming soon*

## Credits

### `FlurryManagerPlugin/Images/InvertSelect.png`

<a href="https://www.flaticon.com/free-icons/select" title="select icons">Select icons created by Freepik - Flaticon</a>

### `FlurryEditorPlugin/Images/Pencil.png`

<a href="https://www.freepik.com/icon/pencils_2829958#fromView=search&page=1&position=67&uuid=886e8a07-9119-44d4-8da2-451df209b38a">Icon by Freepik</a>

Image is color inverted.

### Harmony.WPF

Embedded library [Harmony](https://github.com/pardeike/Harmony), licensed under the MIT license.

### Diffplex.WPF

Embedded library [Diffplex](https://github.com/mmanela/diffplex), licensed under the Apache License 2.0.
