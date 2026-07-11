INSERT INTO AppVersions (Version, ReleasedAt, Notes)
VALUES (
	'1.0.1',
	GETUTCDATE(),
	'Mobile usability and reliability improvements.
- Fixed the Create New List button sometimes being scrolled out of view after signing in on mobile
- Bigger, easier to tap drag handle and delete icons when editing a list on mobile
- Slightly larger item description text for better readability
- Launch button now shows a loading spinner and a clear message if your connection drops
- Added a confirmation step before Check All so it cannot be triggered by accident
- Fixed a visual overlap between the Save button and Help icon while editing a list
- Profile checkboxes can now be toggled by tapping the label, not just the icon
- Staying logged in is now more reliable across devices and after closing your browser'
);
