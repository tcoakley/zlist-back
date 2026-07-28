INSERT INTO AppVersions (Version, ReleasedAt, Notes)
VALUES (
	'1.0.3',
	GETUTCDATE(),
	'Cleaner navigation and faster sign-in.
- Signing in with Remember Me no longer flashes the login form while your session is restored in the background — shows a quick loading animation instead
- About page is now split into About and Version tabs, and can be viewed without signing in
- Rewrote the About page with more detail on how zChecklist is used, including team and family examples
- Profile page is now split into Profile and Settings tabs
- Renamed menu items to Profile / Settings and About / Version to reflect the new tabs
- Hid the back-to-Lists link on mobile while running a list, to declutter the toolbar now that Delete Run has been added'
);
