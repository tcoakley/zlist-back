INSERT INTO AppVersions (Version, ReleasedAt, Notes)
VALUES (
	'1.0.2',
	GETUTCDATE(),
	'Reliability, connectivity, and run management improvements.
- Fixed getting logged out unexpectedly after a few days, even on devices set to remember you
- Added a No internet connection banner so it is clear when connectivity drops instead of the app appearing to hang
- Requests that get stuck now fail quickly with a clear message instead of hanging indefinitely
- The app now updates itself automatically after a new release, no more manually clearing your cache
- Added a heads up banner with a refresh button if you have the app open when a new version ships
- Checking the last item in a running list now asks for confirmation before completing it instead of completing automatically
- Fixed a bug where declining that confirmation could leave the Complete List button stuck disabled
- Added the ability to delete an entire run, with confirmation, from the active run screen and from history'
);
