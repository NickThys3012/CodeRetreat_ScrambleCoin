# 🪙 Scramblecoin – Game Overview

> Scramblecoin is a turn-based board game from *Disney Dreamlight Valley* (A Rift in Time expansion), played against Villagers to earn friendship, ranking points, and collectible figurines.

---

## 🗺️ The Board

- **8×8 grid** with randomly generated obstacles each game:
  - **Lakes** – 2×2 tile obstacles, impassable
  - **Rocks** – 1-tile obstacles, impassable (can be destroyed by Ralph)
  - **Fences** – placed on tile *edges*; block orthogonal movement across that edge; block diagonal movement if two fences connect at a corner

---

## 🎮 Game Structure

### Setup
1. Player selects a **line-up of 5 pieces** before the game starts.
2. Cancelling at this stage does **not** count as a forfeit.
3. The **player always goes first**.

### Board Limit
- Each player can have a **maximum of 3 pieces on the board** at any time.
- On your turn you may place/replace up to 1 piece, bringing you to or keeping you at the 3-piece cap.

### Turn Sequence (5 turns total)
Each turn, in order:

1. **Coin Spawn** – At the start of each turn (before placement), the board randomly generates star coins:
   - **Turn 1 (initial):** **7–9 silver coins** are randomly placed on the board at the start of the game
   - **Turns 1–3:** Only additional silver coins (1 pt each) spawn each turn
   - **Turn 4:** 2 gold coins (3 pts each) spawn for both player and opponent
   - **Turn 5:** Player gets 2 gold coins; opponent gets only **1 gold coin** (balance mechanic for going second)
2. **Place/Replace a Piece** *(optional, skippable)* – Place a new piece on the board, or replace an existing one
3. **Move All Pieces** – Move every piece on the board (pieces waiting to move are marked with "Zzz")

### End Condition
- Game ends after the **opponent's 5th turn**
- Player with the **highest coin score wins**
- Tied score = **Draw**

---

## 🏆 Scoring & Rewards

### Coins
| Coin Type | Points |
|-----------|--------|
| Silver    | 1 pt   |
| Gold      | 3 pts  |

Pieces collect coins as they move through tiles (except Jump-movement pieces, which only collect at their destination).

### Friendship Rewards
| Condition            | Friendship |
|----------------------|------------|
| Completed game       | 200        |
| Eager Villager game  | 1,000      |

---

## 📈 Ranking System

Players earn ranking points on the **Boardgame Ranking** track (0–550).

### Points Per Outcome
| Outcome | Normal Game | Eager/Bonus Game |
|---------|-------------|-----------------|
| Victory | +3          | +6              |
| Draw    | +2          | +4              |
| Defeat  | +1          | +2              |

- **Forfeit** (cancelling mid-game after piece selection): counts as a **loss** — 0 ranking points, and the game is marked as played for the day
- Ranking points are only awarded for the **first game per day** against each Villager; rematches that same day award no points regardless of outcome
- At **550 ranking points**, no further points are earned
- New **Figurines** are unlocked at milestone ranks (randomly ordered): `3, 9, 15, 24, 33, 45, 57, 69, 84, 99, 114, 129, 147, 165, 183, 201, 219, 237, 258, 279, 300, 350, 400, 450, 500, 550`

Each unlocked Figurine also unlocks **2 Furniture items** in your Collection.

---

## 😤 Eager Villagers

- Some Villagers become **Eager** and want a bonus game — shown by a 🎲 icon on their map marker and above their head
- A **"BONUS"** label appears in conversation
- Eager Villagers may chase the player around the Valley
- After playing, the Villager returns to their normal schedule

---

## 🧩 Piece Mechanics

### Entry Points
| Icon | Meaning |
|------|---------|
| Borders | Piece enters from the board's edges |
| Corners | Piece enters from one of the 4 corners |
| Anywhere | Piece can be placed on any free tile |

### Movement Types
| Type | Description |
|------|-------------|
| Orthogonal | Horizontal/vertical only |
| Diagonal | Diagonal only |
| Any direction | Full freedom of direction |
| Jump | Teleports directly to destination, ignores obstacles; does NOT collect coins along the way |
| Charge | Moves in one direction until hitting an obstacle, piece, or board edge |
| Ethereal | Can pass through obstacles/pieces but must *end* on a free tile |

### Key Movement Rules
- Pieces move up to their **maximum tile count** (player chooses how many, up to the max)
- Pieces **cannot move through** other pieces or obstacles (unless Ethereal)
- Pieces **collect all coins** along their path (unless using Jump)
- Some pieces move **multiple times** per turn; all movements must be used
- **Ice patches** (left by Elsa): any piece crossing one slides to the next non-ice tile; disrupts multi-move and Charge pieces; Jump is unaffected

---

## 🎭 Figurines

> Unlock rank shown in parentheses. Starter pieces are given by Mickey during *A Game of Coins*.

| Figurine            | Unlock         | Play Cond. | Entry    | Movement                  | Special Ability |
|---------------------|----------------|------------|----------|---------------------------|-----------------|
| **Mickey Mouse**    | Starter        | —          | Borders  | Orthogonal up to 3        | None |
| **Minnie Mouse**    | Starter        | —          | Borders  | Diagonal up to 3          | None |
| **Donald Duck**     | Starter        | —          | Corners  | Any direction up to 3     | None |
| **Goofy**           | Starter        | —          | Corners  | Any 3 (Jump)              | Jump |
| **Scrooge McDuck**  | Starter        | —          | Corners  | Any 2                     | +1 bonus coin at end of each turn |
| **Flynn**           | Rank 3         | —          | Anywhere | Any 1                     | Leaves a silver coin on his previous tile |
| **Nala**            | Rank 9         | —          | Borders  | Diagonal 2×2 (Jump)       | Double jump diagonally |
| **Simba**           | Rank 15        | —          | Borders  | Orthogonal 2×2 (Jump)     | Double jump orthogonally |
| **Cogsworth**       | Rank 24        | —          | Borders  | Any 1, then Orthogonal 2  | None |
| **Lumiere**         | Rank 33        | —          | Borders  | Any 1, then Diagonal 2    | None |
| **Remy**            | Rank 45        | Turn 2     | Borders  | Diagonal 2 × 2            | None |
| **Fairy Godmother** | Rank 57        | —          | Anywhere | Any 2 (Ethereal)          | Gives +1 move to all adjacent ally pieces |
| **Ralph**           | Rank 69        | —          | Borders  | Orthogonal 3              | Destroys adjacent fences & rocks on stop |
| **Pumbaa**          | Rank 84        | —          | Borders  | Orthogonal Charge         | Charge; destroys surrounding fences on stop |
| **Stitch**          | Rank 99        | —          | Borders  | Orthogonal 3              | Can pass through and destroy fences |
| **Ursula**          | Rank 114       | —          | Anywhere | Any 2 (Ethereal)          | Gives −1 move to all adjacent opponent pieces |
| **EVE**             | Rank 129       | Turn 2     | Corners  | Any 8 (Jump)              | Massive jump range |
| **WALL•E**          | Rank 147       | —          | Borders  | Orthogonal Charge         | Charge; pushes adjacent pieces 1 tile away on stop |
| **Elsa**            | Rank 219 *(T2)*| Turn 2     | Borders  | Orthogonal 4              | Leaves ice patches; pieces crossing them slide to next tile |
| **Olaf**            | Rank 183       | —          | Anywhere | Any 1 × 2                 | None |
| **Daisy**           | Rank 201       | Turn 2     | Anywhere | Any 3 (Jump)              | Can land on any piece; swaps positions + steals 1 opponent coin |
| **Anna**            | Rank 258       | —          | Borders  | Orthogonal 1 × 3          | Must use all 3 moves |
| **Sulley**          | Rank 237       | —          | Borders  | Any 2                     | Pushes opponent pieces 2 tiles away on stop |
| **Cinderella**      | Rank 279       | —          | Corners  | Any 2, then Any 1         | Removes herself from the board at start of turn 5 |
| **Mike Wazowski**   | Rank 300       | Turn 3     | Corners  | Any 2                     | Gives a random adjacent ally +1 coin collect buff |
| **Moana**           | Rank 400       | —          | Anywhere | Any 1 (grows)             | Gains +1 movement each turn she stays on the board |
| **Scar**            | Rank 450       | Turn 3     | Corners  | Any 4 (Jump)              | Can land on an opponent's piece to remove it from the board |
| **Oswald**          | Rank 500       | —          | Borders  | Orth 1 → Diag 1 → repeat  | Stops early if a move collects no coin |
| **Rapunzel**        | Rank 550       | Turn 3     | Anywhere | Any 1                     | Collects up to 3 coins from adjacent tiles after moving |
| **Kristoff**        | Timebending L2 | —          | Borders  | Diagonal 1 × 3            | None |
| **Rafiki**          | Timebending L1 | —          | Corners  | Any 4 (Jump)              | Pushes all adjacent pieces 1 tile away on stop |
| **Merlin**          | Timebending L2 | Turn 4     | Anywhere | Any 2 (Ethereal)          | Converts 1 nearby silver coin to gold at start of his turn |
| **Forky**           | Timebending L1 | —          | Anywhere | Any 2                     | Removes himself from the board at end of his first turn |
| **Jafar**           | Ranking        | —          | Borders  | Multi-step (grows)        | Gains additional moves each turn on the board |

---

## 🥇 Starting Pieces

Your first game against Mickey requires these 5 starter figurines (provided by Mickey via the quest *A Game of Coins*):

- Mickey Mouse
- Minnie Mouse
- Goofy
- Donald Duck
- Scrooge McDuck

---

## 🤺 Opponents

| Villager           | Difficulty | Piece 1       | Piece 2       | Piece 3       | Piece 4       | Piece 5       |
|--------------------|------------|---------------|---------------|---------------|---------------|---------------|
| **Mickey Mouse**   | Level 1    | Donald Duck   | Minnie Mouse  | Goofy         | Scrooge       | Mickey Mouse  |
| **Goofy**          | Level 1    | Goofy         | Goofy         | Goofy         | Goofy         | Goofy         |
| **Scrooge McDuck** | Level 1    | Flynn         | Scrooge       | Goofy         | Merlin        | Minnie Mouse  |
| **Kristoff**       | Level 1    | Fairy Godmother | Ursula      | EVE           | Stitch        | Forky         |
| **Moana**          | Level 1    | Merlin        | Nala          | Ursula        | Olaf          | WALL•E        |
| **Sulley**         | Level 1    | —             | —             | —             | —             | —             |
| **Nala**           | Level 1    | Olaf          | Ursula        | Remy          | Stitch        | Fairy Godmother |
| **Remy**           | Level 1    | Flynn         | Nala          | Stitch        | Donald Duck   | Gaston        |
| **WALL•E**         | Level 1    | EVE           | Stitch        | Forky         | Lumiere       | Remy          |
| **Elsa**           | Level 2    | Mickey Mouse  | Gaston        | WALL•E        | Merlin        | Scrooge       |
| **Minnie Mouse**   | Level 2    | Flynn         | Simba         | Cogsworth     | Lumiere       | Nala          |
| **Donald Duck**    | Level 2    | EVE           | Fairy Godmother | Donald Duck | Nala          | Moana         |
| **Maui**           | Level 2    | Anna          | EVE           | Donald Duck   | Fairy Godmother | Goofy       |
| **Mike Wazowski**  | Level 2    | —             | —             | —             | —             | —             |
| **Olaf**           | Level 2    | Gaston        | Goofy         | Cinderella    | Rafiki        | Remy          |
| **Stitch**         | Level 2    | —             | —             | —             | —             | —             |
| **Oswald**         | Level 2    | —             | —             | —             | —             | —             |
| **Simba**          | Level 2    | Anna          | Olaf          | WALL•E        | Goofy         | Lumiere       |
| **Ariel**          | Level 2    | EVE           | Minnie Mouse  | Rafiki        | Anna          | Simba         |
| **Prince Eric**    | Level 2    | Merlin        | Rapunzel      | Elsa          | Minnie Mouse  | Anna          |
| **Ursula**         | Level 2    | Mickey Mouse  | Donald Duck   | Anna          | Goofy         | Cinderella    |
| **Rapunzel**       | Level 2    | —             | —             | —             | —             | —             |
| **Buzz Lightyear** | Level 2    | Stitch        | Lumiere       | Flynn         | Minnie Mouse  | Merlin        |
| **Woody**          | Level 2    | Mickey Mouse  | Lumiere       | Remy          | WALL•E        | Rafiki        |
| **EVE**            | Level 2    | Flynn         | Lumiere       | Remy          | WALL•E        | Rafiki        |
| **The Beast**      | Level 2    | —             | —             | —             | —             | —             |
| **Gaston**         | Level 2    | —             | —             | —             | —             | —             |
| **Fairy Godmother**| Level 2    | Scrooge       | Donald Duck   | Stitch        | Cinderella    | Forky         |
| **The Fairy Godmother** | Level 2 | Scrooge    | Donald Duck   | Stitch        | Cinderella    | Forky         |
| **Mulan**          | Level ?    | —             | —             | —             | —             | —             |
| **Mushu**          | Level ?    | —             | —             | —             | —             | —             |
| **Anna**           | Level 3    | Simba         | Rapunzel      | Flynn         | Nala          | Gaston        |
| **Mirabel**        | Level 3    | Remy          | Nala          | Rapunzel      | Fairy Godmother | Ralph       |
| **Daisy**          | Level 3    | Mike Wazowski | Nala          | Elsa          | Minnie Mouse  | Oswald        |
| **Jasmine**        | Level 3    | Anna          | Daisy         | Moana         | Ralph         | *(+1)*        |
| **Aladdin**        | Level 3    | Kristoff      | Olaf          | Oswald        | Ralph         | Ursula        |
| **Jafar**          | Level 3    | EVE           | Forky         | Gaston        | Merlin        | Rapunzel      |
| **Scar**           | Level 3    | Minnie Mouse  | Anna          | Donald Duck   | Lumiere       | Fairy Godmother |
| **Mother Gothel**  | Level 3    | Ursula        | Kristoff      | Minnie Mouse  | Remy          | Forky         |
| **Merlin**         | Level 3    | Lumiere       | Simba         | Flynn         | Merlin        | Stitch        |
| **Belle**          | Level 3    | —             | —             | —             | —             | —             |
| **Jack Skellington**| Level 3   | —             | —             | —             | —             | —             |
| **Vanellope**      | Level 3    | Merlin        | WALL•E        | Rapunzel      | Lumiere       | Forky         |

> ℹ️ Villager lineups are fixed but displayed in random order during the game. Some lineups are not yet documented (shown as —).

---

## 💡 Tips

- **Going second is a big advantage** — the opponent gets fewer gold coins on turn 5 to partially compensate
- Keep pieces on the board long to maximize **Moana** and **Jafar** power
- Use **Elsa's ice patches** to disrupt your opponent's pieces
- **Scrooge** passively earns coins every turn just for being on the board
- **Scar** is the only Jump piece that can remove opponent figurines
