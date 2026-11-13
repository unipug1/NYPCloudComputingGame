# Firebase setup (sign-ups + progress)

The game saves every sign-up to **Cloud Firestore** in a `players` collection:

| Field | Example |
|---|---|
| name | "Tan Ah Kow" |
| email | "ahkow@gmail.com" |
| createdAt | 13 Nov 2026 3:50pm |
| done | ["Wire Game", "LLM Game"] (completed quests) |
| updatedAt | last progress save |

The document ID is a SHA-256 hash of the email, so each email can only sign up once.
It uses Firestore's REST API, which works in **WebGL** (the official Firebase Unity SDK does not support WebGL).

## 1. Create the project (about 5 minutes)
1. Go to https://console.firebase.google.com and sign in with the Google account the team will own it with.
2. **Add project** → name it e.g. `nyp-openhouse-game` → Google Analytics can be **off** → Create.
3. Left menu → **Build → Firestore Database → Create database**.
   - Location: **asia-southeast1 (Singapore)**
   - Start in **production mode**
4. In Firestore, open the **Rules** tab, delete everything, paste in the contents of `firestore.rules` (next to this file), then **Publish**.
5. Click the ⚙ (top left) → **Project settings → General**:
   - Copy the **Project ID**
   - Under *Your apps*, click the **</>** (Web) icon → register the app (any nickname, no hosting) → copy the **apiKey** from the config it shows.

## 2. Put the keys in Unity
Open the `Main_Map` scene → select **GameSystems** → **Firebase Service** component:
- **Project Id** = your Project ID
- **Web Api Key** = the apiKey

Then click **Overrides → Apply All** at the top of the Inspector, so the GameSystems prefab gets the keys too.
That's it. If these two fields are empty, the game still works, but saves accounts on the device only.

## 3. Test
Play → SIGN UP → enter a name + email → Create account.
In the Firebase console → Firestore → `players`, a new document appears.

## Notes
- **The Web API key is safe to ship in the game.** It only identifies the project. What protects the data is the rules file: the game can create a player, read **one** player by email hash (for login), and update only their quest list. It **cannot** list, download or delete the player list.
- To see or export everyone who signed up (e.g. to send e-certificates), use the Firebase console. In Firestore you can view the documents, and **Import/Export** (paid Blaze plan) or a quick admin script can export them.
- **PDPA:** many visitors are under 18. Get your lecturer or NYP to approve the wording on the sign-up screen ("Only used to send your e-certificate"), and delete the data after the certificates are sent.
- **Free plan limits** (Spark): 50k reads and 20k writes per day, which is plenty for an Open House.
