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
| FrostyModManager | N/A | Add deselect all button to controls tab. | :grey_question: |
| FrostyEditor | N/A | Add an export button to the toolbar (next to New Project, Open Project, Save Project, and Save Project As). <!-- Maybe also FrostySdk; see changelog --> | :white_check_mark: |
| FrostyEditor | N/A | Add context menu options to bookmarks tab. | :grey_question: |
| FrostyModManager | N/A | Add "applied" state filter from @Skylark13's PR (CadeEvs/FrostyToolsuite#346). | :grey_question: |
| FrostyModManager | N/A | Add applied mod counter and available mod counter. | :grey_question: |
| FrostyModManager | N/A | Toggle selected button. | :grey_question: |
| FrostyModManager | N/A | Add "Copy to Clipboard" button for the Affected Files list. | :grey_question: |
| FrostyModManager | N/A | Hide the screenshots menu if there are no screenshots in the mod (to make use of all available space). | :grey_question: |
| FrostyCore | FrostyEditor | Port/modify the launch window for Kyber from Mophead's fork. | :grey_question: |
| FrostyEditor | N/A | Default folder icon for submenus. | :grey_question: |
| FrostyCore | BlueprintEditorPlugin | Add `Open in Blueprint Editor` button to the pointer ref dropdown in the property grid. | :grey_question: |
| FrostyEditor | BlueprintEditorPlugin | Add "Open in Blueprint Editor" ToolbarItem to default list. | :grey_question: |

## Credits

### `FlurryManagerPlugin/Images/InvertSelect.png`

<a href="https://www.flaticon.com/free-icons/select" title="select icons">Select icons created by Freepik - Flaticon</a>
