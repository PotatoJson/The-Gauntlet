# Hellbent: Story Bible

> **Status:** draft 5, the revenge story. Layer 1 is in the game (see section 10), the final boss included. The team's decisions are in section 11.
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
| **The Unfinished Knight** | The final boss, at the bottom of Hell. Whoever sent the demons turns out to wear the knight's own face: his reflection, stepped out of a mirror as a shadow of ink | The Narrator's shadow: the storyteller's grief, written as the villain. Never fully painted (see 5.4) |
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

**Brutus phase 2** (at the 50% roar) · **Mode:** a short cutscene (`DemonBoss.Enrage`): the camera takes in his roar, which blasts out across the floor and breaks every ward round the arena

| Who | Line | Key |
|---|---|---|
| Brutus | ENOUGH! Let's see you dodge THIS. | `[Story_A1_P2_01]` |

**Brutus defeated** · **Mode:** Narration line

| Who | Line | Key |
|---|---|---|
| Brutus | This... isn't how it was supposed to end... | `[Story_A1_Win_01]` |
| Narrator | The Demon King fell. It did not feel the way the knight had hoped. So he went deeper. | `[Story_A1_Win_02]` |

> **Layer 2 hints:** Brutus can't remember their faces because there were no faces to remember. "Not how it was supposed to end" means "I was supposed to win this time." And the first taste of revenge is already hollow.

**The fight** (`DemonBoss`, the Dungeon's boss platform over the spike pit):
- **The wards:** as the fight starts, a ring of iron posts with glowing fire-glass between them rises round the edge of the platform (`WardRing`), so nobody goes off into the spikes. Each pane takes two blows: it cracks at the first and shatters at the second, leaving a gap.
- **The charge:** whenever the player is 5 m or more away and it's ready (every 4 seconds) he winds up (head down, foot scraping; a burning stripe on the floor shows the line once it's set) and charges along it at 26 m/s. Rolled clear, he smashes into the ward at the end and stands dazed for 2 seconds: the moment to punish him. Caught, the player is carried and thrown into the ward: 55 damage and a knockdown, and the pane takes the blow.
- **Up close:** now and then, instead of a blow, he grabs (30% of the time in reach, at most every 8 seconds; a roll gets out of it). Phase 1: both hands seize the player, lift them overhead and hurl them into the ward he's facing, 45 damage (over the edge, onto the spikes, where that ward is broken). Phase 2: he runs them through with a sword (20), lifts them on the blade, spins a turn and a quarter and flings them across the floor (40), landing short of the edge.
- **Phase 2:** his roar breaks every ward (above). From then on a throw has nothing to stop it: over the edge onto the spikes, which kills outright, and a charge he misses just skids to a halt at the edge.
- **Animation:** the wind-up, the run, the carry, the throw and the daze are new (`Docs/Art/InkShadow.blend`, actions `Brutus_*`, retargeted onto him); the ward post is `Docs/Art/BrutusWard.blend`.

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

**Pergius hides** (`HellLord.Fog`): as the fight starts, and again at 40% health, he vanishes into one of five banks of fog round the edge of the floor. Waves of demons (three waves: two grunts and a caster; two grunts and two eyeballs; two elites) come out of the fog, and every bank fires lasers at the player (a thin warning line, then the beam down it). A faint pair of red eyes in his bank gives him away up close. Walking into his bank drags him out, stunned for 3 seconds; walking into the wrong one bursts it in their face and stuns them for 2 seconds. Left alone for 35 seconds, he steps out on his own.

| Who | Line | Key |
|---|---|---|
| Pergius *(hiding)* | Find me, if those eyes of yours are worth anything. | `[Story_A2_Hide_01]` |
| Pergius *(found)* | No... How did you see me? | `[Story_A2_Found_01]` |

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

**Final boss: the Unfinished Knight** (`InkShadow`, the Narrator's shadow made of ink). At the bottom of Hell there's no throne, only a tall mirror. It waits: the player can use the arena's two shrines and open its two chests first, and starts the scene by looking into the mirror (the Interact key, standing near it). The knight walks up to it and sees himself. He tilts his head and the reflection doesn't; it drains to black ink, white eyes open in its helmet, it walks up to the glass and cracks it, and the glass bursts. The knight rolls back, and the shadow rises out of the ink in front of the empty frame, twice his size, carrying the sword he lost. **Mode:** cutscene (`InkShadowIntro`), then fight lines

| Who | Line | Key |
|---|---|---|
| Narrator | At the bottom of Hell there was no throne, and no army. Only a mirror. | `[Story_A3_Boss_01]` |
| Narrator | And in it, the knight saw the one he had come so far to find. | `[Story_A3_Boss_02]` |
| Shadow | Did you think it would have a different face? | `[Story_A3_Boss_03]` |
| Shadow | Every knight who comes for revenge finds me. I always look like them. | `[Story_A3_Boss_04]` |
| Shadow | Come, then. Let's finish the story. | `[Story_A3_Boss_05]` |
| Shadow *(half health: the ink spills)* | You can't kill ink. You can only spill it. | `[Story_A3_P2_01]` |
| Shadow *(defeated)* | ...There. Now the story can end. | `[Story_A3_Win_01]` |
| Narrator *(defeated)* | And the shadow ran out, like the last of the ink in a pen. | `[Story_A3_Win_02]` |
| Narrator *(death, killed by it)* | The knight fell, and the shadow wore his face a little better. | `[Story_Death_Shadow]` |

> **Layer 2 hints:** "I always look like them" is every earlier telling, each with the knight's face. "Let's finish the story" and "now the story can end" are the Narrator's own wish.

**The fight** (`InkShadow`, Hell's boss chamber, alone, no adds):
- **Phase 1:** a sword fight. In reach: a **rising cut**, its quick attack (it sinks low with the blade trailing behind it, then lunges in and brings it up from the floor: roll to the side), a **horizontal cut** (the blade drawn back at its waist, then swept round in front: roll back), a three-hit **combo** (a cut, the backhand back across, then a step in and an overhead chop), and now and then a heavy slam that splashes ink and cracks the floor. Against a player who keeps their distance it **throws its sword**, point first, curving after them (it turns only so fast, so a roll bends its path). The shadow bursts into ink as it lets go, then appears in the air to catch the sword as it closes on them, held in the stance of its next cut while the whole game runs in slow motion (0.3 seconds at a fifth of normal speed) and it falls to the floor: the moment to read it and roll the right way. Then it cuts. It reads the player like a mirror: a charged heavy attack let go in its reach has a 40% chance to make it hop back out of it. Its footwork: just out of reach it sometimes **circles** the player, sword raised; at 5 to 9.5 m it **lunges**, a long thrust stretched to reach them (closer than 5 m it hops back first and lunges from there); a player who stays **behind** it for more than about half a second gets a spinning backhand. In reach it sometimes **grabs** with its claw instead of cutting: it can't be blocked, only rolled. Caught, the knight is lifted overhead and slammed down (a knockdown).
- **Phase 2 (half health, "the ink spills"):** it sinks into the floor as a pool. The pool hunts the player, then stops: the moment to roll clear. It bursts up out of it straight into a rising cut. From then on it moves faster, and every 6 seconds or so it uses one of its ink or mirror powers, mostly the mirrors (each mirror move is three times as likely as the dive, and none comes twice running): the dive; **break the mirror** (a mirror rises in front of the player and the boss's reflection slowly forms in it over 4.5 seconds: punch the glass 6 times first, a heavy counting twice, and it shatters and the boss drops to one knee for 3 seconds; too slow and the reflection's blow erupts across the floor 11 m round the mirror); **which one is real** (the boss sinks into the ink and appears in three mirrors round the player, each drawn back to lunge; the two fakes have cracked glass. Punch the real one and it falls out onto one knee for 3 seconds; punch a fake and it bursts in the player's face; wait 3.5 seconds and the real one lunges out); or the **mirror ambush** (two mirrors rise to the player's left and right, an ink copy dashes out of each in turn, then the boss leaps high and drives its sword into the floor where the player stands, inside a ring that marks it).
- **Numbers:** 1400 health (2100 in Hell); rising cut 28, horizontal cut 30, combo 22 + 22 + 36, slam 40, thrown sword 26, lunge 30, turn sweep 26, grab 45, leap 42, break-the-mirror blow 50, a fake mirror 30, ambush dash 24, dive 36, before Hell's ×1.3. In reach: rising cut 35% of the time, combo 20%, horizontal cut 15%, grab 15%, slam 15%. The combo's hits are just over half a second apart, longer than the knight's 0.5 seconds of safety after being hit, so each one can land. All on the prefab (`Assets/Prefabs/Enemies/Ink-Shadow.prefab`).
- **Model:** low-poly (about 700 triangles), built and rigged in Blender to match the knight's blocky look: `Docs/Art/InkShadow.blend`, exported to `Assets/Models/InkShadow/`. Its colours are a 4×4 palette texture, so it reads on the unlit enemy shader. Humanoid rig. Its idle, walk, death (it kneels on its planted sword), sword throw, combo, both cuts, strafes, lunge, turn sweep, grab, leap and summon are its own, keyed in the same file (actions `Shadow_*`, exported as `InkShadow@*.fbx`); the slam and the hop back reuse the Elite's clips.

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
| The final boss is the knight's own reflection | ✅ `InkMirror` | Revenge makes you what you hunt | The story's villain is the one telling it |

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

1. The final boss falls. This time, grey clay spreads out from where it fell, across the arena, until everything is clay.
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
| Final boss: mirror cutscene, phase 2, defeat | `InkShadowIntro.Timeline`, `InkShadow.StartPhaseTwo`, `InkShadow.Die` |
| Chest, shrine, first grab in Hell | `GauntletChoiceUI.OpenChoiceMenu`, `ShrineInteractable.CompleteUse`, `TrapEnemy.TryApplyTrap` |
| Death lines | `DeathScreenShatter` (the killer picks a boss line) |
| Loading screen line | `LoadingScreen.LoadLevel` |
| Ending, and the "finished once" flag | `LevelTransition` at Hell's exit → `StoryDialogue.TellEnding` |

**Adding a line:** add a key to the Story table, then call `StoryDialogue.Say("Key", Speaker.Who)` (or `SayOnce`) where the moment happens.

**Adding or changing a picture:** edit `Docs/Art/prologue.aseprite` (192×108, one frame per page) and export the frame as a PNG named after the line's key into `Assets/Resources/Story/`. Import settings: Point filter, no compression, no mipmaps, Non-Power-of-2 set to None. Pictures work for the ending pages too.

**Not built yet:** the true ending, and everything in Layer 2. The launch ending still plays at Hell's exit rather than the moment the final boss falls.

---

## 11. Decisions

| # | Question | Decision |
|---|---|---|
| 1 | The premise | Revenge for the knight's family, killed when Hell broke through (replaces "save the princess") |
| 2 | The knight's weapon | A sword, shattered against the Demon King in the prologue |
| 3 | Knight's name | Unnamed, always "the knight" |
| 4 | The family | Kept vague: never named, shown or counted. Heard only as whispers |
| 5 | Final boss | The Narrator's shadow, made of ink: the knight's reflection steps out of a mirror (replaces Brutus and Pergius in Hell) |
| 6 | Death lines | Every death, from a shuffled deck |
| 7 | Narrator voice | Voice blips for now |
| 8 | Prologue art | Pixel-art pages drawn in Aseprite |
| 9 | Layer 2 timing | A free update after launch |
| 10 | Who the Narrator is | B, a grieving storyteller who lost their own family |
