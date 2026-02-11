---
uid: prompt-settings
---

# Use prompt settings menu

Open the prompt settings menu to access Assistant settings, refresh your project summary, view conversation overrides, and change Autorun and reasoning behavior.

Use the settings menu to:

- Access conversation-specific controls without leaving the Assistant window
- View which permissions you have overridden for the current conversation
- Clear permission overrides
- Toggle settings such as Autorun and collapse the **Reasoning** section in the response

## Prerequisites

To use the prompt settings menu:

1. Open the Assistant window.
2. Start a conversation.

## Open Assistant settings

The prompt settings menu provides a shortcut to the main Assistant settings.

To open the Assistant settings from the prompt:

1. Select the gear icon in the text field.
2. Select **Open Assistant Settings**.

The **Preferences** window opens at the Assistant section, where you can review global permission settings and other options. For more information, refer to [Configure Assistant permissions and preferences](xref:preferences).

## Refresh project overview

Use **Refresh Project Overview** to regenerate a summary of your current Unity project. This overview helps the Agent understand your project’s structure and intent so it can provide more relevant responses.

When you select this option, Assistant creates or updates a `Project_Overview.md` file in your project’s **Assets** folder. This file contains a structured overview of your project, including:

| Section name | Description |
| ------------ | ----------- |
| **Project Overview** | Describes the purpose of the project, the intended audience, and the core features that define the experience. |
| **Gameplay Flow/User Loop** | Explains how a user moves through the experience from launch to exit, including key states and transitions. |
| **Architecture (Runtime + Editor)** | Describes how major systems interact, including primary scenes, managers, patterns, and data flow. |
| **Scene Overview** | Lists the main scenes, their roles, how they load each other, and the rules that control scene flow. |
| **UI System** | Summarizes the UI framework in use, screen structure, data binding, navigation flow, and how to update UI screens. |
| **Asset & Data Model** | Explains how the project stores and organizes content, including prefabs, ScriptableObjects, addressables, and saved data. |
| **Project Structure (Repo & Folder Taxonomy)** | Shows how files and folders are organized in the repository and what conventions it uses to keep the structure consistent. |
| **Technical Dependencies** | Lists the Unity version, render pipeline, packages, plug-ins, SDKs, and any required external services. |
| **Build & Deployment** | Describes how to build, test, and deploy the project locally and in automated pipelines. |
| **Style, Quality & Testing** | Defines code standards, performance targets, profiling practices, and testing expectations. |
| **Notes, Caveats & Gotchas** | Captures edge cases, limitations, warnings, and common pitfalls to avoid. |

To refresh the project overview:

1. Select the gear icon in the Assistant text field.
2. Select **Refresh Project Overview**.

   When the process completes, Assistant saves the updated overview as a `.md` file. You can continue to use Assistant while the overview refreshes.

> [!IMPORTANT]
> Always use **Refresh Project Overview** after important project changes, such as updates to game flow, dependencies, scene organization, folder layout, or asset contents. When you refresh, Unity regenerates the `Project_Overview.md file` so Assistant can reference the latest project state. For best results, refresh the file whenever you make significant manual updates to your project to keep Assistant’s context accurate.

## Toggle Autorun from the prompt

You can enable or disable Autorun directly from the prompt settings menu.

To toggle Autorun:

1. Select the gear icon in the text field.
2. Select **Autorun** to enable or disable automatic execution of operations set to **Allow** or **Ask**.

When you enable Autorun, Assistant doesn't show permission prompts for operations that are set to **Allow** or **Ask Permission**. Operations set to **Deny** remain blocked. For more information, refer to [Enable Autorun](xref:preferences#enable-auto-run).

## Toggle collapse reasoning from the prompt

You can control whether Assistant collapses its **Reasoning** section when it finishes a response.

To toggle reasoning behavior:

1. Select the gear icon in the prompt field.
2. Select **Collapse Reasoning when complete** to enable or disable automatic collapsing of the **Reasoning** section.

When you enable this option, the **Reasoning** section is hidden by default when the response completes, and you can expand it if you need more detail. For more information, refer to [Control reasoning display](xref:preferences#control-reasoning-display).

## View and clear conversation overrides

When you allow an operation for a conversation, Assistant stores an override that applies to the current conversation only. You can review and remove these overrides from the prompt settings menu.

To view or clear overrides:

1. Select the gear icon in the text field to open the prompt settings menu.
2. Expand the **Permission Overrides** section and review any operations that show an override for the current conversation.
3. To remove an override, select **X** next to that operation.

After you remove an override, Assistant asks for permission again the next time it needs to perform that operation in the conversation.

## Additional resources

* [Assistant interface](xref:assistant-interface)
* [Assistant Ask and Agent modes](xref:assistant-modes)
