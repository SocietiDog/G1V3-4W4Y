# Gw2Giveaway Release Checklist (No UI/Visual Changes)

## Scope guardrails
- Zero intentional visual/XAML layout changes.
- Runtime behavior preserved except bug fixes and safety hardening.
- Each task remains rollback-safe (isolated code paths).

## Task A — Startup/settings + channel rewards wiring
### Before
- `Trivia` settings loaded before `TriviaViewModel` was created.
- Channel point rewards were not reliably wired/persisted from settings.

### After
- `TriviaViewModel` is created before settings are applied.
- `MainWindow.ChannelPointRewards` is exposed for binding.
- Rewards are loaded from and saved to `AppSettings.ChannelPointRewards`.

### Validation
1. Launch app with pre-existing `settings.json`.
2. Confirm trivia color/reward settings load correctly on startup.
3. Add/remove channel rewards; restart app; verify rewards persist.

## Task B — Twitch/EventSub auth corrections
### Before
- IRC credentials used channel name instead of bot username.
- EventSub token/client-id handling was inconsistent and errors were hidden.

### After
- IRC login uses `BotName` (fallback to channel if empty).
- EventSub strips `oauth:` for Helix and accepts configured `ClientId`.
- EventSub lifecycle/auth failures are logged.

### Validation
1. Connect with valid bot username + token.
2. Verify IRC joins channel and chat send works.
3. Verify EventSub connects and subscription attempts are logged.

## Task C — Roll/overlay reliability
### Before
- Roll action could no-op if overlay window had not been opened first.
- Invalid image URLs could throw during roll/preview.

### After
- Roll path ensures overlay is created/configured before spin.
- Safe image loading helper prevents crashes on bad URLs.

### Validation
1. Launch app and click `ROLL WINNER` without manually opening overlay first.
2. Confirm roll starts and winner flow completes.
3. Test invalid prize image URL and confirm app does not crash.

## Task D — Exception/logging hardening
### Before
- Multiple silent catches reduced diagnosability.

### After
- Critical exception paths log to `Data/logs/app-YYYYMMDD.log`.
- User-visible behavior remains unchanged for non-fatal paths.

### Validation
1. Trigger a known failure path (bad API/network).
2. Confirm user gets existing message and log file receives details.

## Task E — Security token storage
### Before
- OAuth token persisted in plaintext.

### After
- OAuth token persisted as DPAPI-encrypted value (`CurrentUser`).
- Legacy plaintext setting can be read once and migrated on save.

### Validation
1. Save settings with OAuth token.
2. Inspect `settings.json`; verify encrypted field is present and plaintext is empty.
3. Restart app and verify OAuth still auto-loads.

## Task F — Smoke tests + release gate
### Manual smoke test matrix
1. **First run**: app launches, no crashes, default settings render.
2. **Settings persistence**: save/restart roundtrip for giveaway + trivia.
3. **Twitch connect/disconnect**: connect, chat send, disconnect.
4. **Entries flow**: start entries, add entrants via command/manual, stop entries.
5. **Roll flow**: prize-only, bank-only, random-pool modes.
6. **Prize bank**: open bank editor, edit slots, save/reload persistence.
7. **Trivia**: start/stop round, answer handling, leaderboard updates.
8. **Offline handling**: no internet -> graceful errors + logs.

### Build gate
- Build solution in `Release` configuration with 0 errors.
- Verify no visual diffs were introduced in XAML.

### Rollback safety
- If a task fails in QA, revert that task’s commit only:
  - A: startup/settings/rewards wiring
  - B: Twitch/EventSub auth
  - C: overlay reliability
  - D: logging hardening
  - E: secure token storage
