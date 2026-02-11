# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [1.6.0-pre.1] - 2026-02-09

## [1.5.0-pre.2] - 2026-01-15

### Added

- Added banner to show 10% of points remaining.

### Fixed

- Fixed some doc links from 6.2 to 6.3.
- Fixed object picker 'Generate New' button error under 6.4.

## [1.5.0-pre.1] - 2026-01-05

## [1.0.0-pre.20] - 2025-08-27

### Fixed

- Refresh generator models when connection is established.

## [1.0.0-pre.19] - 2025-08-14

### Changed

- For the purposes of AI Toolkit features and for the duration of access token validity, project connections now persist through failures of UnityConnect and Unity Hub. 

## [1.0.0-pre.18] - 2025-08-12

### Fixed

- Fixed refresh of settings and points when Editor Application is out of focus.

## [1.0.0-pre.17] - 2025-07-31

### Added

- Added AI packages out of date message banners.
- Added refresh of settings and points on Editor Application focus change.

### Changed

- Update SDK to version 0.24.0-preview.2.

### Fixed

- Fixed AI button not showing deducted cost.

## [1.0.0-pre.16] - 2025-07-16

### Fixed

- Better handling of settings and points fetching errors.
- Cleaner points and settings initialization when Editor Application is not in Focus.

## [1.0.0-pre.15] - 2025-06-27

### Changed

- Changed to a soft-dependency pattern to import Plugin attributes from AI Assistant for Generators.

### Fixed

- Fixed points refresh on access token refresh on domain reload, open, or play and stop.

## [1.0.0-pre.14] - 2025-06-26

### Added

- Added 'View Documentation' link.

### Fixed

- Fixed 'Manage Account' link.

## [1.0.0-pre.13] - 2025-06-25

### Added

- Added remaining points tooltip.
- Added 'hide AI menu' preference.
- Added manage account menu item.

### Changed

- Update SDK to version 0.21.0.

## [1.0.0-pre.12] - 2025-06-02

### Fixed

- Fixed internal account environment.

## [1.0.0-pre.11] - 2025-05-29

### Changed

- Run Unity Hub if it isn't running when refreshing the cloud access token.
- Show invalid cloud project banner if cloud settings not valid for 10 seconds or more.

## [1.0.0-pre.10] - 2025-05-27

### Changed

- Implemented an attempt to refresh the cloud access token when querying the Points Manager, if Unity Hub is running.

## [1.0.0-pre.9] - 2025-05-22

### Added

- Added support for Editor paused play mode.

### Changed

- Updated the 'Unityai' asset label to 'UnityAI'.

## [1.0.0-pre.8] - 2025-05-14

### Fixed

- Fixed some connection loading indicators.

## [1.0.0-pre.7] - 2025-05-12

### Fixed

- Fixed labels and copy.
- Fixed ai enablement flags sync with cloud project.

### Changed

- Update SDK to version 0.18.0.

## [1.0.0-pre.6] - 2025-04-30

### Fixed

- Fixed labels and copy.

## [1.0.0-pre.5] - 2025-04-23

### Fixed

- Fixed "Open Assistant" menu.

## [1.0.0-pre.4] - 2025-04-16

### Changed

- Update SDK to version 0.16.2.
- Update assistant plugins.
- Update ai-enabled for assistant and generators.

## [1.0.0-pre.3] - 2025-04-09

### Changed

- Update SDK to version 0.15.0.

## [1.0.0-pre.2] - 2025-03-27

### Changed

- Moved shared modules to generators namespace.

## [1.0.0-pre.1] - 2025-03-21

### Added

- Initial release of the AI Toolkit package.
