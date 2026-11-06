# Hellbent: Story Bible

> **Status:** draft 4, the revenge story. Layer 1 is in the game (see section 10), except the final boss and the ending at the bottom of Hell, which aren't built yet. The team's decisions are in section 11.
> **Scope:** Layer 1 is the launch story. Layer 2 (the twist) is a free update after launch. Layer 1 plants hints for it, but every hint also makes sense without the twist.

---

## 1. Logline

**Layer 1:** A knight comes home to his family on the night Hell breaks through the ground. His sword shatters against the Demon King, and when the fires go out, his family is gone. With no blade left, he puts on the gauntlets of fallen knights and goes down through the dungeon, the caves and Hell itself for revenge.

**Layer 2 (update):** The knight is a character in a story that is told again and again. There is no family to avenge, only the storyteller's own loss. Revenge never brings anyone back, and the knight starts to notice.

---

## 2. Theme and tone

| | Layer 1 (launch) | Layer 2 (update) |
|---|---|---|
| **Core theme** | Revenge: going where no one comes back from, because Hell took everything | Grief: revenge was never going to fill the empty house |
| **Tone** | Storybook dark fantasy. Earnest, grim, never cynical | Quiet and eerie. The storybook frame starts to show its seams |
| **What the player believes** | "Hell killed my family. I'll make it pay." | "Who is telling this, and why do they need it to end in revenge?" |
| **Ending feeling** | Victory that feels hollow: "...Is that it?" | Bittersweet. The knight chooses to let go, or not yet |

**Rule for the writing:** the Narrator always speaks like a storybook ("And so the knight…"). The knight barely speaks. That gap is what Layer 2 plays with.

**The family stays vague.** Never named, never shown, never counted. In the levels they are only ever whispering voices. That keeps the player's own idea of them, and it is what makes the twist work.

**Recurring image: shattering.** The sword shatters, the screen shatters on death, the world cracks. In Layer 1 these are separate events. In Layer 2 they are all the same thing: the story breaking.

---

## 3. Cast

| Character | Role | Voice / notes |
|---|---|---|
| **The Knight** | Player character. Unnamed, always "the knight" (he). Blue tunic, helmet, gauntlets | Almost silent in Layer 1. In Layer 2 he starts to answer the Narrator back |
| **The Narrator** | Tells the story. Every line is the Narrator unless marked otherwise | Warm, theatrical, past tense: "The knight rolled aside, as knights do." Gets impatient when the knight dies: "No, no. That isn't how it goes." |
| **The family** | The knight's family, taken by Hell in the prologue. Never named or shown | Heard only as whispers in the levels ("...is that you?"). As the knight goes deeper, they ask him to stop |
| **Brutus, the Demon King** | Led the raid and broke the knight's sword. Boss 1, Dungeon (Demon Boss) | Proud and loud. Doesn't even remember the family's faces |
| **Pergius, Lord of Ruin** | Boss 2, Caves (Hell Lord) | Calm and ancient. Speaks as if he's read ahead |
| **The final boss** | Hell. Whoever sent the demons. A new boss, not designed yet | Its paint reveal should stop partway (see 5.4) |
| **The Shrine Spirit** | Voice of the Shrine of Upgrade | One line. Knows more than it says |
| **The Fallen Knights** | Others who swore revenge on Hell. Their swords stand around the pit; their gauntlets are the player's | In Layer 2, earlier tellings of the same story |

---

## 4. The world

- **The night it happened:** Hell split the ground open beside the knight's house. He fought the Demon King, and his sword shattered "like glass". By dawn the house was burned and empty.
- **The pit:** the hole the demons came out of, and went back down. Its edge is ringed with the swords of knights who went down before and never came back.
- **The Gauntlets** (Fire, Water, Earth, Wind, Lightning, Ice): taken from those fallen knights. Each pair "remembers" its wearer, which is why they come with skill gems built in.
- **Gems** grade from **Rough → Polished → Radiant → Perfect → Flawless**. In the lore, gems are crystallised courage. In Layer 2, they're the story's ink.
- **The descent:** the Training Grounds (tutorial) → the Dungeon (Brutus) → the Caves (Pergius) → Hell (the final boss).
- **Death:** the world cracks and shatters like glass (`DeathScreenShatter`), the same way the sword did. Layer 1 reading: "the knight's vision shatters." Layer 2 reading: the page tears, and the story starts again.
- **Bosses paint themselves in** (`BossPaintReveal`). Layer 1 reading: dark magic takes shape. Layer 2 reading: the story is drawing its villain as the knight arrives.

---

## 5. Story beats by scene

Format for each beat: **When** (the trigger in game) · **Who** · **Lines** · **Mode** (how it's shown; see section 6). Keep each line under about 90 characters. Localization keys are in `[brackets]`.

### 5.0 Prologue: New Run

**When:** **New Game** is pressed, before the Dungeon loads. Continue never plays it (Continue is greyed out when there is no save). **Mode:** illustrated pages: a pixel-art picture above the words, slowly zooming in, clicked through like a book. Hold ESC or Start for 2 s to skip.
**Who:** Narrator. **Pictures:** `Assets/Resources/Story/Story_Pro_0N.png`, drawn in `Docs/Art/prologue.aseprite` (one frame per page).

| # | Line | Picture | Key |
|---|---|---|---|
| 1 | Once, in a kingdom that no longer remembers its name, a knight came home to his family. | The cottage at dusk, warm windows, the knight on the path | `[Story_Pro_01]` |
| 2 | That night, the ground split open, and Hell came up through it. | The ground cracks, demons climb out, the roof burns, the knight draws his sword | `[Story_Pro_02]` |
| 3 | The knight's sword struck the Demon King true, and shattered like glass. | Close-up: the blade breaks on the Demon King. **When the player turns the page, it shatters** | `[Story_Pro_03]` |
| 4 | When the fires went out, the house was empty. His family was gone. | Grey dawn, the burned ruin, the knight kneeling by his broken sword | `[Story_Pro_04]` |
| 5 | The demons went back down: past the dungeons, past the caves, into Hell. | The glowing pit, demons going down the steps | `[Story_Pro_05]` |
| 6 | Many had sworn revenge on Hell. None came back. | The pit, ringed with the swords of fallen knights | `[Story_Pro_06]` |
| 7 | And then there was one more. | The knight at the edge, gauntlets glowing, backlit by the pit | `[Story_Pro_07]` |

**After page 7** the view falls into the pit: it zooms down past the knight, glows hot, goes black, and the Dungeon fades in as the knight lands.

> **Layer 2 hint:** "one more" is literal. In the update, this exact prologue plays again after the true ending. The sword breaking is the one scene the Narrator can never tell differently (see 8.3). The family is never drawn: the pictures show only the house.

### 5.1 Tutorial: the Training Grounds

**When:** the existing `TutorialManager` steps. **Mode:** the tutorial's own dialogue box. The story lines go in front of the existing control hints and **don't replace them**.

| Step | Lines | Key |
|---|---|---|
| Intro (before controls) | With no sword, the knight came to an empty field under an unfinished sky. | `[Story_Tut_Intro_01]` |
| | Learn to move, knight. The deep places are not kind to the clumsy. | `[Story_Tut_Intro_02]` |
| Punching | No blade? Then fists. Hit them like you mean it. | `[Story_Tut_Punch_01]` |
| Potion | Drink when you must. There are no inns in Hell. | `[Story_Tut_Potion_01]` |
| First gauntlet (reward) | These gauntlets belonged to a knight who went before you. They remember how to fight. | `[Story_Tut_Reward_01]` |
| Merge | Courage, pressed hard enough, becomes stone. Rough stones become Flawless ones. | `[Story_Tut_Merge_01]` |
| Shrine | A shrine. The old spirits still answer, once each. | `[Story_Tut_Shrine_01]` |
| Portal (end) | The way down is open. Somewhere below, the ones who did this are waiting. | `[Story_Tut_End_01]` |

> **Layer 2 hint:** the tutorial is a white grid under a painted sky, which is an unfinished page. "Unfinished sky" is the only nod.

### 5.2 Act 1: the Dungeon

**Arrival** (level start, after the spawn teleport effect) · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Narrator | The dungeon smelled of rust and old promises. | `[Story_A1_Arrive_01]` |
| Narrator | Somewhere below, the Demon King. Somewhere between, everything that wanted the knight dead. | `[Story_A1_Arrive_02]` |

**Family echo** (second chamber cleared) · **Mode:** Narration line, whispered (italic, soft blips)

| Who | Line | Key |
|---|---|---|
| Family (whisper) | ...is that you? You came back... | `[Story_A1_Echo_01]` |
| Narrator | The voices came from everywhere and nowhere, as voices do in stories like this. | `[Story_A1_Echo_02]` |

**Boss intro: Brutus** · **Mode:** Cutscene subtitles in `DemonBossIntro`

| When in cutscene | Who | Line | Key |
|---|---|---|---|
| Torches go out | Narrator | The torches died one by one, as if the room were holding its breath. | `[Story_A1_Boss_01]` |
| Paint reveal | Brutus | You! The little knight with the glass sword. | `[Story_A1_Boss_02]` |
| Camera pulls out | Brutus | Revenge? I don't even remember their faces. | `[Story_A1_Boss_03]` |

**Brutus phase 2** (at the 50% roar) · **Mode:** Narration line, fight keeps going

| Who | Line | Key |
|---|---|---|
| Brutus | ENOUGH! Let's see you dodge THIS. | `[Story_A1_P2_01]` |

**Brutus defeated** · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Brutus | This... isn't how it was supposed to end... | `[Story_A1_Win_01]` |
| Narrator | The Demon King fell. It did not feel the way the knight had hoped. So he went deeper. | `[Story_A1_Win_02]` |

> **Layer 2 hints:** Brutus can't remember their faces because there were no faces to remember. "Not how it was supposed to end" means "I was supposed to win this time." And the first taste of revenge is already hollow.

### 5.3 Act 2: the Caves

**Arrival** · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Narrator | Below the dungeon, the earth opened like an old wound. | `[Story_A2_Arrive_01]` |
| Narrator | The knight did not look back. Knights in stories rarely do. | `[Story_A2_Arrive_02]` |

**Shrine** (the first shrine used in a run, in any level) · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Shrine Spirit | You again. ...No. Someone like you. Choose, little knight. | `[Story_Shrine_01]` |

**Family echo** (second chamber cleared) · **Mode:** Narration line, whispered

| Who | Line | Key |
|---|---|---|
| Family (whisper) | Don't go any further. Please. Come home. | `[Story_A2_Echo_01]` |

**Boss intro: Pergius** · **Mode:** Cutscene subtitles in `HellLordIntro`

| When in cutscene | Who | Line | Key |
|---|---|---|---|
| Fog clears | Narrator | In the heart of the caves sat the Lord of Ruin, who had been waiting longer than the stone. | `[Story_A2_Boss_01]` |
| Paint reveal | Pergius | Ah. You're early. Or is it late? It is so hard to keep the pages straight. | `[Story_A2_Boss_02]` |
| Before the beam | Pergius | Let us see if this one ends differently. | `[Story_A2_Boss_03]` |

**Pergius defeated** · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Pergius | Go on, then. Something is waiting at the bottom. Something always is. | `[Story_A2_Win_01]` |
| Pergius | I hope it gives you what you want. | `[Story_A2_Win_02]` |

> **Layer 2 hint:** Pergius is the one character who half-knows. "Keep the pages straight" still reads as an ancient being's madness in Layer 1.

### 5.4 Act 3: Hell

**Arrival** · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Narrator | And at last, Hell. Hotter than the stories said. Quieter, too. | `[Story_A3_Arrive_01]` |
| Narrator | Somewhere in all that fire was whoever had sent the demons. The knight would find them. | `[Story_A3_Arrive_02]` |

**Grabbers and traps** (the first grab in Hell) · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Narrator | Hell holds on to what it catches. Mostly knights. | `[Story_A3_Grab_01]` |

**Family echo** (second chamber cleared) · **Mode:** Narration line, whispered

| Who | Line | Key |
|---|---|---|
| Family (whisper) | It won't bring us back. You know that, don't you? | `[Story_A3_Echo_01]` |

**Final boss: a new boss** *(not designed or built yet)*: whoever sent the demons. It should use `BossIntroCutscene` like the others. Lines to write once the boss exists. One suggestion to keep:

> Its paint reveal **stops at about 80%**: part of it stays clay-grey for the whole fight. In Layer 1 that's just a striking design. In Layer 2 it's the story running out of ink.

### 5.5 Ending, Layer 1 (launch)

**When:** the final boss is defeated. **For now** (no final boss yet) it plays when the player leaves Hell after a full run (`LevelTransition`). **Mode:** black pages

| # | Who | Line | Key |
|---|---|---|---|
| 1 | Narrator | And the knight cut down the last of them, and Hell was quiet. | `[Story_End_01]` |
| 2 | Narrator | His family was avenged. The kingdom would sing of it. | `[Story_End_02]` |
| 3 | Knight | ...Is that it? | `[Story_End_03]` |
| 4 | Narrator | The end. | `[Story_End_04]` |

**Back on the menu (one line):** `Narrator: ...Again?` `[Story_End_05]`

> A complete launch ending: the revenge is done, and it isn't enough. "Is that it?" and "Again?" are the hooks for Layer 2.

---

## 6. How dialogue works with gameplay

**The rule: the story never stops a fight.** Dialogue either waits for a safe moment, or rides along without taking control.

| Mode | What it looks like | Player control | How it advances | Used for |
|---|---|---|---|---|
| **Pages** | Black screen, a picture (if there is one) above typed words | None; time is stopped | Click, Space, Enter or gamepad A. Hold ESC or Start for 2 s to skip | Prologue, ending |
| **Tutorial box** | The tutorial's own dialogue box | Movement and attacks off | Click, Space, Enter or gamepad A | Tutorial |
| **Narration line** | One line near the bottom of the screen, fades on its own | Full control | After reading time | Level arrivals, echoes, chest, shrine, first grab, boss shouts and defeats |
| **Cutscene subtitles** | A narration line inside the letterbox bars | The cutscene has control | Timed to the cutscene. Skipping the cutscene clears them | Boss intros |
| **Death line** | A narration line under the Game Over menu | None; the knight is dead | Stays until the player restarts | Every death |

**Rules:**

- **One line at a time.** Narration lines queue. Moments that matter more clear the queue first: a death, a boss going down, a skipped cutscene.
- **Once per run.** Level arrivals, echoes, chest, shrine and grab play once per run. The prologue plays on every New Game.
- **Don't talk over notifications.** The first skill gem had a line, but the "New Skill Gem Acquired" panel covers the bottom of the screen at that moment, so it was cut.
- **Echoes** play when the player clears the second chamber of a level.
- **Light touch.** About 4 to 6 narrated moments per level.

### 6.1 Death lines (every death)

Drawn like cards from a shuffled deck: none repeats until all have been used. When a boss lands the killing blow, its own line is used instead.

| Line | Key |
|---|---|
| No, no. That isn't how it goes. | `[Story_Death_01]` |
| And the knight fell. ...Let's try that again. | `[Story_Death_02]` |
| That's not the ending. Not yet. | `[Story_Death_03]` |
| Somewhere above, a house stood empty, and waited. | `[Story_Death_04]` |
| Every story has a page like this one. | `[Story_Death_05]` |
| The knight got up. Knights in stories always do. | `[Story_Death_06]` |
| Hm. I'll tell that part better next time. | `[Story_Death_07]` |
| That was not the brave part. | `[Story_Death_08]` |
| A sword would have helped. If it hadn't broken. | `[Story_Death_09]` |
| Again, then. From the top. | `[Story_Death_10]` |
| Down, but not done. | `[Story_Death_11]` |
| The story paused, and waited for its knight. | `[Story_Death_12]` |
| *(Brutus)* Brutus laughed, and the dungeon laughed with him. | `[Story_Death_Brutus]` |
| *(Pergius)* "Not this time either," said the Lord of Ruin. | `[Story_Death_Pergius]` |

A restart then gets a restart line instead of the level's arrival lines: "Once, in a kingdom that no longer remembers its name..." `[Story_Restart_01]` or "From the beginning, then." `[Story_Restart_02]`

### 6.2 Other lines

| When | Line | Key |
|---|---|---|
| First gauntlet chest of a run | Two pairs of gauntlets. Two knights who never came home. Choose one to carry on. | `[Story_Chest_01]` |
| Loading screen | Every knight who came before wore the same gauntlets. | `[Story_Load_01]` |
| Loading screen | Gems are courage, pressed until it shines. | `[Story_Load_02]` |
| Loading screen | The Demon King has never lost a fight. Until he does. | `[Story_Load_03]` |
| *(held back for the update)* | No one remembers the faces of the knight's family. Not even the Narrator. | `[Story_Load_04]` |

---

## 7. Hints planted for Layer 2

| Hint | In the game? | Layer 1 reading | Layer 2 reading |
|---|---|---|---|
| The sword shatters like glass in the prologue, and the page shatters with it | ✅ prologue | A cursed Demon King | The first page tear, the moment the story can't change |
| Death shatters the screen like glass | ✅ `DeathScreenShatter` | Your vision breaks | The page tears |
| Bosses paint themselves in | ✅ `BossPaintReveal` | Dark magic forming | The story drawing its villain |
| The tutorial is a white grid under a painted sky | ✅ Tutorial scene | Training grounds | An unfinished page |
| The family is never named, shown or counted | ✅ prologue pictures, echoes | Grief too raw to describe | There was no family |
| Brutus doesn't remember their faces | ✅ dialogue | Cruelty | There were no faces |
| The family's voices ask the knight to stop | ✅ echoes | His own guilt | The storyteller arguing with themself |
| Gauntlets and swords of earlier knights | ✅ prologue, lore | Fallen avengers | Earlier tellings of the same story |
| "...Is that it?" / "...Again?" | ✅ ending | Revenge is hollow | Someone keeps restarting the story |
| The final boss is never fully painted | 📝 final boss | Striking design | The ink ran out |

---

## 8. Layer 2: "The Last Page" (free update after launch)

**Unlock:** clear the game once (recorded in PlayerPrefs `Story_ClearedOnce` from launch), then start a new run. The prologue plays again, word for word, but this time the knight interrupts.

### 8.1 New beats

| When | Who | Line |
|---|---|---|
| Prologue, page 3 | Knight | It always breaks. Why does it always break? |
| | Narrator | ...That isn't your line. |
| Prologue, page 4 | Knight | Their faces. I can't remember their faces. |
| Prologue, page 7 | Knight | One more. How many before me? |
| Tutorial intro | Knight | I've been here before. I know this sky. |
| Brutus intro | Brutus | You again? No... you remember, don't you. |
| Pergius defeat | Pergius | So you've seen the seams. Good. Look closely at what waits below. |
| Death (Layer 2 deck) | Narrator | Please. Just finish it. I need someone to finish it. |

### 8.2 True ending

1. The final boss falls. This time, its unpainted part spreads across the arena as everything turns to grey clay.
2. The knight climbs out of the pit and walks home. The door of the cottage opens, and **the house is empty**: a blank white grid inside, the same as the tutorial.
3. Dialogue (pages):

| Who | Line |
|---|---|
| Knight | There's no one here. |
| Narrator | ...No. There never was. |
| Narrator | There was a story about a knight who got his revenge, and someone needed to hear it end. |
| Knight | So you sent me down. Again and again. |
| Narrator | Every time, you got a little further. Every time, I thought: this time it will be enough. |
| Knight | The sword. It breaks every time, doesn't it. |
| Narrator | ...That's the part I can't change. That's the part that really happened. |
| Knight | Who are you telling this to? |
| Narrator | ...To myself, I think. |

4. **The final choice** (two buttons, reusing the chest panel's two-option layout):
   - **"Close the book."** The knight takes off the gauntlets and sets them down by the door. The screen shatters *slowly*, then fades to white. Narrator: *"The end. A real one, this time."*
   - **"Tell it again."** The knight walks back toward the pit. Narrator: *"...Once, in a kingdom that no longer remembers its name..."* This unlocks a New Game+ (a harder run, with Layer 2 lines everywhere).

### 8.3 Who is the Narrator? **Decided: B, a grieving storyteller**

Someone who lost their own family and couldn't stop it, and keeps retelling a version where they get to fight back. It's told only through implication; the real world is never shown.

- **The sword is the real moment.** The night the sword shattered is the one part that really happened (in some form): the storyteller tried and failed. That's why it breaks the same way in every telling.
- **Revenge is the story they tell themselves.** Killing Hell's lords never fills the empty house, which is why the ending asks "Is that it?"
- **Impatience on death** is grief, not cruelty: every failure is the original failure again.
- **The family is never seen** because the storyteller can't bring themself to picture them.
- **"Close the book"** is accepting the loss. **"Tell it again"** is not being ready yet. Neither is the wrong answer.

---

## 9. Voice

**Voice blips** (built): a short tone per letter as the text types, at a pitch for each speaker: the Narrator in the middle, Brutus and Pergius low, the knight lower, the family's whispers high and soft. Recording real narration for the key lines (the prologue, the ending, the Layer 2 conversation) can come later, for the update. AI voices are possible, but Steam asks you to disclose AI-generated content on the store page.

---

## 10. How it's built

Everything goes through `Assets/Scripts/Story/StoryDialogue.cs`, which creates itself when the game starts. The lines live in the **Story** localization table (`Assets/Localization/Story*`) in English, German, Japanese and Simplified Chinese; edit them in **Window > Asset Management > Localization Tables**. The German, Japanese and Chinese lines are first drafts for a native speaker to check.

| Beat | Where it's triggered |
|---|---|
| Prologue pages (with the page shatter and the fall into the pit) | `MainMenu.OnNewRunClicked` → `StoryDialogue.Tell` |
| Prologue pictures | Any page whose key has a picture in `Assets/Resources/Story/` (same name as the key) shows it. Source: `Docs/Art/prologue.aseprite` |
| Tutorial lines and blips | `TutorialManager` |
| Level arrivals, restart lines, "...Again?" | `StoryDialogue` itself, when a scene loads |
| Family echoes | `EnemySpawner.OnChamberCleared` (second chamber of a level) |
| Boss intro subtitles | `DemonBossIntro`, `HellLordIntro`; a skip clears them |
| Brutus phase 2, boss defeats | `DemonBoss.StartIntermission`, `DemonBoss.Die`, `HellLord.Die` |
| Chest, shrine, first grab in Hell | `GauntletChoiceUI.OpenChoiceMenu`, `ShrineInteractable.CompleteUse`, `TrapEnemy.TryApplyTrap` |
| Death lines | `DeathScreenShatter` (the killer picks a boss line) |
| Loading screen line | `LoadingScreen.LoadLevel` |
| Ending, and the "finished once" flag | `LevelTransition` at Hell's exit → `StoryDialogue.TellEnding` |

**Adding a line:** add a key to the Story table, then call `StoryDialogue.Say("Key", Speaker.Who)` (or `SayOnce`) where the moment happens.

**Adding or changing a picture:** edit `Docs/Art/prologue.aseprite` (192×108, one frame per page) and export the frame as a PNG named after the line's key into `Assets/Resources/Story/`. Import settings: Point filter, no compression, no mipmaps, Non-Power-of-2 set to None. Pictures work for the ending pages too.

**Not built yet:** the final boss and its lines, the true ending, and everything in Layer 2.

---

## 11. Decisions

| # | Question | Decision |
|---|---|---|
| 1 | The premise | Revenge for the knight's family, killed when Hell broke through (replaces "save the princess") |
| 2 | The knight's weapon | A sword, shattered against the Demon King in the prologue |
| 3 | Knight's name | Unnamed, always "the knight" |
| 4 | The family | Kept vague: never named, shown or counted. Heard only as whispers |
| 5 | Final boss | A new boss (whoever sent the demons), designed later |
| 6 | Death lines | Every death, from a shuffled deck |
| 7 | Narrator voice | Voice blips for now |
| 8 | Prologue art | Pixel-art pages drawn in Aseprite |
| 9 | Layer 2 timing | A free update after launch |
| 10 | Who the Narrator is | B, a grieving storyteller who lost their own family |
