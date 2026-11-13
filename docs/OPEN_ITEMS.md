# Open items to verify

On Nov 13 the Official Rules, Judging, FAQ and Special Awards pages were read in full ([rules](https://start-developer-competition-26.devpost.com/rules), [judging](https://start-developer-competition-26.devpost.com/details/judging), [FAQ](https://start-developer-competition-26.devpost.com/details/faqs), [special awards](https://start-developer-competition-26.devpost.com/details/special-awards)). Answers below quote the section they come from. What's left open needs a person with the team's accounts.

Status: ⬜ open · 🟨 in progress / escalated · ✅ verified · ❌ blocker

| # | Item | Why it matters | Owner | Status | Answer |
|---|---|---|---|---|---|
| 1 | Start program membership for the person who submits | Rules §2(d): "Representative or person submitting an Entry must be a member of the Meta VR Start Program by the time of submission" | PRO | ⬜ | Check on the [Developer Forum](https://communityforums.atmeta.com/category/horizon-developer-forum): logged in, your name shows the flair "Start Member" or "Start Partner" (FAQ). No flair 24 h after applying → [support ticket](https://developers.meta.com/vr/support/) |
| 2 | Developer Access enabled on the submitter's Meta account | Rules §2(c), by the time of submission | PRO | ⬜ | Meta account settings |
| 3 | India eligibility | | PRO | ✅ | Eligible. Rules §2(b) excludes only Brazil, Quebec and comprehensively sanctioned areas (Crimea, Russia, Cuba, Iran, North Korea, Syria, OFAC-designated) |
| 4 | Prize payment to India | | PRO | ✅ | Rules §7: a bank account may be required for prizes ≥ $1,000; winners outside the US get IRS Form 1042-S and US withholding may reduce the payment; prize goes to the Representative |
| 5 | Age | | PRO | ✅ | Rules §2(a): every member at least 18 and the age of majority where they live |
| 6 | Prize stacking | Decides whether to spend scope on specials | PRO | ✅ | **No stacking.** Rules §7 "Limit one (1) prize per Entry"; FAQ: an entry in line for several gets the highest-value one. Specials are open to both divisions and all tracks |
| 7 | Special awards | | PRO | ✅ | $25,000 each: Social & Multiplayer, Agentic Interaction, Reason to Come Back, First Five Minutes, Accessibility Forward, Boldest Original Concept. Judge's Choice: 2 × $20,000. Track prizes: Best New Gaming $100,000, runner-up $50,000 |
| 8 | Do Meta SDKs, Building Blocks and samples count as pre-existing code? | New Experience bans pre-existing codebases | PRO | ✅ | Allowed. Rules §4(12): projects must use tools and SDKs "available to the general public and/or to Start Program members". New Experience = "conceived of and built within the competition window (starting September 24, 2026)". Our dated git history from Nov 1 is the evidence. Third-party assets need a licence we can show (Rules §4(12), §4: Sponsor "may at any time request written proof of ownership or proper license") |
| 9 | Team size, Representative | | PRO | ✅ | No maximum team size (FAQ). One Entry per team, submitted by one Representative, who receives any prize. A person can't be both a solo entrant and a Representative |
| 10 | AI-generated code, art, voice | | PRO | ✅ | No rule against AI-assisted code. Judging: "show real gameplay that honestly represents the experience … Don't lean on AI-generated video to carry the pitch." Rules §5: AI tools may assist judging; humans decide |
| 11 | Release channel and judge access | A broken judge install fails Stage One | PRO | 🟨 | Rules §4(6–9): APK uploaded to the Meta VR Developer Dashboard in a **new** release channel named exactly **Competition**; the Devpost form needs the **link to the project in that channel**. FAQ: "APKs will not be considered" on their own. Must stay available and free, unchanged, until the winner announcement (~Dec 11). Signed release keystore created Nov 13. Nov 13: app and Public channel "Competition" created; dry-run build 0.1.72522 (Day 9) uploaded with `tools/upload_competition.sh` (build ID 1352503974622724), Meta validation and postprocessing checks passed (one warning: 32-bit logging libraries packed by Meta's SDK). The final build replaces it. **Invite URL:** |
| 12 | Which device judges use | | PRO | ✅ | FAQ: "Judges will review apps on a Meta Quest 3 or Quest 3S." Prioritise Quest 3/3S performance; min 60 fps (Judging) |
| 13 | Do we need a headset? | | PRO | ✅ | FAQ: "No, you are not required to own a Meta Quest headset." Test and film with Meta XR Simulator |
| 14 | Does the ISDK Eye Gaze building block fall back to head direction or to hand rays on Quest 3/3S? | `LookSource` needs head direction | SYS | ✅ | **Hand rays.** v207.0.1 source: `GazeInteractionInstallationRoutine` calls `InjectOptionalEnableRayFallback(true)`. Our `LookSource` supplies head direction instead (Nov 1) |
| 15 | Meta XR Operator + microgestures coexistence | | SYS | ✅ | Not needed: no device, and neither is used in the build |
| 16 | Can we record passthrough cleanly? | The video | ART | ✅ | Rules §4(9): the video may show the project "via XR Simulator or another equivalent emulator". Simulator 207 records MP4 |
| 17 | Title availability and trademark ("Sleight" is taken on itch.io) | Store and Devpost naming; Rules §4(19) bans brand names and logos in the project | PRO | ⬜ | Search the Meta store, itch.io, Steam and a trademark database for "Misdirection" before Nov 17 |
| 18 | Age rating | Rules §4(15): must fit Meta's content guidelines for 10+, 13+ or 18+ | PRO | 🟨 | Builds are uploaded with age group TEENS_AND_ADULTS (13+), which avoids the extra requirements for apps aimed at children. Content (street magic, frowns, an "Hm!" heckle) fits 13+ |
| 19 | Language | Rules §4(16): English or English subtitles | PRO | ✅ | The game and all materials are in English |
| 20 | Keystore backup | Losing it means the app can never be updated | PRO | 🟨 | Created Nov 13 in `secrets/` (git-ignored), copy in `D:\backup\misdirection-secrets`. **Also copy it off this PC** (USB drive or password manager) |

## Deadlines

| What | When (IST) | When (PT) |
|---|---|---|
| Team target: Devpost marked **Submitted** | Tue Nov 17, evening | Tue Nov 17 |
| Team target: final edits done | Wed Nov 18, 18:00 | Wed Nov 18, 04:30 |
| Entry period closes (Rules) | **Thu Nov 19, 01:30** | Wed Nov 18, 12:00 |
| Judging | Nov 18 – ~Dec 9 | |
| Winners announced | ~Dec 11 | |

After the close, don't change the build or the submission. The FAQ allows bug-fix uploads until the close, then nothing until winners are announced.

## Judging, in short

- **Stage One (pass/fail):** the entry "reasonably fits the theme and reasonably applies the required tools and features". Hands-first end to end is a hard requirement.
- **Stage Two, 25% each:** Innovation & Creativity; Experience Design (onboarding, habit-forming purpose, purposeful passthrough, FoV-aware design); Technical Implementation ("gaze interactions (Unity/Unreal ISDK v207+), hand tracking, passthrough, spatial anchoring, and FoV-aware design", min 60 fps on Quest, MRUK and "holding up in rooms the developer never tested"); Polish & Presentation (UI/UX, art, sound, submission text, images, video).
