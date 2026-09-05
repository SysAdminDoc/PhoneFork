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

## F120 Run the helper provider pagination test

- Blocked 2026-09-05 on build resources, not on the work itself.
- The refactor shipped: `exportRows` now delegates its offset/limit arithmetic to `PageWindow`
  (`helper-apk/app/src/main/java/.../providers/PageWindow.kt`), and `PageWindowTest` covers 1,001
  rows at limit 500, exact multiples of the page size, off-by-one boundaries, page sizes 1 to 7 over
  20 rows, and parameter clamping. What has never run is the test itself.
- Attempts, all 2026-09-05:
  1. `:agent:agentJar` via the build governor failed with a bare `25.0.2`. Cause: `JAVA_HOME` points
     at Android Studio's JBR, a JDK 25 build Gradle 8.14.4 cannot parse. Fixed by overriding
     `JAVA_HOME` to Temurin 21.
  2. `:app:testDebugUnitTest` refused by the governor: 2.79 GB free against a 3 GB floor, with the
     build lock held by another repo's session.
  3. Same task, retried later: 0.02 GB free, lock held again; the governor acquired the lock and the
     build exited 1 with its output swallowed.
  4. Compiling `PageWindow.kt` and its test directly with `kotlin-compiler-embeddable` from the
     Gradle cache, bypassing the daemon. Got past `KMappedMarker` by adding stdlib, reflect,
     script-runtime and daemon-embeddable, then hit `kotlinx.coroutines.CoroutineScope`. Assembling
     the full compiler classpath by hand is a dependency hunt with no end in sight.
- Worth knowing: the defect this item was filed against appears not to exist. The original loop was
  hand-traced twice, once by the author and once by an adversarial review pass, for 1,001 rows at
  limit 500 and for exact-multiple and off-by-one cases; both traces returned every row exactly once
  with no gap at a page boundary. The refactor is still worth keeping for testability and for the
  clamping it added, but expect the test to pass on the first green run rather than expose a bug.
- Unblock: run the governor for `:app:testDebugUnitTest` with
  `JAVA_HOME` set to a JDK 21 while the machine has more than 3 GB free and no other session holds
  the build lock. One green run closes this.

## F125 Validate against One UI 8.5 and One UI 9 / Android 17

- Blocked 2026-09-05. Needs physical hardware this environment does not have, and inherits the same
  constraint as the two-phone smoke test above.
- Scope: record, per One UI version, whether `cmd wifi list-networks`, `cmd role get-role-holders`,
  `settings list`, `pm disable-user` and `pm install-create` still behave as PhoneFork expects, and
  either handle any divergence in code or name it as a known limitation in README.md.
- Specifically untested today: the `oneUi: ">=8.5"` override predicates in
  `assets/debloat/overrides.json` have never run against a device that matches them, and the
  Advanced Protection probe added in F114 reads candidate settings keys that no real device has
  confirmed.
- Unblock: connect a device on One UI 8.5 or One UI 9 and run the matrix with the operator present.

## F123 Grow the settings safety corpus beyond the current 38 safe rules

- Blocked 2026-09-05 on a missing fixture, not on the rule-writing.
- The acceptance is measured against "a captured S25-to-S22 diff fixture", and no such fixture is in
  the repository. CLAUDE.md records that a real capture happened during v0.3.0 hardware validation
  (1,062 keys on the S25 against 967 on the S22, 271 applicable), but only those counts were kept;
  the key list itself was never committed.
- Why this cannot be worked around: writing rules against a synthetic key list and then measuring
  coverage against that same list proves nothing. The 60 percent target is only meaningful against
  keys a real Samsung device actually reports.
- Current state for whoever picks this up: `SamsungSettingsCorpus` holds 38 Safe, 6 Review and 14
  Blocked rules. Everything else resolves to Unknown and is skipped unless the caller passes
  `--include-uncatalogued-settings`, which gives up the safety gate wholesale.
- Unblock: run `phonefork settings dump --device <serial> --out settings.json` on both phones, commit
  the resulting diff as a test fixture with any personal values scrubbed, then grow the corpus
  against it and add the coverage assertion.