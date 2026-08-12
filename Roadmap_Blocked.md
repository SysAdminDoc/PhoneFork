# Blocked Roadmap Items

## Real two-phone Samsung migration smoke test

- Evidence checked 2026-08-03. This validation requires two physical Samsung phones and an
  operator-authorized migration run. The current published-CLI ADB probe
  exposes one `emulator-5554` and one Samsung device, not the required pair of
  Samsung devices.
- Emulator fallback checked 2026-08-03: Samsung-to-emulator app, Pictures
  media, and secure-settings dry-runs completed with zero destination writes or
  errors. This validates the ADB/CLI compatibility path but does not replace
  the physical Samsung-to-Samsung gate.
- Scope: document one end-to-end source-to-destination smoke test before a
  public v1 release. Do not run a destructive migration against an emulator or
  an unapproved device.
- Unblock: connect and authorize two Samsung devices, then run and document the
  approved migration matrix with the operator present.

## Commercial-grade device corpus and test lab

- Evidence checked 2026-08-12. This item is explicitly gated on real demand after
  public releases. No demand signal, hardware budget, test-lab owner, or approved
  support matrix is available in this task, so implementation would require
  external resources and human prioritization.
- Scope: build a commercial-grade device corpus and physical test lab covering
  supported Samsung models, Android releases, One UI releases, USB/wireless ADB,
  and migration-domain fixtures once public-release demand justifies it.
- Unblock: establish demand, assign an owner and budget, and approve the supported
  model/OS matrix and destructive-test policy.
