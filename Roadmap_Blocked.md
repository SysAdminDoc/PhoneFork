# Blocked Roadmap Items

## Real two-phone Samsung migration smoke test

- Evidence checked 2026-08-03. This validation requires two physical Samsung phones and an
  operator-authorized migration run. The current published-CLI ADB probe
  exposes one `emulator-5554` and one Samsung device, not the required pair of
  Samsung devices.
- Scope: document one end-to-end source-to-destination smoke test before a
  public v1 release. Do not run a destructive migration against an emulator or
  an unapproved device.
- Unblock: connect and authorize two Samsung devices, then run and document the
  approved migration matrix with the operator present.
