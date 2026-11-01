# Open items to verify

The research could only read the Devpost pages second-hand (Cloudflare blocked them on Nov 1). Treat every Devpost quote as unverified until someone reads it in a browser. Fill in **Answer** with the source and date, then set **Status**.

Status: ⬜ open · 🟨 in progress / escalated · ✅ verified · ❌ blocker

| # | Item | Why it matters | Where to verify | Owner | Due | Status | Answer |
|---|---|---|---|---|---|---|---|
| 1 | Start program approval for at least one member (ideally all) | Mandatory. The application page has been reported broken since Oct 25 | Start application page, forum thread, Meta support | PRO | Nov 1 | ⬜ | |
| 2 | Developer Access enabled on each Meta account | Required by submission time | Meta account settings | PRO | Nov 1 | ⬜ | |
| 3 | India eligibility | Captured rules exclude only Brazil, Quebec and sanctioned regions, but unverified | Devpost rules "Eligibility", FAQ, Start application country list | PRO | Nov 1 | ⬜ | |
| 4 | Prize payment to India: banking or wire limits, tax withholding | Rules mention both | Devpost rules "Prizes" and "Taxes" | PRO | Nov 2 | ⬜ | |
| 5 | Age of majority for every member | 2025 required 18+ and local majority | Devpost rules | PRO | Nov 1 | ⬜ | |
| 6 | Prize stacking (track + specials); are specials open to every division? | Decides whether to spend any scope on specials | Devpost rules "Prizes" | PRO | Nov 2 | ⬜ | |
| 7 | Special-award definitions, amounts ($25k vs $20k), names | Only captured second-hand | Devpost overview and rules | PRO | Nov 2 | ⬜ | |
| 8 | Do Meta SDK samples (MR Tabletop), Asset Library models and third-party assets count as "pre-existing codebases"? | The New Experience division bans them, and we start from the MR Tabletop sample | Devpost rules/FAQ; ask on the Meta forum; keep dated git history | PRO | Nov 2 | ⬜ | |
| 9 | Team-size limit and Team Representative mechanics | Not found in research | Devpost rules | PRO | Nov 2 | ⬜ | |
| 10 | Policy on AI-generated code, art, voice; open-source licences | Only AI video is called out | Devpost rules; Developer Content Guidelines | PRO | Nov 3 | ⬜ | |
| 11 | Release channel mechanics: exact name "Competition", Invite URL, tester-account requirements | A broken judge install fails Stage One | Developer Dashboard; `hz-store-submit` skill | PRO | Nov 2 (test), Nov 16 (final) | ⬜ | Invite URL: |
| 12 | Which device judges use (Quest 3 or 3S) | Quest 3S has no depth sensor, so depth occlusion must degrade gracefully | Devpost FAQ; ask organisers | PRO | Nov 4 | ⬜ | |
| 13 | Hardware in hand: at least one Quest 3, ideally a 3S too | Frame rate and hand feel need a real device | Team inventory | PRO | Nov 1 | ⬜ | |
| 14 | Does the ISDK Eye Gaze building block fall back to head direction or to hand rays on Quest 3/3S? | `LookSource` needs head direction as the look source | Device test (Day 1 runbook, step F) | SYS | Nov 2 | ⬜ | |
| 15 | Does Meta XR Operator (OpenXR Plugin ≥1.17) coexist with the OVRPlugin microgesture path? | The plan uses both | Device test | SYS | Nov 3 | ⬜ | |
| 16 | Does our capture method record passthrough cleanly? | The trailer depends on mixed-reality footage | Device test | ART | Nov 6 | ⬜ | |
| 17 | Title availability and trademark check ("Sleight" is taken on itch.io) | Store and Devpost naming | Store and web search | PRO | Nov 8 | ⬜ | |

## Working assumptions until verified

| Topic | Assumption |
|---|---|
| Criteria weights | Four criteria at 25% each, behind a pass/fail Stage One |
| Special awards | $25k each; the exact name doesn't change the design |
| Prize stacking | Unknown. Don't spend scope on specials until confirmed |
| VR Glasses specs | Use Meta's compare page: 70°×66° FoV, eye tracking, eyes + hands input |
| Glasses face tracking | Not available to apps |
| Microgestures | Native Unity: yes |
| Shortest sessions | Leisure Lovers segment (Meta's primary source) |
