# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.6.0-pre.1] - 2026-02-09

### Added
- Added links to the Profiler Window for the profiling agent integration

## [1.5.0-pre.2] - 2026-01-15

### Added

- What's New section about points.

### Changed

- Changed package description.

### Fixed

- Fixed right-click issue in chat history.
- Fixed new missing permissions cost.

## [1.5.0-pre.1] - 2026-01-05
- Added image and screenshot attachments support with multiple format compatibility
- UI Builder Agent with a preview system
- Permissions system for file create/modify/delete actions with session overrides
- New Ask mode for question-focused interactions without executing actions
- Code Edit tools with diff and syntax highlighting
- Package Manager tools for add/remove/embed packages
- Project Overview tool for quick project structure summary
- New Orchestrator with improved multi-step workflows and automatic tool review
- Added reasoning blocks with auto-hide options

## [1.0.0-pre.12] - 2025-08-12
- Update AI-Toolkit to 1.0.0-pre.18

## [1.0.0-pre.11] - 2025-07-31
- Added tooltips to actions buttons
- Cannot press enter anymore to cancel chat progress
- Fuzzy matching improvements
- Added links to new docs
- Conversation is retained on domain reload
- Remove preview chips from routes

## [1.0.0-pre.10] - 2025-07-16
- Minor bug fixes
- Fixed issue where URLs were not being updated automatically for upgrading users
- Fixed issue where versions API endpoint reporting unsupported server versions was not shutting down the UI

## [1.0.0-pre.9] - 2025-07-15
- Return enter cancels prompt and resends it
- Fix code edit temp file
- Check for unauthorized code before loading the assembly
- Support function calling during streaming

## [1.0.0-pre.8] - 2025-06-27
- Moved Plugin Attributes back to AI Assistant

## [1.0.0-pre.7] - 2025-06-25
- Minor bug fixes
- Improved error handling for function calling
- Finalized onboarding content
- Disabled run when code won't compile
- Fixed performance issues for long conversations
- Improved history panel performance

## [1.0.0-pre.6] - 2025-06-05
- Bump AI Toolkit to 1.0.0-pre.12
- Fixed issue blocking .NET Standard Editor compatibility
- Minor UI fixes
- Minor bug fixes
- Improved attachment selection UX
- Fixed issue adding additional attached context to prompts
- Added support for array types as run parameter field.

## [1.0.0-pre.5] - 2025-05-14
- Improved Agent deletion logic
- Improved search window performance
- Improved automatic conversation naming
- Fixed issue with conversation history after editing agent actions
- Other small bug fixes

## [1.0.0-pre.4] - 2025-05-13
- Update AI-Toolkit to 1.0.0.-pre.7
- Added console tab attachments
- Updated to beta backend urls
- Fixed source attribution placement
- General usability fixes
- Fixed access token refresh issue
- Fixed bugs related to API alignment
- Assistant can now be used when the editor is paused

## [1.0.0-pre.3] - 2025-04-22

### Changed
- update AI-Toolkit to 1.0.0.-pre.4

## [1.0.0-pre.2] - 2025-04-16

### Changed
- Version Bump for re-release in production

## [1.0.0-pre.1] - 2025-04-11

### Changed
- Initial release of the AI Assistant Package
- Adds a menu item at `Window > AI > Assistant` to access tool
- Updated to interact with the new AI Assistant server using the AI Assistant protocol
