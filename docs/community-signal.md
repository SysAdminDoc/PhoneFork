# PhoneFork — Community Signal Research

Research snapshot 2026-05-14. Mining of Reddit, XDA, Samsung Community, Hacker News, Android Central, GitHub Discussions, Stack Overflow, and tech-blog comments for pain points with existing Android phone-migration tooling (Samsung Smart Switch primarily, plus UAD-NG / Shizuku / Canta / Google Restore). Pattern threshold: complaints in 3+ independent threads = strong signal.

Source for the full agent transcript that produced this file: PhoneFork session 2026-05-14, Phase 1 community-signal research.

---

## §1 "Smart Switch only transferred app shells — no app data" (STRONG — 12+ threads)

Most-repeated gripe across every Samsung-adjacent forum. Apps land on the new phone but every login, configuration, theme, in-app save, and stored setting is gone. Users discover this only **after** wiping the old phone.

- https://forums.androidcentral.com/threads/samsung-smart-switch-does-not-transfer-apps.1020392/
- https://eu.community.samsung.com/t5/galaxy-s24-series/completed-full-smart-switch-process-none-of-the-apps-know-who-i/td-p/9102766
- https://forums.androidcentral.com/threads/replacing-phone-using-smart-switch-what-wont-transfer.1056747/
- https://us.community.samsung.com/t5/Galaxy-S25/SmartSwitch-quot-There-are-no-items-that-can-be-restored-quot/td-p/3165050
- https://eu.community.samsung.com/t5/galaxy-s25-series/apps-section-not-showing-in-smart-switch/td-p/12029744
- https://eu.community.samsung.com/t5/galaxy-a-series/smart-switch-installed-apps-but-no-icons/td-p/10468111
- https://news.ycombinator.com/item?id=19879358
- https://gist.github.com/MasonFlint44/4b32d86da40f79d12355bb993fe23953
- https://gist.github.com/AnatomicJC/e773dd55ae60ab0b2d6dd2351eb977c1
- https://github.com/timschneeb/debuggable-app-data-backup
- https://github.com/RikkaApps/Shizuku/discussions/203

**PhoneFork feature opportunity**: per-app data extraction pipeline using `adb shell pm path`, `run-as` for debuggable apps, `adb backup` fallback for legacy, plus a **brutally honest pre-flight scan** that tells the user *exactly* which apps will lose data and offers per-app cloud-export shortcuts.

## §2 "Wi-Fi passwords didn't transfer" (STRONG — 8+ threads)

- https://eu.community.samsung.com/t5/galaxy-s23-series/how-to-restore-wifi-passwords-from-previous-device/td-p/10785508
- https://xdaforums.com/t/transferring-wifi-passwords.3606429/
- https://eu.community.samsung.com/t5/galaxy-s24-series/transfer-saved-networks-from-huawei-or-xiaomi-to-samsung-s24/td-p/9300525
- https://mobiletechaddicts.com/does-smart-switch-transfer-passwords/
- https://us.community.samsung.com/t5/A-Series-Other-Mobile/Transferring-Samsung-Pass-to-a-new-phone/td-p/3555088
- https://r1.community.samsung.com/t5/samsung-wallet-pay/samsung-pass-service-migration-to-samsung-wallet/td-p/32144894
- https://joelchrono.xyz/blog/setting-up-phones-is-a-nightmare/

**PhoneFork feature opportunity**: Shizuku-bound `WifiManager.getPrivilegedConfiguredNetworks()` extraction + QR-code fallback for each network + selective import.

## §3 "Stuck at 99% / hung forever / transfer failed" (STRONG)

- https://us.community.samsung.com/t5/Galaxy-S24/Samsung-Switch-hangs-at-99-9-and-1-minute-remainin-when-trying/td-p/2877811
- https://r2.community.samsung.com/t5/Galaxy-S/S24-ULTRA-SMARTSWITCH-STUCK/td-p/16147179
- https://mobiletrans.wondershare.com/samsung-transfer/samsung-smart-switch-stuck-at-99.html
- https://www.airdroid.com/file-transfer/fix-samsung-smart-switch-stuck-issues/
- https://www.imobie.com/support/samsung-smart-switch-stuck-at-99.htm
- https://www.coolmuster.com/android/samsung-smart-switch-stuck.html
- https://www.urtech.ca/2025/05/solved-samsung-smart-switch-data-transfers-which-is-faster-usb-cable-or-wifi/
- https://eu.community.samsung.com/t5/galaxy-s22-series/smart-switch-problems/td-p/4867139
- https://eu.community.samsung.com/t5/mobile-apps-services/smart-switch-32-bit-for-pc-is-very-slow-we-need-a-64-bit/td-p/11960468

Root cause across many threads: 50,000+ SMS messages freeze the transfer at 99%.

**PhoneFork feature opportunity**: resumable chunked transfer with **per-data-type checkpoints**; PC-side parallel pipes exploiting USB-3.x bandwidth both sides.

## §4 "Required Samsung account / locked at OOBE" (5+ threads)

- https://eu.community.samsung.com/t5/mobile-apps-services/smart-switch-on-new-s23-ultra-not-allowing-receive-mode/td-p/8111453
- https://eu.community.samsung.com/t5/other-smartphones/can-t-sign-in-to-samsung-account-on-my-device-after-factory/td-p/5187453
- https://www.osnews.com/story/144520/setting-up-phones-is-a-nightmare/

**PhoneFork feature opportunity**: zero Samsung account, zero Google account, zero cloud round-trip — the differentiating headline.

## §5 "Phone got slower after migration / bloat carried over" (STRONG — every debloat thread)

- https://xdaforums.com/t/s25-ultra-debloat-and-privacy-list.4716655/
- https://xdaforums.com/t/s24-ultra-debloat-and-privacy-list.4654142/
- https://xdaforums.com/t/galaxy-s25-ultra-debloat-guide.4747503/
- https://github.com/itxjobe/samsungdebloat/tree/main/
- https://github.com/Achno/debloat-samsung-ADB-shizuku
- https://github.com/samolego/Canta
- https://eu.community.samsung.com/t5/galaxy-s24-series/soo-much-bloatware/td-p/11611403
- https://nelsonslog.wordpress.com/2023/12/22/migrating-to-a-new-android-phone-sucks/

**PhoneFork feature opportunity**: built-in debloat profile applied to NEW phone before migration. **Disable MSM Bloat-Installer first** to prevent reinstall-after-debloat.

## §6 "Banking apps / 2FA reauth required" (5+ threads)

- https://www.technibble.com/forums/threads/microsoft-authenticator.90656/
- https://www.quora.com/I-lost-my-Authenticator-code-because-I-changed-my-phone-How-do-I-reset-my-account-again
- https://news.ycombinator.com/item?id=42648597
- https://2fa.directory/us/

**PhoneFork feature opportunity**: pre-migration "authenticator audit" — scan for Google Authenticator, Authy, Microsoft Authenticator, Duo, bank tokens. Block migration with a wizard until user acknowledges.

## §7 "Secure Folder / Samsung Pass / Wallet missed" (STRONG — 8+ threads)

- https://xdaforums.com/t/secure-folder-not-restoring-by-smart-switch.4665109/
- https://eu.community.samsung.com/t5/mobile-apps-services/help-secure-folder-hasn-t-transfered/td-p/4086223
- https://r1.community.samsung.com/t5/secure-folder/does-smart-switch-transfer-secret-folder/td-p/20903282
- https://xdaforums.com/t/secure-folder-samsung-forgot-a-point-here-secure-folder-warning-on-samsung-switch-application.4569759/
- https://us.community.samsung.com/t5/Galaxy-S24/How-to-transfer-my-secure-folder-files-to-the-S24-Ultra/td-p/2790896
- https://r1.community.samsung.com/t5/galaxy-s/how-do-sync-wallet-to-new-phone/td-p/21432514
- https://us.community.samsung.com/t5/Samsung-Apps-and-Services/FAQ-Smart-Switch-keep-custom-notification-ringtone-settings/td-p/2333762
- https://forums.androidcentral.com/threads/bixby-routines-saving.1016854/
- https://us.community.samsung.com/t5/Suggestions/Backup-Bixby-Routines-Please/td-p/2407892

**PhoneFork feature opportunity**: explicit pre-flight scan listing Secure Folder presence, Samsung Pass entries, Wallet card count, Bixby Routines count, per-contact ringtone assignments, AOD config, Edge Panel layout — with deep links to in-app export tools.

## §8 "Smart Switch is one-way: new phone direction only" (4+ threads)

- https://eu.community.samsung.com/t5/discussion/downgrade-how/td-p/8247645
- https://eu.community.samsung.com/t5/questions/no-option-to-downgrade-on-smartswitch/td-p/4300646
- https://r2.community.samsung.com/t5/Galaxy-S/No-option-In-smart-switch-PC/td-p/12180690
- https://eu.community.samsung.com/t5/galaxy-s25-series/downgrade-to-7-0-from-8-0-with-smart-switch-downloading-mode/td-p/12516006

**PhoneFork feature opportunity**: NEW→OLD direction is a core feature, not an afterthought.

## §9 "Cross-region / CSC mismatch breaks restore" (3+ threads)

- https://r1.community.samsung.com/t5/others/change-country-region/td-p/8730462
- https://drfone.wondershare.com/transfer/samsung-smart-switch-not-working.html

**PhoneFork feature opportunity**: detect CSC + locale + region on both, side-by-side diff banner.

## §10 "I want selective restore — skip these specific apps" (Quieter but present, 4+ threads)

- https://xdaforums.com/t/samsung-smart-switch-select-what-apps-to-restore.3896118/
- https://r1.community.samsung.com/t5/samsung-smart-switch/mulitple-smart-switch-problems/td-p/16385617
- https://news.ycombinator.com/item?id=42648597
- https://github.com/seedvault-app/seedvault

**PhoneFork feature opportunity**: migration manifest UI with per-package checkboxes + presets + saved profiles + dry-run preview.

## §11 "Setting up a new phone is now a multi-hour nightmare" (HN front page 2026)

- https://news.ycombinator.com/item?id=47170958
- https://joelchrono.xyz/blog/setting-up-phones-is-a-nightmare/
- https://www.osnews.com/story/144520/setting-up-phones-is-a-nightmare/
- https://nelsonslog.wordpress.com/2023/12/22/migrating-to-a-new-android-phone-sucks/
- https://news.ycombinator.com/item?id=29213246
- https://news.ycombinator.com/item?id=19878620
- https://news.ycombinator.com/item?id=28751447

**PhoneFork feature opportunity**: single unattended pipeline — plug in two phones, click Start, walk away. Time budget as the headline metric.

---

## Synthesis — Top 10 Features Driven by Community Signal

1. NEW→OLD direction as a first-class mode (§8).
2. Pre-flight scan + "what cannot be transferred" report (§1, §6, §7).
3. Wi-Fi credential extraction with QR-code fallback (§2).
4. Resumable, chunked, parallel-pipe migration with per-data-type checkpoints (§3).
5. Built-in debloat profile applied to NEW phone with MSM-installer kill (§5).
6. Zero Samsung-account, zero Google-account, zero cloud (§4).
7. Per-package selective-restore UI with saved profiles + dry-run preview (§10).
8. App-data extraction pipeline + per-app cloud-export shortcuts (§1).
9. Default-app role restore (§1 Nova-launcher report).
10. CSC / locale / region diff warning (§9).
