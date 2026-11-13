# Rebuild the Booth Page: Unity drop-in

## Setup (about 2 minutes)
1. Unity: **Window → TextMeshPro → Import TMP Essential Resources** (once per project).
2. Drag this whole `HarnessMinigame` folder into `Assets/` so the path is `Assets/HarnessMinigame/`.
3. Menu **Tools → Harness → 1. Fix Import Settings**. This sets every PNG to Sprite, Point filter and no compression.
4. Open your room/map scene, then menu **Tools → Harness → 2. Build Minigame UI**.
   This creates a `HarnessCanvas` with all 8 screens, every button and card in place, and all OnClick events already wired up.
5. Select `HarnessMinigame` and set:
   - **Website Url**: your friend's site
   - **Quest Trigger**: a QuestCompleteTrigger linked to this quest in your QuestManager
6. On your NPC or terminal, add **HarnessOpener**, drag `HarnessMinigame` into it, and give the object a **Collider2D**.
   Tapping it opens the mini-game. The mini-game starts hidden.
7. Drop your NPC portrait sprite into the 2 `NPC_Portrait` placeholders (Story and Builder screens).

## Folders
| Folder | What |
|---|---|
| Scenes/ | backgrounds S1–S8 (no buttons, text baked in). S2/S3/S4 are the same builder screen. |
| Scenes_NoText/ | the same backgrounds without text, if you want to use your own TMP text |
| Buttons/ | every button, normal + pressed |
| Swatches/ | colour swatches + selected ring |
| HUD/ | meter fill |
| Resources/Harness/ | cards + phone preview pages. **Don't rename or move**: the script loads them by name |
| Scripts/ | HarnessMinigame, HarnessOpener, QuestManager, QuestCompleteTrigger, CloudNetworkMeter |
| Editor/ | HarnessUIBuilder (the one-click builder; editor-only, not included in builds) |
| Editable/ | buttons and cards with no text + label_tab, for customising (see below) |
| ForFriendsWebsite~/ | JS snippet so the friend's site fills the prompt box from the link (Unity ignores this folder) |

## Customising text
- To change card or button wording, copy `Editable/Cards_NoText/*` over `Resources/Harness/Cards/` (same file names), and `Editable/Buttons_NoText/*` over `Buttons/`.
  Then add a TextMeshPro child to each card or button with your text.
- To use your own titles, point the panel background Images at `Scenes_NoText/`.

## Already-existing QuestManager?
If your project already has QuestManager / QuestCompleteTrigger / CloudNetworkMeter, delete the copies in `Scripts/` to avoid duplicates.

## Fonts
The builder uses TMP's default font. For the pixel look, import **Pixelify Sans** or **Press Start 2P** (Google Fonts, free) with TMP's Font Asset Creator, then swap the font on the texts.
