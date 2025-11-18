# Changes to be Made to Inform 6 Library for Guncho Multiplayer Support

Based on the Inform 7 extension `Guncho Realms.i7x`, here are the key changes needed for multiplayer Interactive Fiction support, described at an appropriate level of abstraction for an Inform 6 programmer:

## Part 1 - Player Characters

### 1.1 PC Pool Management
- **Create a PC kind/class** (`i7_pc_kind`) distinct from the standard player object
- **PC-corral container** (`i7_pc_corral`): Off-stage storage for 25 pre-created PC objects (disconnected players)
- **Properties needed**:
  - `i7_mud_id` (number): Unique player ID (positive for real players, negative for guests)
  - `mud-name` (indexed text/string): Player's name in the MUD
  - `pronouns` (4-element array): Stores pronoun state (him/her/it/them) for context switching
  - `reserved` (attribute): Marks a PC slot as temporarily reserved during connection
- **Consciousness transfer**: When player moves between realms, transfer `mud_id` and `mud_name` from old body to new body

### 1.2 Pronoun Management
- **Per-player pronoun storage**: Each PC has its own pronoun array
- **Save/Load/Reset functions** in I6:
  - `SavePronouns(victim)`: Store current global pronouns into victim's array
  - `LoadPronouns(victim)`: Load pronouns from victim's array into globals
  - `ResetPronouns(victim)`: Clear victim's pronoun storage
- Call these when switching the `player` global variable

### 1.3 Command Prompt & Input Handling
- **Hide command prompt**: Set to empty string (server sends prompts via tags)
- **Directed input parsing**: Commands prefixed with `<playerID>:` switch the active player, then execute command in their context
  - Parse pattern: `^(-?\d+):(.*)` 
  - Output tag `<$t N>` to target that player with responses
- **Special commands**: Commands starting with `$` are intercepted and handled separately (not sent to parser)

## Part 2 - Output Routing (Tag Protocol)

### 2.1 Core Tagging Functions
Implement these output wrappers that inject XML-like tags:
- **`<$t N>...</$t>`**: Target output to player with ID N only
- **`<$a>...</$a>`**: Announcement mode (broadcast to all players)
- **`<$d NAME>`**: Disambiguate mode - signal player name context for disambiguation prompts
- **`<$b REALMNAME>`**: Transfer player to different realm

### 2.2 Multiplayer-Aware Action Reporting
- **Modify action processing** to iterate through all connected PCs and generate separate output for each:
  1. Before action: Check which PCs can observe the action
  2. After action: For each observing PC, wrap their report output in `<$t ID>...</$t>` tags
- **Player awareness**: A PC can observe if they `can_see` the action location
- **Replace standard reporting** with multiplayer version that calls report rules once per observing PC

## Part 3 - Player Joining/Leaving

### 3.1 Join Protocol (`$join NAME=ID[,ENTRANCE]`)
- Parse join command format: name, ID, optional entrance path
- Find available PC in corral (prefer reserved if any)
- Set properties: `mud_id`, `mud_name`, proper-named attribute
- Reset pronouns for new player
- Move along entrance path (see section 5.2)
- Set gender based on player attribute "gender" (m/f/other)
- Announce arrival: "NAME appears from DIRECTION" or "fades into view"
- Execute player-specific look command

### 3.2 Leave Protocol (`$part ID`)
- Drop all possessions in current location with announcements
- Announce departure: "NAME disappears DIRECTION" or "fades away"
- Get exit direction from player attribute "waygone"
- Clear `mud_id` and `mud_name`
- Move PC back to corral

## Part 4 - Modified Standard Actions

### 4.1 Unblock Sensory Actions
Remove "block" rules for these actions (allow them to succeed):
- thinking, waving hands, smelling, listening, tasting, jumping, rubbing, singing
- Add simple report rules for each

### 4.2 Block Meta-Commands
Block these with custom messages (multiplayer games don't support them):
- quit, save, restore, restart
- transcript on/off
- superbrief/verbose/brief modes
- notify on/off
- undo (treat as mistake)

### 4.3 Modified Action Reports
- **Waiting**: Change report to use actor, not "You"
- **Looking**: Don't report when other PCs look (only NPCs)
- **Examining**: Don't report when other PCs examine (only NPCs)

## Part 5 - Inter-Player Interactions

### 5.1 Chat System
- **`$say` command**: `"PLAYER says (to TARGET), 'MESSAGE'"` - targeted or broadcast speech
- **`$emote` command**: `"PLAYER (to TARGET) ACTION"` - third-person narration
- Parse target from server register "chattarget"
- Parse message from server register "chatmsg"
- Block normal ASK/TELL/ANSWER actions when directed at PCs (redirect to chat)

### 5.2 Item Exchange
- **Offering**: Transform `GIVE X TO Y` into `OFFER X TO Y` when Y is a PC
- Create "generosity" relation tracking offered items
- **Accepting**: PC can `ACCEPT X` to take offered item
- Cancel expired offers each turn (if recipient can't see item anymore)

### 5.3 Showing Items
- Unblock `SHOW X TO Y` action
- When showing to a PC: trigger automatic examine for recipient
- Reports visible to both giver and recipient

## Part 6 - Multi-Realm Support

### 6.1 Entrance System
- **Table of Entrances**: Maps entrance tokens (strings) to room objects
- **Entrance paths**: Compact serialization format for locations
  - `=TOKEN`: Start at entrance
  - `,DIRECTION`: Move in direction (use short form: single char/word from dict)
  - `,%CONTAINER`: Enter container/supporter
  - `,!`: Error marker
  - `~`: Off-stage marker
- **Navigation functions**:
  - `entrance path to OBJ`: Generate path string from nearest entrance to object
  - `move OBJ along entrance path PATH`: Execute serialized path

### 6.2 Inter-Realm Commands
- **`$locate ID`**: Return entrance path to player ID
- **`$knock TOKEN`**: Check if entrance is valid and PC slot available
  - Response: "ok" (reserved slot), "full" (no slots), "invalid" (bad token)
- **Sending away**: Set player attribute "waygone" to exit direction, output `<$b REALM>` tag

## Part 7 - Server Communication (Registers)

### 7.1 Register Types
Two categories of data exchange with server:
- **Numeric registers**: Single integer values
- **Text registers**: Arbitrary-length strings

### 7.2 Implementation via FyreVM Channel
- Use FyreVM's conversation channel (`FYC_CONVERSATION`)
- **Protocol** (Glulx):
  - Write command: `getword NAME` / `putword NAME VALUE` / `gettext NAME MAXLEN` / `puttext NAME TEXT`
  - Read response via `FY_READLINE` into buffer
  - Parse numeric results or text chunks
- **Protocol** (Z-machine):
  - Use `@save` / `@restore` opcodes with special buffers
  - Magic prefixes: `txtn` (name), `txtl` (length), `txtd` (data)

### 7.3 Specific Register APIs
- **Player attributes**: Get/set per-player persistent data (e.g., "gender", "waygone", "description")
  - Uses registers: `pq_id`, `pq_attr`, `pq_attrval`
- **Player storage slots**: Realm-local per-player data
  - Uses registers: `ls_playerid`, `ls_attr`, `ls_playerval`
- **Realm storage slots**: Global realm data
  - Uses registers: `ls_attr`, `ls_realmval`
- **Real-time events**: Set `rteinterval` register to N (seconds), receive `$rtevent` commands

## Part 8 - Parser Modifications

### 8.1 Disambiguation Changes
- **Add `DisambigMode()` function**: Output `<$d` tag followed by original command text
- **Check for directed input**: Before processing disambiguation response, check if it's another player's command (starts with digits+colon) or system command (starts with `$`)
- **Reconstruct buffer**: If new command detected, copy to main buffer and trigger `RECONSTRUCT_INPUT`

### 8.2 "Again" Command Handling
- Block "again" command with custom message ("may only be used on a line by itself")
- Don't save command history for "again" functionality

### 8.3 Scope Modifications
- **`GetMatchList()` function**: Parse text string into object list
- **Buffer switching**: Implement `SwitchBufferIn/Out` to temporarily parse alternative text without losing main command
- Uses secondary parse buffer (`buffer2`, `parse2`)

## Part 9 - WorldModel Changes

### 9.1 ChangePlayer Modification
- **Before switching**: If old player is PC kind, save pronouns
- **After switching**: If new player is PC kind, load pronouns
- **Former self handling**: Don't move previous PC to "PC-corral" on switch (keep in world)
- Remove `proper` attribute manipulation for "former self" (PCs stay proper-named)

### 9.2 Printing Modifications (Articles)
- In `IndefArt`, `CIndefArt`, `DefArt`, `CDefArt`: Check for proper-named PCs
- Handle proper-named objects specially (no article, just name)
- Use `indef_mode` flag to track article context

## Part 10 - Action Processing

### 10.1 Persuasion Blocking
- Modify "Requested Actions Require Persuasion" rule
- Block persuasion attempts on PC objects: "You can't order other players."
- Allow normal persuasion for NPCs

### 10.2 Competitive Scoring
- Add optional `COMPETITIVE_SCORING` constant
- Modify obituary headline to include actor name: "PLAYER has won/lost" (vs. "You have won/lost")
- Skip "final question" rule (no replay/restore options)

## Part 11 - Additional Utilities

### 11.1 Object Homing
- Create "object-home" relation tracking original location
- `send X home`: Teleport object back to original location with announcements
- Initialize on game start

### 11.2 List Utilities
- `list of objects called TEXT near POV`: Parse text in POV's scope, return matching objects
- Used for resolving names in entrance paths and chat targets

### 11.3 Real-Time Events
- `$rtevent` command triggers "real-time event" rulebook
- Authors can add rules for periodic processing

### 11.4 Shutdown Notification
- `$shutdown` command triggers "realm shutdown" rulebook
- Allows cleanup before realm process terminates

---

## Mapping Notes for Inform 6

Most changes map directly to Inform 6 with these considerations:

1. **Properties**: Define as Inform 6 properties with appropriate types
2. **Indexed text**: Use Inform 6 strings (with buffer management for dynamic text in I7)
3. **Relations**: Use arrays or properties for 1-to-1, lists for many-to-many
4. **Rulebooks**: Implement as functions called at appropriate points
5. **Tag injection**: Insert print statements at action processing boundaries
6. **Parser hooks**: Replace sections in `Parser.h` (or equivalent)
7. **Library Messages**: Override in `LibraryMessages` object

Some I7-specific features (dynamic text, complex type checking) may need simplification or alternative implementation strategies in I6.
