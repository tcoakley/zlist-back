INSERT INTO AppVersions (Version, ReleasedAt, Notes)
VALUES (
	'1.1.0',
	GETUTCDATE(),
	'Subtasks, sign-in improvements, and small usability fixes.
- Added subtasks — break any checklist item into smaller steps, each with its own checkbox and progress tracking
- A checklist item with subtasks stays open while running a list until all its subtasks are checked off, and asks for confirmation if you try to complete it early
- Tap the initials badge next to a completed item to see the full name of who checked it off, helpful when two collaborators share initials
- Added Sign in with Google
- Pending list invitations now appear at the top of your Lists page with Accept and Decline buttons
- Added protection against repeated failed sign-in attempts
- Added a Settings option to control whether the keyboard opens automatically when adding items on mobile
- Line breaks in item descriptions are now preserved while running a list
- Various collaborator and subscription page improvements'
);
