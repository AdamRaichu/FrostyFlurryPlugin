# Flurry Plugin for Frosty Toolsuite

This is a plugin designed for v1.0.6.3 of FrostyToolsuite.
Custom forks or other versions of Frosty are not guaranteed to be supported.

The point of this fork is to port features from my fork of Frosty into a plugin that can be used on the vanilla build of Frosty.

[Harmony](https://github.com/pardeike/Harmony) is licensed under the MIT license.

## Feature List

:grey_question: means I haven't tried to add something. :x: means I tried and I wasn't able to get it working. :white_check_mark: means it is implemented.

| Parent Project | Integrates With | Description | Supported? |
| :---: | :---: | :--- | :---: |
| FrostyCore | N/A | Added a counter to the `View Instances` button that shows how many root objects an asset has. | :white_check_mark: |
| FrostyModManager | N/A | Add counter to tab titles for available and applied mods. | :grey_question: |
| FrostyModManager | N/A | Add deselect all button to controls tab. | :white_check_mark: |
| FrostyEditor | N/A | Add an export button to the toolbar (next to New Project, Open Project, Save Project, and Save Project As). <!-- Maybe also FrostySdk; see changelog --> | :white_check_mark: |
| FrostyEditor | N/A | Add context menu options to bookmarks tab. | :white_check_mark: |
| FrostyModManager | N/A | Add "applied" state filter from @Skylark13's PR (CadeEvs/FrostyToolsuite#346). | :grey_question: |
| FrostyModManager | N/A | Add applied mod counter and available mod counter. | :grey_question: |
| FrostyModManager | N/A | Toggle selected button. | :grey_question: |
| FrostyModManager | N/A | Add "Copy to Clipboard" button for the Affected Files list. | :grey_question: |
| FrostyModManager | N/A | Hide the screenshots menu if there are no screenshots in the mod (to make use of all available space). | :grey_question: |
| FrostyCore | FrostyEditor | Port/modify the launch window for Kyber from Mophead's fork. | :white_check_mark: |
| FrostyEditor | N/A | Default folder icon for submenus. | :grey_question: |
| FrostyCore | BlueprintEditorPlugin | Add `Open in Blueprint Editor` button to the pointer ref dropdown in the property grid. | :white_check_mark: |
| FrostyEditor | BlueprintEditorPlugin | Add "Open in Blueprint Editor" ToolbarItem to default list. | :white_check_mark: |
| ReferencesPlugin | BlueprintEditorPlugin | Add `Open in Blueprint Editor` context menu option. | :grey_question: |
| ReferencesPlugin | N/A | Add `Copy GUID` context menu option. | :grey_question: |
| ReferencesPlugin | N/A | Add images to context menu options that did not previously have them. | :grey_question: |
| ReferencesPlugin | N/A | Add `Display Reference Details` from Mophead's fork. | :grey_question: |
| ReferencesPlugin | N/A | Add `Copy file path` context menu option | :grey_question: |
| ReferencesPlugin | N/A | Add counter for how many files are referenced. | :grey_question: |
| BundleEditorPlugin | N/A | Add counter for how many bundles the asset is in. Displays as `Bundles (<default> + <added>)`. | :grey_question: |
| ReferencesPlugin | N/A | Add options to hide MVDBs and NetRegs from the "references to ___" list. | :grey_question: |
| FrostyEditor | N/A | Autosave on export. | :white_check_mark: |

## Credits

### `FlurryManagerPlugin/Images/InvertSelect.png`

<a href="https://www.flaticon.com/free-icons/select" title="select icons">Select icons created by Freepik - Flaticon</a>

### `FlurryEditorPlugin/Images/Pencil.png`

<a href="https://www.freepik.com/icon/pencils_2829958#fromView=search&page=1&position=67&uuid=886e8a07-9119-44d4-8da2-451df209b38a">Icon by Freepik</a>

Image is color inverted.
