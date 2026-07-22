# Launch checklist — Google Play

Everything needed to ship **Dice with Oma** on Google Play. Split into what the build already
handles (done in code, re-applied by `Yahtzee > Setup Project`) and what only you can do in the
Play Console or with legal/art work.

> Policies and thresholds below were current when written (2026-07). Google changes these — re-check
> the exact target-API level and testing requirements on the Play Console at submission time.

---

## 0. The one gating surprise: closed-testing requirement

New **personal** developer accounts (registered since late 2023) must run a **closed test with at
least 20 testers for 14 continuous days** before they can apply for production access. This is not
a code task — but it means "submit to production" is **~2+ weeks** after the account is ready, not a
same-day thing. Plan for it: get the account made and a closed test running early, even with a
rough build. An organisation account avoids this; a personal one does not.

---

## 1. Handled in the build (verify, don't redo)

Applied by `SceneBootstrapper.ConfigureForPlay`, run via **`Yahtzee > Setup Project`**:

| Requirement | State |
|---|---|
| 64-bit **ARM64** + **IL2CPP** | ✅ set |
| **Target API 35** (`AndroidTargetSdkVersion`) | ✅ set — bump if Play requires higher at submission |
| Min API 22 | ✅ (broad reach; no Play floor on min) |
| **App Bundle (.aab)** output | ✅ enabled |
| **No permissions** — INTERNET/storage forced off | ✅ (game is fully offline) |
| Launcher label **"Dice with Oma"** (was "yahtzee") | ✅ set |
| Portrait-locked | ✅ |
| "Yahtzee" trademark removed from UI → "Five of a Kind" | ✅ |

**⚠️ Run `Yahtzee > Setup Project` once against the real project.** These values are enforced in
code but only written to `ProjectSettings.asset` when that menu item runs. The editor was open
during development, so the committed asset may still show the old values (`AndroidTargetSdkVersion:
0`) until you run it. Confirm afterwards that the asset shows `35`.

**Build the store artifact:** `Yahtzee > Release Build (Android App Bundle)` → `Build/Smoke/dice-with-oma.aab`.
Verified to build clean (24.7 MB). It is signed with the **debug** keystore — fine for verifying it
builds, **not** uploadable. Real signing is §3.

---

## 2. Developer account (one-time)

- [ ] Create a Google Play Console account — **$25 one-time**, at play.google.com/console.
- [ ] Complete identity + (for personal accounts) the **D-U-N-S / address verification** Google now
      requires. This can take days — start early.
- [ ] Decide **personal vs organisation** account. Personal triggers the §0 closed-testing gate.

## 3. App signing (get this right once, back it up forever)

- [ ] Create a **release keystore** (`keytool -genkey -v -keystore dice-with-oma.keystore -alias
      diceoma -keyalg RSA -keysize 2048 -validity 10000`). In Unity: Player Settings → Publishing
      Settings → point at it, tick "Custom Keystore".
- [ ] **Back the keystore + passwords up in two places.** Lose it and you can never update the app
      under the same listing — there is no recovery.
- [ ] Enrol in **Play App Signing** (default for new apps): you upload with your key, Google
      re-signs for distribution.

## 4. Store listing assets (need real art — see §7)

- [ ] **App icon** — 512×512 PNG. The build currently ships Unity's default; replace it. Also set an
      **adaptive icon** (foreground + background layers) in Player Settings → Icon.
- [ ] **Feature graphic** — 1024×500 PNG.
- [ ] **Phone screenshots** — 2–8, 16:9 or 9:16. Use real device captures (`adb shell screencap`);
      the kitchen + a Results screen make good ones.
- [ ] **Short description** (≤80 chars) and **full description** (≤4000). Do **not** use the word
      "Yahtzee" anywhere in the listing — describe it as a five-dice game. Suggested short: *"A cozy
      dice game against Oma, your warm and cheeky grandmother."*
- [ ] Category (Games → Board or Casual), contact email, external store presence.

## 5. Compliance forms (in Console)

- [ ] **Privacy policy URL** — required even though the app collects nothing. Host a one-page policy
      stating no data is collected or shared. Free options: a GitHub Pages page, or a generator.
- [ ] **Data safety form** — declare **no data collected, no data shared**. This is honest because
      the app is offline with no analytics/network (permissions forced off, §1). Keep it that way.
- [ ] **Content rating** — complete the IARC questionnaire. A dice game with no violence/gambling
      (no real-money stakes) rates **Everyone / PEGI 3**. Note: the Store is cosmetic-only and
      **free**, so it is not "simulated gambling" or IAP — but see §6.
- [ ] **Target audience & content** — pick the age bands. If you include under-13, Families Policy
      and stricter ad/data rules apply; simplest is 13+.
- [ ] **Ads declaration** — the app has **no ads**. Declare so.
- [ ] **Government/News/COVID** declarations — all No.

## 6. Decisions still open (resolve before submitting)

- [ ] **Package id** is still `com.DefaultCompany.yahtzee`. Owner chose to leave it for now, but it
      is **permanent once published** and visible in the Play URL — and it still contains the
      trademark word. Strongly worth setting to something like `com.munruh.dicewithoma` before first
      upload (Player Settings → Other Settings → Package Name). After first publish it can never
      change.
- [ ] **The Store is real but thin** — one purchasable dice skin (Ruby), free, no points economy. It
      functions and persists, but as shipped it reads as a promise. Either flesh it out (more skins,
      an earn/points loop) or hide the Store button until it is ready. A visible-but-empty store can
      draw 1-star "nothing to buy" reviews.
- [ ] **Difficulty ε is not save-persisted** (HANDOFF §risks): resuming a saved game mid-way can
      diverge from an uninterrupted one at any difficulty above Sharp. Fix by persisting the ε draw
      count alongside `RngDraws` before release, or the resume-determinism guarantee is incomplete.
- [ ] **Unity splash** — Unity Personal shows a non-removable "Made with Unity" splash. If that is
      not wanted, it needs a Plus/Pro licence. Not a blocker.

## 7. Art still needed (the M4/M5 real-art pass)

The game is gray-box: primitive kitchen, purple-mannequin Oma. None of it is store-ready visually.
Before launch: the low-poly kitchen, the real Oma model with expressions, the app icon/feature
graphic, and a store screenshot pass. This is the largest remaining non-code chunk and needs the
artist (HANDOFF "What's left", M4 item 1 and M5).

## 8. Final pre-submit gate

- [ ] `Yahtzee > Setup Project` run; `ProjectSettings.asset` shows target API 35, product name
      "Dice with Oma".
- [ ] Real keystore configured and backed up.
- [ ] `dice-with-oma.aab` built release-signed, installed and smoke-tested on a device
      (`Tools\device-smoke.ps1` builds the APK equivalent; for the AAB use `bundletool` or an
      internal-testing track).
- [ ] EditMode + PlayMode green (`Tools\run-tests.ps1`).
- [ ] Listing text free of "Yahtzee"; no Hasbro assets anywhere.
- [ ] Data-safety, content-rating, ads, privacy-policy forms all complete.
