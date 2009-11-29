Version 2/080522 of Guncho Realms by Guncho Cabal begins here.

"This extension implements the I7-side changes needed for multiplayer realms."

Use authorial modesty.
Use full-length room descriptions.
Use dynamic memory allocation of at least 16384.
Use MAX_STATIC_DATA of 500000.

Part 1 - Player characters

[We don't want "your former self" hanging around.]

When play begins: move yourself to the PC-corral.

Chapter 1 - The PC kind

A PC is a kind of person. A PC is usually proper-named. The PC kind translates into I6 as "i7_pc_kind".

A PC can be reserved or unreserved. A PC is usually unreserved.

A PC has indexed text called the mud-name. Rule for printing the name of a PC (called whoever) (this is the PC name printing rule): say mud-name of whoever. Understand the mud-name property as describing a PC.

The description of a PC is "[possibly customized description of the item described]".

After examining a PC (this is the list PC possessions after examining rule):
    if the noun is wearing something, say "[The noun] is wearing [a list of things worn by the noun.";
    if the noun is carrying something, say "[The noun] is carrying [a list of things carried by the noun]."

To say possibly customized description of (victim - PC):
	let custom desc be player attribute "description" of the victim;
	if custom desc is "" begin;
        issue library message examining action number 2 for the victim;
	otherwise;
		replace the text "\$" in custom desc with "&dollar;";
		say custom desc;
	end if.

A PC has a number called the mud-id. The mud-id property translates into I6 as "i7_mud_id". The mud-id of a PC is usually 0.

Mud-identification relates a number (called ID) to a PC (called whomever) when ID is the mud-id of whomever. The verb to identify (it identifies, they identify, it identified, it is identified) implies the mud-identification relation.

Mud-naming relates indexed text (called the name) to a PC (called whomever) when the name is the mud-name of whomever. The verb to name (it names, they name, it named, it is named) implies the mud-naming relation.

The PC-corral is a container. The PC-corral object translates into I6 as "i7_pc_corral". 25 PCs are in the PC-corral.

Definition: a PC is connected if it is not in the PC-corral.
Definition: a PC is disconnected if it is in the PC-corral.

To transfer consciousness from (source - a PC) to (target - PC):
	now the mud-name of target is the mud-name of source;
	now the mud-name of source is "";
	now the mud-id of target is the mud-id of source;
	now the mud-id of source is 0.

Section 1 - Multiplayer pronoun support

Include (- with pronouns 0 0 0 0, -) when defining a PC.

Include (-
[ ResetPronouns victim  p;
	p = victim.&pronouns;
	if (~~p) rfalse;
	p-->0 = 0; p-->1 = 0; p-->2 = 0; p-->3 = 0;
];

[ SavePronouns victim  p;
	p = victim.&pronouns;
	p-->0 = PronounValue('him');
	p-->1 = PronounValue('her');
	p-->2 = PronounValue('it');
	p-->3 = PronounValue('them');
];

[ LoadPronouns victim  p;
	p = victim.&pronouns;
	SetPronoun('him', p-->0);
	SetPronoun('her', p-->1);
	SetPronoun('it', p-->2);
	SetPronoun('them', p-->3);
];
-).

Chapter 2 - Special input handling

When play begins (this is the hide command prompt rule):
	change the command prompt to "".

After reading a command (this is the handle directed input rule):
	if the player's command matches the regular expression "^(-?\d+):(.*)" begin;
		let the target-id be the numeric value of the text matching subexpression 1;
		if the target-id identifies a PC (called the target) begin;
			change the player to the target;
			let L be the text matching subexpression 2;
			change the text of the player's command to L;
			say "<$t [target-id]>";
		otherwise;
			say "No such player.";
			reject the player's command;
			rule succeeds;
		end if;
	end if.

After reading a command (this is the handle special commands rule):
	if character number 1 in the player's command is "$" begin;
		follow the special command handling rules;
		reject the player's command;
	end if.

Special command handling is a rulebook.

The last special command handling rule (this is the unknown special command rule):
	say "No such special command.";
	rule fails.

Chapter 3 - Special output handling

[BUGFIX: 5J39 contains a bug where passing indexed text from one phrase to another can cause I6 compilation errors when the second phrase has parameters that are runtime type checked. The "target" parameters here should be typed PC, but they're objects instead to work around this bug.]

To tell (message - indexed text) to (target - an object):
	say "<$t [mud-id of the target]>[message]</$t>".

To tell (message - indexed text) to everyone else near (target - an object):
	repeat with X running through connected PCs who can see the target begin;
		if X is not the target, say "<$t [mud-id of X]>[message]</$t>";
	end repeat.

To tell (message - indexed text) to everyone who can see (spectacle - an object), except the actor:
	repeat with X running through connected PCs who can see the spectacle begin;
		let skipping be false;
		if except the actor, let skipping be whether or not X is the player;
		if skipping is false, say "<$t [mud-id of X]>[message]</$t>";
	end repeat.

To announce (message - indexed text):
	say "<$a>[message]</$a>".

Chapter 4 - Winning and losing

Use competitive scoring translates as (- Constant COMPETITIVE_SCORING; -).

Include (-
[ PRINT_OBITUARY_HEADLINE_R;
    print "<$a>^^    ";
    VM_Style(ALERT_VMSTY);
    print "***";
    #ifdef COMPETITIVE_SCORING;
    if (deadflag == 1) print " ", (the) player, " has lost ";
    if (deadflag == 2) print " ", (the) player, " has won ";
    #ifnot; ! cooperative scoring
    if (deadflag == 1) print " You have lost ";
    if (deadflag == 2) print " You have won ";
    #endif; ! scoring type
    if (deadflag ~= 0 or 1 or 2)  {
        print " ";
        if (deadflag ofclass Routine) (deadflag)();
		if (deadflag ofclass String) print (string) deadflag;
        print " ";
    }
    print "***";
    VM_Style(NORMAL_VMSTY);
    print "^^"; #Ifndef NO_SCORE; print "^"; #Endif;
    rfalse;
];
-) instead of "Print Obituary Headline Rule" in "OrderOfPlay.i6t".

The ask the final question rule is not listed in the shutdown rulebook.

Part 2 - Player commands

Chapter 1 - Game engine metacommands

Section 1 - Joining

A special command handling rule (this is the handle joining rule):
	if the player's command matches the regular expression "^\$join (.+)=(-?\d+)(?:,(.*))?$", case insensitively begin;
		let the new mud-name be the text matching subexpression 1;
		let the new mud-id be the numeric value of the text matching subexpression 2;
		let the new location be the text matching subexpression 3;
		add a player named the new mud-name with ID the new mud-id at location the new location;
		rule succeeds;
	end if.

[TODO: Rewrite this in I6 so it can treat the digits as characters instead of strings.]
To decide which number is numeric value of (T - indexed text):
	let S be 1;
	let L be the number of characters in T;
	if L is 0, decide on 0;
	let negated be false;
	if character number 1 in T is "-" begin;
		let negated be true;
		let S be 2;
	end if;
	let result be 0;
	repeat with N running from S to L begin;
		let C be character number N in T;
		let D be 0;
		if C is "1" begin; let D be 1; otherwise if C is "2"; let D be 2;
		otherwise if C is "3"; let D be 3; otherwise if C is "4"; let D be 4;
		otherwise if C is "5"; let D be 5; otherwise if C is "6"; let D be 6;
		otherwise if C is "7"; let D be 7; otherwise if C is "8"; let D be 8;
		otherwise if C is "9"; let D be 9; otherwise if C is "0"; let D be 0;
		otherwise; decide on 0; end if;
		let result be (result * 10) + D;
	end repeat;
	if negated is true, let result be 0 - result;
	decide on result.

To add a player named (new mud-name - indexed text) with ID (new mud-id - number) at location (new path - indexed text):
	if no PC is in the PC-corral begin;
		say "No available player slots.";
	otherwise if the new mud-id identifies a PC;
		say "ID #[new mud-id] is already in use.";
	otherwise if the new mud-name names a PC;
		say "Name '[new mud-name]' is already in use.";
	otherwise;
		if a PC (called the reserved body) is reserved, let newbie be the reserved body;
		otherwise let newbie be a random PC in the PC-corral;
		now the newbie is unreserved;
        now the newbie is proper-named;
		reset pronouns for the newbie;
		move the newbie along entrance path the new path;
		change the mud-id of the newbie to the new mud-id;
		change the mud-name of the newbie to the new mud-name;
		follow the player joining rules for the newbie;
	end if.

To reset pronouns for (victim - object): (- ResetPronouns({victim}); -).

Player joining is an object-based rulebook. The player joining rulebook has a PC called the current PC.

The first player joining rule for a PC (called the newbie):
	now the current PC is the newbie.

A first player joining rule (this is the initial PC location rule):
	if the current PC is in the PC-corral begin;
		let R be the first room;
		move the current PC to R;
	end if;
	change the player to the current PC;
	say "<$t [mud-id of the current PC]>";
	try looking.

A first player joining rule (this is the set PC gender rule):
	let gender be player attribute "gender" of the current PC in lower case;
	if gender is "m" begin;
		now the current PC is male;
		now the current PC is not neuter;
	otherwise if gender is "f";
		now the current PC is female;
		now the current PC is not neuter;
	otherwise;
		now the current PC is neuter;
	end if.

A player joining rule for a PC (called newbie) (this is the announce PC connection rule):
	let waygone be player attribute "waygone" of the current PC;
	let dir be the direction known as waygone;
	let msg be indexed text;
	if dir is a direction, let msg be "[The newbie] appears from [directional opposite of dir].";
	otherwise let msg be "[The newbie] fades into view.";
	tell msg to everyone else near the current PC.

To say directional opposite of (dir - a direction):
    if dir is up begin;
        say "below";
    otherwise if dir is down;
        say "above";
    otherwise;
        say the opposite of dir;
    end if.

To decide which room is the first room:
	repeat with R running through rooms begin;
		decide on R;
	end repeat;
	decide on nothing.

Section 2 - Leaving

A special command handling rule (this is the handle leaving rule):
	if the player's command matches "$part [number]" begin;
		remove the player with ID the number understood;
		rule succeeds;
	end if.

To remove the player with ID (parting mud-id - number):
	if the parting mud-id identifies a PC (called the goner), follow the player leaving rules for the goner;
	otherwise say "No such player."

Player leaving is an object-based rulebook. The player leaving rulebook has a PC called the current PC.

The first player leaving rule for a PC (called the goner):
	now the current PC is the goner.

A player leaving rule for a PC (called the goner) (this is the drop possessions when leaving rule):
	if the goner has something begin;
		let msg be indexed text;
		let msg be "You leave behind [the list of things had by the goner] as you exit the realm.";
		tell msg to the goner;
		let msg be "[The goner] drops [a list of things had by the goner].";
		tell msg to everyone else near the goner;
		now everything had by the goner is in the location of the goner;
	end if.

A player leaving rule for a PC (called the goner) (this is the announce PC disconnection rule):
	let waygone be player attribute "waygone" of the goner;
	let dir be the direction known as waygone;
	if dir is a direction, tell "[The goner] disappears [motion toward dir]." to everyone else near the goner;
	otherwise tell "[The goner] fades away into nothingness." to everyone else near the goner.

To say motion toward (dir - a direction):
    if dir is up begin;
        say "upward";
    otherwise if dir is down;
        say "downward";
    otherwise;
        say "to [the dir]";
    end if.

The last player leaving rule (this is the low-level PC cleanup rule):
	change the mud-id of the current PC to 0;
	change the mud-name of the current PC to "";
	move the current PC to the PC-corral.

Object-homing relates various things to one thing (called the object-home). The verb to be at home in implies the object-homing relation.

When play begins (this is the initial object homes rule):
	repeat with X running through things begin;
		now X is at home in the holder of X;
	end repeat.

To send (X - a thing) home:
	tell "[The X] disappears." to everyone who can see X;
	move X to the object-home of X;
	tell "[The X] appears." to everyone who can see X.

Section 3 - Polling for info

A special command handling rule (this is the handle info requests rule):
	if the player's command matches "$info" begin;
		say "Info text goes here!";
		rule succeeds;
	end if.

Chapter 2 - Chat commands

A special command handling rule (this is the handle chatting and emoting rule):
	if the player's command matches the regular expression "^\$(say|emote)\b", case insensitively begin;
		let cmd be the text matching subexpression 1 in lower case;
		change the text spoken to server text register "chatmsg";
        change the chat direction to server text register "chattarget";
        if the number of characters in the chat direction is greater than 0 begin;
            let targets be the list of objects called the chat direction near the player;
            let the bad'uns be a list of objects;
            repeat with X running through targets begin;
                if X is not a PC, add X to bad'uns;
            end repeat;
            remove bad'uns from targets;
            if the number of entries in targets is greater than 0, change the chat direction to "[the entry 1 in targets]";
        end if;
		if cmd is "say", try chatting;
		otherwise try emoting;
		rule succeeds;
	end if.

The text spoken is an indexed text that varies.
The chat direction is an indexed text that varies.

Chatting is an action applying to nothing.

Carry out chatting (this is the standard chatting rule):
    let N be the number of characters in the chat direction;
	tell "You say[if N is not 0] (to [chat direction])[end if], '[the text spoken]'" to the player;
	tell "[The player] says[if N is not 0] (to [chat direction])[end if], '[the text spoken]'" to everyone else near the player.

Emoting is an action applying to nothing.

Carry out emoting (this is the standard emoting rule):
	if the text spoken matches the regular expression "^<.,:;!?[apostrophe]>", let space be "";
	otherwise let space be " ";
	let the full emote be indexed text;
    let N be the number of characters in the chat direction;
	let the full emote be "[The player][if N is not 0] (to [chat direction])[end if][space][the text spoken]";
	tell the full emote to the player;
	tell the full emote to everyone else near the player.

Answering is speech action. Telling someone about something is speech action. Asking someone about something is speech action. 

Instead of speech action when the noun is a PC (this is the explain chatting rule), say "As you speak the same language, you might as well speak aloud. (Just a ' mark followed by what you'd like to say will suffice.)" 

Quietly idling is an action out of world, applying to nothing. Understand "qidle" as quietly idling.

Chapter 3 - Modified Inform actions

This is the new other people looking rule:
	if the actor is not the player and the actor is not a PC, say "[The actor] looks around."

The new other people looking rule is listed instead of the other people looking rule in the report looking rules.

This is the new other people examining rule:
	if the actor is not the player and the actor is not a PC, say "[The actor] looks closely at [the noun]."

The new other people examining rule is listed instead of the report other people examining rule in the report examining rulebook.

Report waiting (this is the new report waiting rule):
    stop the action with library message waiting action number 1 for the player.

The standard report waiting rule is not listed in any rulebook.

The block thinking rule is not listed in any rulebook.

Report thinking (this is the standard report thinking rule):
	say "You contemplate your situation."

Report someone thinking (this is the standard report someone thinking rule):
	say "[The actor] pauses for a moment, lost in thought."

The block waving hands rule is not listed in any rulebook.

Report waving hands (this is the standard report waving hands rule):
	say "You wave."

Report someone waving hands (this is the standard report someone waving hands rule):
	say "[The actor] waves."

The block smelling rule is not listed in any rulebook.

Report smelling (this is the standard report smelling rule):
	say "You smell nothing unexpected."

Report someone smelling (this is the standard report someone smelling rule):
	say "[The actor] sniffs[if the noun is the player] you[otherwise if the noun is not the location] [the noun][end if]."

The block listening rule is not listed in any rulebook.

Report listening (this is the standard report listening rule):
	say "You hear nothing unexpected."

Report someone listening (this is the standard report someone listening rule):
	say "[The actor] listens[if the noun is the player] to you[otherwise if the noun is not the location] to [the noun][end if]."

The block tasting rule is not listed in any rulebook.

Report tasting (this is the standard report tasting rule):
	say "You taste nothing unexpected."

Report someone tasting (this is the standard report someone tasting rule):
	say "[The actor] licks [if the noun is the player]you[otherwise][the noun][end if]."

The block jumping rule is not listed in any rulebook.

Report jumping (this is the standard report jumping rule):
	say "You jump on the spot, fruitlessly." 

Report someone jumping (this is the standard report someone jumping rule):
	say "[The actor] jumps."

The block rubbing rule is not listed in any rulebook.

Check an actor rubbing a person (this is the block rubbing people rule):
	if the actor is the player, say "That seems intrusively personal." instead;
	stop the action.

Report rubbing (this is the standard report rubbing rule):
	say "You achieve nothing by this."

Report someone rubbing (this is the standard report someone rubbing rule):
	say "[The actor] rubs [the noun]." instead. 

The block singing rule is not listed in any rulebook.

Report singing (this is the standard report singing rule):
	say "You sing a beautiful tune."

Report someone singing (this is the standard report someone singing rule):
	say "[The actor] assails your ears with an out-of-tune ditty."

Check quitting the game (this is the block quitting rule):
    say "To disconnect, just type 'quit' by itself." instead.
Check saving the game (this is the block saving rule):
    say "Saving the game state is not permitted." instead.
Check restoring the game (this is the block restoring rule):
    say "Restoring a saved game is not permitted." instead.
Check restarting the game (this is the block restarting rule):
    say "Restarting the game is not permitted." instead.
Check switching the story transcript on (this is the block transcript on rule):
    say "Transcripting is not permitted." instead.
Check switching the story transcript off (this is the block transcript off rule):
    say "Transcripting is not permitted." instead.
Check preferring abbreviated room descriptions (this is the block superbrief rule):
    say "Changing the room description setting is not permitted." instead.
Check preferring unabbreviated room descriptions (this is the block verbose rule):
    say "Changing the room description setting is not permitted." instead.
Check preferring sometimes abbreviated room descriptions (this is the block brief rule):
    say "Changing the room description setting is not permitted." instead.
Check switching score notification on (this is the block notify on rule):
    say "Changing the score notification setting is not permitted." instead.
Check switching score notification off (this is the block notify off rule):
    say "Changing the score notification setting is not permitted." instead.

Understand "undo" as a mistake ("Sorry, undo is not available.").
Understand "oops" as a mistake ("Don't worry about it.").
Understand "oops [text]" as a mistake ("Sorry, typo correction is not available.").

Chapter 4 - Player interactions

Section 1 - Giving items to other players

Generosity relates various things to one PC (called the potential recipient). The verb to be offered to implies the generosity relation.

Definition: a thing is offered if it is offered to the person asked.

Accepting is an action applying to one thing. Understand "accept [something offered]" as accepting. Understand "accept [something]" as accepting.

Check giving something to a PC (this is the translate giving to offering rule): try offering the noun to the second noun instead. The translate giving to offering rule is listed before the block giving rule in the check giving it to rules. The translate giving to offering rule is listed after the can't give to a non-person rule in the check giving it to rules.

Offering it to is an action applying to two things.

Carry out offering it to:
	now the noun is offered to the second noun.

Report offering it to (this is the standard report offering rule):
	say "You offer [the noun] to [the second noun]."

Report someone offering (this is the standard report someone offering rule):
	if the second noun is the player, say "[The actor] offers you [a noun]. (To accept it, type 'accept [the noun]'.)";
	otherwise say "[The actor] offers [the noun] to [the second noun]."

The accepting action has a person called the person offering (matched as "from").

Setting action variables for accepting:
	if the noun is enclosed by a person (called the current holder), now the person offering is the current holder.

Check accepting a person (this is the block accepting people rule):
	say "You resolve to accept [if the noun is the player]yourself[otherwise][the noun][end if] as a person, faults and all." instead.

Check accepting something (this is the can't accept what's not offered rule):
	if the noun is not offered to the player, say "[The noun] is not being offered to you." instead.

Carry out accepting:
	now the player carries the noun;
	now the noun is not offered to anyone.

Report accepting something (this is the standard report accepting rule):
	say "You accept [the noun][if the person offering is a person] from [the person offering][end if]."

Report someone accepting something (this is the standard report someone accepting rule):
	say "[The actor] accepts [the noun][if the person offering is the player] from you[otherwise if the person offering is a person] from [the person offering][end if]."

Every turn (this is the cancel expired offers rule):
	repeat with item running through things that are offered to someone begin;
		if the potential recipient of the item cannot see the item, now the item is not offered to anyone;
	end repeat.

Section 2 - Showing items to other players

The block showing rule is not listed in any rulebook.

Report showing something to someone (this is the standard report showing rule):
    say "You hold up [the noun] for [the second noun] to see."

Report someone showing something to someone (this is the standard report someone showing rule):
	say "[The actor] holds up [the noun] for ";
	if the second noun is the player
	begin;
		say "you to see. [run paragraph on]"; 
		try examining the noun;
	otherwise;
		say "[the second noun] to see.";
	end if.

Part 3 - Multi-realm awareness

Chapter 1 - Sending the player away

To send (victim - a PC) to (destination - text):
	if we are going a direction (called dir), change player attribute "waygone" of the victim to "[dir in short form]";
	otherwise change player attribute "waygone" of the victim to "?";
	say "<$t [mud-id of the victim]><$b [destination]></$t>".

Chapter 2 - Entrances

A special command handling rule (this is the handle locating rule):
	if the player's command matches "$locate [number]" begin;
		let the victim be a random PC identified by the number understood;
		if the victim is a PC, say entrance path to the victim;
		rule succeeds;
	end if.

A special command handling rule (this is the handle entrance checking rule):
	if the player's command matches the regular expression "^\$knock (.*)$" begin;
		let the desired entrance be the text matching subexpression 1;
		if the desired entrance is a valid entrance token begin;
			if an unreserved PC (called the new body) is in the PC-corral begin;
				now the new body is reserved;
				say "ok";
			otherwise;
				say "full";
			end if;
		otherwise;
			say "invalid";
		end if;
		rule succeeds;
	end if.

Table of Entrances
entrance room	entrance token
a room		"default"

When play begins (this is the initialize default entrance rule):
	repeat with R running through rooms begin;
		choose the row with entrance token of "default" in the Table of Entrances;
		change the entrance room entry to R;
		continue the action;
	end repeat.

To decide whether (T - indexed text) is a valid entrance token:
	repeat through the Table of Entrances begin;
		if T is the entrance token entry, decide yes;
	end repeat;
	decide no.

To decide which room is entrance corresponding to (T - indexed text):
	repeat through the Table of Entrances begin;
		if T is the entrance token entry, decide on the entrance room entry;
	end repeat;
	decide on nothing.

To decide which text is entrance token corresponding to (R - room):
	repeat through the Table of Entrances begin;
		if R is the entrance room entry, decide on the entrance token entry;
	end repeat;
	decide on "!".

To decide which object is closest entrance to (R - room):
	let the shortest distance be 10000;
	repeat through the Table of Entrances begin;
		let the candidate be the entrance room entry;
		let D be the number of moves from the candidate to R, using doors;
		if D is 0, decide on the candidate;
		if D is not -1 and D is less than the shortest distance begin;
			let the shortest distance be D;
			let the result be the candidate;
		end if;
	end repeat;
	decide on the result.

To say entrance path to (obj - something):
	if obj is off-stage begin;
		say "~";
		stop;
	end if;
	let R be the location of obj;
	let E be the closest entrance to R;
	if E is not a room begin;
		say "!";
		stop;
	end if;
	say "=[entrance token corresponding to E]";
	while E is not R begin;
		let thataway be the best route from E to R, using doors;
		say ",[thataway in short form]";
		let E2 be the room-or-door thataway from E;
		if E2 is a door, let E be the other side of E2 from E;
		otherwise let E be E2;
	end while;
	while E is not the holder of obj begin;
		if E holds something (called the next level) which encloses obj begin;
			if the next level is a PC, stop;
			say ",%[next level]";
			let E be the next level;
		otherwise;
			say ",!";
			stop;
		end if;
	end while.

To say (D - direction) in short form:
	(- print (address) ({D}.&name)-->0; -).

[BUGFIX: the type of "obj" should be a thing, not an object.]
To move (obj - an object) along entrance path (path - indexed text):
	while path is not "" begin;
		if path matches the regular expression "^(<^,>*)(?:,(.*))?$" begin;
			let token be the text matching subexpression 1;
			let path be the text matching subexpression 2;
			if token matches the regular expression "^=(.*)$" begin;
				[ move to entrance ]
				let E be the entrance corresponding to the text matching subexpression 1;
				if E is a room, move obj to E;
				otherwise stop;
			otherwise if token matches the regular expression "^%(.*)$";
				[ move into container/supporter ]
				let C be the thing near obj known as the text matching subexpression 1;
				if C is a thing, move obj to C;
				otherwise stop;
			otherwise if token is "!";
				[ error ]
				stop;
			otherwise if token is "~";
				[ off-stage ]
				remove obj from play;
				stop;
			otherwise;
				[ move in the named direction ]
				let thataway be the direction known as token;
				if thataway is a direction begin;
					let the target be the bugfixed room-or-door thataway from the location of obj;
					if the target is a room, move obj to the target;
					if the target is a door, move obj to the other side of the target from the location of obj;
				otherwise;
					stop;
				end if;
			end if;
		end if;
	end while.

[BUGFIX: this phrase is a shim to avoid the resolver bug, which seems to be necessary even though "room __ from __" doesn't have an indexed text parameter.]
To decide which object is bugfixed room-or-door (dir - an object) from (source - an object):
	let result be the room-or-door dir from source;
	decide on result.

[BUGFIX: this phrase is a shim to avoid the resolver bug, which seems to be necessary even though "other side of __ from __" doesn't have an indexed text parameter.]
To decide which object is bugfixed other side of (portal - an object) from (source - an object):
	let result be the other side of the portal from the source;
	decide on result.

[BUGFIX: the type of "obj" should be a thing, not an object.]
To decide which object is the thing near (obj - an object) known as (name - indexed text):
	let L be the list of objects called name near obj;
	if the number of entries of L is 0, decide on nothing;
	otherwise decide on entry 1 of L.

[BUGFIX: the type of "obj" should be a thing, not an object.]
To decide which object is the direction known as (name - indexed text):
	let L be the list of objects called name near the north;
	if the number of entries of L is 0, decide on nothing;
	otherwise decide on entry 1 of L.

Include (- [ GetMatchList pov text list  i t rv pov2;
	if (parsetoken_nesting > 0) {
		! save match globals
		@push match_from; @push match_length;
		@push number_of_classes;
		for (i=0: i<number_matched: i++) {
			t = match_list-->i; @push t;
			t = match_classes-->i; @push t;
			t = match_scores-->i; @push t;
		}
		@push number_matched;
		@push scope_reason; @push parser_inflection;
	 }

	parsetoken_nesting++;
	SwitchBufferIn(text);
	match_length = 0; number_matched = 0; match_from = 1; scope_reason = PARSING_REASON; parser_inflection = name;
	SearchScope(ScopeCeiling(pov), pov, NOUN_TOKEN);
	SwitchBufferOut();
	rv = I7ListFromMatchList(list);
	parsetoken_nesting--;

	if (parsetoken_nesting > 0) {
		! restore match globals
		@pull parser_inflection; @pull scope_reason;
		@pull number_matched;
		for (i=0: i<number_matched: i++) {
 			@pull t; match_scores-->i = t;
			@pull t; match_classes-->i = t;
			@pull t; match_list-->i = t;
   		}
		@pull number_of_classes;
		@pull match_length; @pull match_from;
	}
	return rv;
];

[ SwitchBufferIn text  i;
	for ( i=0: i<INPUT_BUFFER_LEN: i++ )
		buffer2->i = buffer->i;
	SetPlayersCommand(text);
	num_words = WordCount();
];

[ SwitchBufferOut  i;
	for ( i=0: i<INPUT_BUFFER_LEN: i++ )
		buffer->i = buffer2->i;
	VM_Tokenise(buffer, parse);
	num_words = WordCount();
	players_command = 100 + WordCount();
];

[ I7ListFromMatchList list desc obj dsize ex len i;
	if ((list==0) || (BlkType(list) ~= LIST_OF_TY)) return false;
	ex = BlkValueExtent(list);
	len = number_matched;
    if (match_length == 0) len = 0;   ! don't waste time with bad matches
	if (len+LIST_ITEM_BASE > ex) {
		if (BlkValueSetExtent(list, len+LIST_ITEM_BASE) == false)
			return 0;
	}
	BlkValueWrite(list, LIST_ITEM_KOV_F, OBJECT_TY);
	BlkValueWrite(list, LIST_LENGTH_F, len);
	for ( i=0: i<len: i++ )
		BlkValueWrite(list, i+LIST_ITEM_BASE, match_list-->i);
	return list;
]; -).

To decide what list of objects is the list of objects called (name - indexed text) near (pov - object): (- GetMatchList({pov},{-pointer-to:name},{-pointer-to-new:LIST_OF_TY}) -).

Chapter 3 - Server registers

Include (-
#ifdef TARGET_GLULX;
Constant SERV_DATA_SIZE = 256 + 4;
Array serv_data -> SERV_DATA_SIZE;

[ ParseServNum  rv sign i ch;
	rv = 0;
	if (serv_data->WORDSIZE == '-') { sign = -1; i = 1; }
	else { sign = 1; i = 0; }
	for ( : i<serv_data-->0: i++ ) {
		ch = serv_data->(WORDSIZE+i);
		if (ch < '0' || ch > '9') break;
		rv = rv * 10 + ch - '0';
	}
	return rv * sign;
];

[ GetServWord name;
	SuspendOutputBuffer();
	FyreCall(FY_CHANNEL, FYC_CONVERSATION);
	print "getword ", (I7_string) name;
	FyreCall(FY_CHANNEL, FYC_MAIN);
	FyreCall(FY_READLINE, serv_data, SERV_DATA_SIZE);
	ResumeOutputBuffer();
	return ParseServNum();
];

[ PutServWord name value;
	SuspendOutputBuffer();
	FyreCall(FY_CHANNEL, FYC_CONVERSATION);
	print "putword ", (I7_string) name, " ", value;
	FyreCall(FY_CHANNEL, FYC_MAIN);
	FyreCall(FY_READLINE, serv_data, SERV_DATA_SIZE);
	ResumeOutputBuffer();
	return (serv_data->WORDSIZE == '1');
];

[ GetServText name indt  len chunk pos i;
	SuspendOutputBuffer();
	FyreCall(FY_CHANNEL, FYC_CONVERSATION);
	print "gettext ", (I7_string) name, " ", SERV_DATA_SIZE - WORDSIZE;
	FyreCall(FY_CHANNEL, FYC_MAIN);
	FyreCall(FY_READLINE, serv_data, SERV_DATA_SIZE);

	len = ParseServNum();

	if (indt == 0) {
		indt = BlkAllocate(len+1, INDEXED_TEXT_TY, IT_Storage_Flags);
		if (~~indt) rfalse;
	} else {
		if (BlkValueSetExtent(indt, len+1, 1) == false) rfalse;
	}
	BlkValueWrite(indt, len, 0);

	pos = 0;
	while (len > 0) {
		FyreCall(FY_READLINE, serv_data, SERV_DATA_SIZE);
		chunk = serv_data-->0;
		len = len - chunk;
		for ( i=0: i<chunk: i++ )
			BlkValueWrite(indt, pos++, serv_data->(WORDSIZE+i));
	}
		
	ResumeOutputBuffer();
	return indt;
];

[ PutServText name indt;
	SuspendOutputBuffer();
	FyreCall(FY_CHANNEL, FYC_CONVERSATION);
	print "puttext ", (I7_string) name, " ", (INDEXED_TEXT_TY_Say) indt;
	FyreCall(FY_CHANNEL, FYC_MAIN);
	FyreCall(FY_READLINE, serv_data, SERV_DATA_SIZE);
	ResumeOutputBuffer();
	return indt;
];
#ifnot; ! TARGET_ZCODE
Array serv_id buffer 16;
Constant SERV_DATA_SIZE = 32; ! must be <= 255
Array serv_data -> SERV_DATA_SIZE;

[ GetServWord name  idstr rv;
	@output_stream 3 serv_id;
	print (I7_string) name;
	@output_stream -3;
	if (serv_id-->0 > 16) serv_id->1 = 16;
	idstr = serv_id + 1;
	@restore serv_data WORDSIZE idstr -> rv;
	if (rv) return serv_data-->0;
	return -1;
];

[ PutServWord name value  idstr rv;
	@output_stream 3 serv_id;
	print (I7_string) name;
	@output_stream -3;
	if (serv_id-->0 > 16) serv_id->1 = 16;
	idstr = serv_id + 1;
	serv_data-->0 = value;
	@save serv_data WORDSIZE idstr -> rv;
	return rv;
];

[ GetServText name indt  len rv chunk i pos;
	! tell the terp which text we want
	serv_id->0 = 4;
	serv_id->1 = 't'; serv_id->2 = 'x'; serv_id->3 = 't'; serv_id->4 = 'n';
	@output_stream 3 serv_data;
	print (I7_string) name;
	@output_stream -3;
	if (serv_data-->0 > SERV_DATA_SIZE) serv_data->1 = SERV_DATA_SIZE;
	len = serv_data->1;
	i = serv_data + WORDSIZE;
	@save i len serv_id -> rv;
	if (~~rv) rfalse;

	! find out how long it is and allocate space
	serv_id->4 = 'l';
	@restore serv_data WORDSIZE serv_id -> rv;
	if (~~rv) rfalse;
	len = serv_data-->0;
	if (indt == 0) {
		indt = BlkAllocate(len+1, INDEXED_TEXT_TY, IT_Storage_Flags);
		if (~~indt) rfalse;
	} else {
		if (BlkValueSetExtent(indt, len+1, 1) == false) rfalse;
	}

	! get the text data
	serv_id->4 = 'd';
	BlkValueWrite(indt, len, 0);
	pos = 0;
	while (len > 0) {
		if (len > SERV_DATA_SIZE)
			chunk = SERV_DATA_SIZE;
		else
			chunk = len;
		@restore serv_data chunk serv_id -> rv;
		if (~~rv) rfalse;
		for ( i=0: i<chunk: i++, pos++ )
			BlkValueWrite(indt, pos, serv_data->i);
		len = len - chunk;
	}

	return indt;
];

[ PutServText name indt  len rv chunk i pos;
	! tell the terp which text we want
	serv_id->0 = 4;
	serv_id->1 = 't'; serv_id->2 = 'x'; serv_id->3 = 't'; serv_id->4 = 'n';
	@output_stream 3 serv_data;
	print (I7_string) name;
	@output_stream -3;
	if (serv_data-->0 > SERV_DATA_SIZE) serv_data->1 = SERV_DATA_SIZE;
	len = serv_data->1;
	i = serv_data + WORDSIZE;
	@save i len serv_id -> rv;
	if (~~rv) rfalse;

	! declare its length
	serv_id->4 = 'l';
	len = IT_CharacterLength(indt);
	serv_data-->0 = len;
	@save serv_data WORDSIZE serv_id -> rv;
	if (~~rv) rfalse;

	! write the text data
	serv_id->4 = 'd';
	pos = 0;
	while (len > 0) {
		if (len > SERV_DATA_SIZE)
			chunk = SERV_DATA_SIZE;
		else
			chunk = len;
		for ( i=0: i<chunk: i++, pos++ )
			serv_data->i = BlkValueRead(indt, pos);
		@save serv_data chunk serv_id -> rv;
		if (~~rv) rfalse;
		len = len - chunk;
	}

	return indt;
];
#endif; ! TARGET_

 -);

To decide which number is server register (register name - text): (- GetServWord({register name}) -).
To change server register (register name - text) to (new value - number): (- PutServWord({register name}, {new value}); -).

To decide which indexed text is server text register (register name - text): (- GetServText({register name}, {-pointer-to-new:INDEXED_TEXT_TY}) -).
To change server text register (register name - text) to (new value - indexed text): (- PutServText({register name}, {-pointer-to:new value}); -).

To decide which indexed text is realm storage slot (register name - text):
    change server text register "ls_attr" to the register name;
    decide on server text register "ls_realmval".
To change realm storage slot (register name - text) to (new value - indexed text):
    change server text register "ls_attr" to the register name;
    change server text register "ls_realmval" to the new value.

[BUGFIX: "subject" parameter type]
To decide which indexed text is player storage slot (register name - text) of (subject - object):
    change server register "ls_playerid" to the mud-id of the subject;
    change server text register "ls_attr" to the register name;
    decide on server text register "ls_playerval".

[BUGFIX: "subject" parameter type]
To change player storage slot (register name - text) of (subject - object) to (new value - indexed text):
    change server register "ls_playerid" to the mud-id of the subject;
    change server text register "ls_attr" to the register name;
    change server text register "ls_playerval" to the new value.

[BUGFIX: "subject" parameter type]
To decide which indexed text is player attribute (attribute name - text) of (subject - object):
	change server register "pq_id" to the mud-id of the subject;
	change server text register "pq_attr" to the attribute name;
	decide on server text register "pq_attrval".

[BUGFIX: "subject" parameter type]
To change player attribute (attribute name - text) of (subject - object) to (new value - indexed text):
	change server register "pq_id" to the mud-id of the subject;
	change server text register "pq_attr" to the attribute name;
	change server text register "pq_attrval" to the new value.

Chapter 4 - Real-time events

A special command handling rule (this is the handle real-time events rule):
	if the player's command matches "$rtevent" begin;
		follow the real-time event rules;
		rule succeeds;
	end if.

Real-time event is a rulebook.

To request real-time events every (N - number) seconds:
	change server register "rteinterval" to N.

To stop real-time events:
	change server register "rteinterval" to 0.

Chapter 5 - Shutdown notification

A special command handling rule (this is the handle shutdown notices rule):
	if the player's command matches "$shutdown" begin;
		follow the realm shutdown rules;
		rule succeeds;
	end if.

Realm shutdown is a rulebook.

Part 4 - Library patches

Chapter 1 - Standard Rules

The investigate multiplayer awareness before action rule is listed instead of the investigate player's awareness before action rule in the specific action-processing rules.
The investigate multiplayer awareness after action and report rule is listed instead of the investigate player's awareness after action rule in the specific action-processing rules.
The report stage rule is not listed in the specific action-processing rules.

The specific action-processing rulebook has a list of PCs called the observant players.

This is the investigate multiplayer awareness before action rule:
	change the observant players to {};
	if action keeping silent is false:
		let the original player be the player;
		repeat with X running through connected PCs:
			change the player to X;
			consider the player's action awareness rules;
			if rule succeeded, add X to the observant players;
		change the player to the original player.

This is the investigate multiplayer awareness after action and report rule:
	if action keeping silent is false:
		let the original player be the player;
		repeat with X running through connected PCs:
			change the player to X;
			let observant be false;
			if X is listed in the observant players:
				let observant be true;
			otherwise:
				consider the player's action awareness rules;
				if rule succeeded, let observant be true;
			if observant is true:
				say "<$t [mud-id of X]>";
				consider the specific report rulebook;
				say "</$t>";
		change the player to the original player.

Chapter 2 - Parser segment

Include (-
    if (held_back_mode == 1) {
        held_back_mode = 0;
        VM_Tokenise(buffer, parse);
        jump ReParse;
    }

  .ReType;

	cobj_flag = 0;
    BeginActivity(READING_A_COMMAND_ACT); if (ForActivity(READING_A_COMMAND_ACT)==false) {
		Keyboard(buffer,parse);
		players_command = 100 + WordCount();
		num_words = WordCount();
    } if (EndActivity(READING_A_COMMAND_ACT)) jump ReType;

  .ReParse;

    parser_inflection = name;

    ! Initially assume the command is aimed at the player, and the verb
    ! is the first word

    num_words = WordCount();
    wn = 1;

    #Ifdef LanguageToInformese;
    LanguageToInformese();
    ! Re-tokenise:
    VM_Tokenise(buffer,parse);
    #Endif; ! LanguageToInformese

    num_words = WordCount();

    k=0;
    #Ifdef DEBUG;
    if (parser_trace >= 2) {
        print "[ ";
        for (i=0 : i<num_words : i++) {

            #Ifdef TARGET_ZCODE;
            j = parse-->(i*2 + 1);
            #Ifnot; ! TARGET_GLULX
            j = parse-->(i*3 + 1);
            #Endif; ! TARGET_
            k = WordAddress(i+1);
            l = WordLength(i+1);
            print "~"; for (m=0 : m<l : m++) print (char) k->m; print "~ ";

            if (j == 0) print "?";
            else {
                #Ifdef TARGET_ZCODE;
                if (UnsignedCompare(j, HDR_DICTIONARY-->0) >= 0 &&
                    UnsignedCompare(j, HDR_HIGHMEMORY-->0) < 0)
                     print (address) j;
                else print j;
                #Ifnot; ! TARGET_GLULX
                if (j->0 == $60) print (address) j;
                else print j;
                #Endif; ! TARGET_
            }
            if (i ~= num_words-1) print " / ";
        }
        print " ]^";
    }
    #Endif; ! DEBUG
    verb_wordnum = 1;
    actor = player;
    actors_location = ScopeCeiling(player);
    usual_grammar_after = 0;

  .AlmostReParse;

    scope_token = 0;
    action_to_be = NULL;

    ! Begin from what we currently think is the verb word

  .BeginCommand;

    wn = verb_wordnum;
    verb_word = NextWordStopped();

    ! If there's no input here, we must have something like "person,".

    if (verb_word == -1) {
        best_etype = STUCK_PE;
        jump GiveError;
    }

    ! Now try for "again" or "g", which are special cases: don't allow "again" if nothing
    ! has previously been typed; simply copy the previous text across

    if (verb_word == AGAIN2__WD or AGAIN3__WD) verb_word = AGAIN1__WD;
    if (verb_word == AGAIN1__WD) {
	print "['again' may only be used on a line by itself. Sorry.]^";
	jump ReType;
    }

    ! Save the present input in case of an "again" next time

    if (verb_word ~= AGAIN1__WD)
        for (i=0 : i<INPUT_BUFFER_LEN : i++) buffer3->i = buffer->i;

    if (usual_grammar_after == 0) {
        j = verb_wordnum;
        i = RunRoutines(actor, grammar); 
        #Ifdef DEBUG;
        if (parser_trace >= 2 && actor.grammar ~= 0 or NULL)
            print " [Grammar property returned ", i, "]^";
        #Endif; ! DEBUG

        if ((i ~= 0 or 1) && (VM_InvalidDictionaryAddress(i))) {
            usual_grammar_after = verb_wordnum; i=-i;
        }

        if (i == 1) {
            results-->0 = action;
            results-->1 = noun;
            results-->2 = second;
            rtrue;
        }
        if (i ~= 0) { verb_word = i; wn--; verb_wordnum--; }
        else { wn = verb_wordnum; verb_word = NextWord(); }
    }
    else usual_grammar_after = 0;
-) instead of "Parser Letter A" in "Parser.i6t".

Include (-
[ NounDomain domain1 domain2 context
	first_word i j k l answer_words marker;
    #Ifdef DEBUG;
    if (parser_trace >= 4) {
        print "   [NounDomain called at word ", wn, "^";
        print "   ";
        if (indef_mode) {
            print "seeking indefinite object: ";
            if (indef_type & OTHER_BIT)  print "other ";
            if (indef_type & MY_BIT)     print "my ";
            if (indef_type & THAT_BIT)   print "that ";
            if (indef_type & PLURAL_BIT) print "plural ";
            if (indef_type & LIT_BIT)    print "lit ";
            if (indef_type & UNLIT_BIT)  print "unlit ";
            if (indef_owner ~= 0) print "owner:", (name) indef_owner;
            new_line;
            print "   number wanted: ";
            if (indef_wanted == 100) print "all"; else print indef_wanted;
            new_line;
            print "   most likely GNAs of names: ", indef_cases, "^";
        }
        else print "seeking definite object^";
    }
    #Endif; ! DEBUG

    match_length = 0; number_matched = 0; match_from = wn;

    SearchScope(domain1, domain2, context);

    #Ifdef DEBUG;
    if (parser_trace >= 4) print "   [ND made ", number_matched, " matches]^";
    #Endif; ! DEBUG

    wn = match_from+match_length;

    ! If nothing worked at all, leave with the word marker skipped past the
    ! first unmatched word...

    if (number_matched == 0) { wn++; rfalse; }

    ! Suppose that there really were some words being parsed (i.e., we did
    ! not just infer).  If so, and if there was only one match, it must be
    ! right and we return it...

    if (match_from <= num_words) {
        if (number_matched == 1) {
            i=match_list-->0;
            return i;
        }

        ! ...now suppose that there was more typing to come, i.e. suppose that
        ! the user entered something beyond this noun.  If nothing ought to follow,
        ! then there must be a mistake, (unless what does follow is just a full
        ! stop, and or comma)

        if (wn <= num_words) {
            i = NextWord(); wn--;
            if (i ~=  AND1__WD or AND2__WD or AND3__WD or comma_word
                   or THEN1__WD or THEN2__WD or THEN3__WD
                   or BUT1__WD or BUT2__WD or BUT3__WD) {
                if (lookahead == ENDIT_TOKEN) rfalse;
            }
        }
    }

    ! Now look for a good choice, if there's more than one choice...

    number_of_classes = 0;

    if (number_matched == 1) i = match_list-->0;
    if (number_matched > 1) {
        i = Adjudicate(context);
        if (i == -1) rfalse;
        if (i == 1) rtrue;       !  Adjudicate has made a multiple
                             !  object, and we pass it on
    }

    ! If i is non-zero here, one of two things is happening: either
    ! (a) an inference has been successfully made that object i is
    !     the intended one from the user's specification, or
    ! (b) the user finished typing some time ago, but we've decided
    !     on i because it's the only possible choice.
    ! In either case we have to keep the pattern up to date,
    ! note that an inference has been made and return.
    ! (Except, we don't note which of a pile of identical objects.)

    if (i ~= 0) {
        if (dont_infer) return i;
        if (inferfrom == 0) inferfrom=pcount;
        pattern-->pcount = i;
        return i;
    }

    ! If we get here, there was no obvious choice of object to make.  If in
    ! fact we've already gone past the end of the player's typing (which
    ! means the match list must contain every object in scope, regardless
    ! of its name), then it's foolish to give an enormous list to choose
    ! from - instead we go and ask a more suitable question...

    if (match_from > num_words) jump Incomplete;

    ! Now we print up the question, using the equivalence classes as worked
    ! out by Adjudicate() so as not to repeat ourselves on plural objects...

	BeginActivity(ASKING_WHICH_DO_YOU_MEAN_ACT);
	if (ForActivity(ASKING_WHICH_DO_YOU_MEAN_ACT)) jump SkipWhichQuestion;

    if (context==CREATURE_TOKEN) L__M(##Miscellany, 45);
    else                         L__M(##Miscellany, 46);

    j = number_of_classes; marker = 0;
    for (i=1 : i<=number_of_classes : i++) {
        while (((match_classes-->marker) ~= i) && ((match_classes-->marker) ~= -i)) marker++;
        k = match_list-->marker;

        if (match_classes-->marker > 0) print (the) k; else print (a) k;

        if (i < j-1)  print (string) COMMA__TX;
        if (i == j-1) {
			#Ifdef SERIAL_COMMA;
			print ",";
        	#Endif; ! SERIAL_COMMA
        	print (string) OR__TX;
        }
    }
    L__M(##Miscellany, 57);

	.SkipWhichQuestion; EndActivity(ASKING_WHICH_DO_YOU_MEAN_ACT);

    ! ...and get an answer:

  .WhichOne;
    #Ifdef TARGET_ZCODE;
    for (i=2 : i<INPUT_BUFFER_LEN : i++) buffer2->i = ' ';
    #Endif; ! TARGET_ZCODE
    DisambigMode();
    answer_words=Keyboard(buffer2, parse2);

    ! Check for another player's command or a system command
    if (IsOtherCommand(buffer2)) {
        VM_CopyBuffer(buffer, buffer2);
        jump RECONSTRUCT_INPUT;
    }

    ! Conveniently, parse2-->1 is the first word in both ZCODE and GLULX.
    first_word = (parse2-->1);

    ! Take care of "all", because that does something too clever here to do
    ! later on:

    if (first_word == ALL1__WD or ALL2__WD or ALL3__WD or ALL4__WD or ALL5__WD) {
        if (context == MULTI_TOKEN or MULTIHELD_TOKEN or MULTIEXCEPT_TOKEN or MULTIINSIDE_TOKEN) {
            l = multiple_object-->0;
            for (i=0 : i<number_matched && l+i<63 : i++) {
                k = match_list-->i;
                multiple_object-->(i+1+l) = k;
            }
            multiple_object-->0 = i+l;
            rtrue;
        }
        L__M(##Miscellany, 47);
        jump WhichOne;
    }

    ! If the first word of the reply can be interpreted as a verb, then
    ! assume that the player has ignored the question and given a new
    ! command altogether.
    ! (This is one time when it's convenient that the directions are
    ! not themselves verbs - thus, "north" as a reply to "Which, the north
    ! or south door" is not treated as a fresh command but as an answer.)

    #Ifdef LanguageIsVerb;
    if (first_word == 0) {
        j = wn; first_word = LanguageIsVerb(buffer2, parse2, 1); wn = j;
    }
    #Endif; ! LanguageIsVerb
    if (first_word ~= 0) {
        j = first_word->#dict_par1;
        if ((0 ~= j&1) && ~~LanguageVerbMayBeName(first_word)) {
            VM_CopyBuffer(buffer, buffer2);
            jump RECONSTRUCT_INPUT;
        }
    }

    ! Now we insert the answer into the original typed command, as
    ! words additionally describing the same object
    ! (eg, > take red button
    !      Which one, ...
    !      > music
    ! becomes "take music red button".  The parser will thus have three
    ! words to work from next time, not two.)

    #Ifdef TARGET_ZCODE;
    k = WordAddress(match_from) - buffer; l=buffer2->1+1;
    for ( j=buffer + buffer->0 - 1 : j>=buffer+k+l : j-- ) j->0 = 0->(j-l);
    for (i=0 : i<l : i++) buffer->(k+i) = buffer2->(2+i);
    buffer->(k+l-1) = ' ';
    buffer->1 = buffer->1 + l;
    if (buffer->1 >= (buffer->0 - 1)) buffer->1 = buffer->0;
    #Ifnot; ! TARGET_GLULX
    k = WordAddress(match_from) - buffer;
    l = (buffer2-->0) + 1;
    for ( j=buffer+INPUT_BUFFER_LEN-1 : j>=buffer+k+l : j-- ) j->0 = j->(-l);
    for (i=0 : i<l : i++) buffer->(k+i) = buffer2->(WORDSIZE+i);
    buffer->(k+l-1) = ' ';
    buffer-->0 = buffer-->0 + l;
    if (buffer-->0 > (INPUT_BUFFER_LEN-WORDSIZE)) buffer-->0 = (INPUT_BUFFER_LEN-WORDSIZE);
    #Endif; ! TARGET_

    ! Having reconstructed the input, we warn the parser accordingly
    ! and get out.

	.RECONSTRUCT_INPUT;

	num_words = WordCount();
    wn = 1;
    #Ifdef LanguageToInformese;
    LanguageToInformese();
    ! Re-tokenise:
    VM_Tokenise(buffer,parse);
    #Endif; ! LanguageToInformese
	num_words = WordCount();
    players_command = 100 + WordCount();
	FollowRulebook(Activity_after_rulebooks-->READING_A_COMMAND_ACT, true);

    return REPARSE_CODE;

    ! Now we come to the question asked when the input has run out
    ! and can't easily be guessed (eg, the player typed "take" and there
    ! were plenty of things which might have been meant).

  .Incomplete;

    if (context == CREATURE_TOKEN) L__M(##Miscellany, 48);
    else                           L__M(##Miscellany, 49);

    #Ifdef TARGET_ZCODE;
    for (i=2 : i<INPUT_BUFFER_LEN : i++) buffer2->i=' ';
    #Endif; ! TARGET_ZCODE
    DisambigMode();
    answer_words = Keyboard(buffer2, parse2);

    ! Check for another player's command or a system command
    if (IsOtherCommand(buffer2)) {
        VM_CopyBuffer(buffer, buffer2);
        jump RECONSTRUCT_INPUT;
    }

    first_word=(parse2-->1);
    #Ifdef LanguageIsVerb;
    if (first_word==0) {
        j = wn; first_word=LanguageIsVerb(buffer2, parse2, 1); wn = j;
    }
    #Endif; ! LanguageIsVerb

    ! Once again, if the reply looks like a command, give it to the
    ! parser to get on with and forget about the question...

    if (first_word ~= 0) {
        j = first_word->#dict_par1;
        if (0 ~= j&1) {
            VM_CopyBuffer(buffer, buffer2);
            return REPARSE_CODE;
        }
    }

    ! ...but if we have a genuine answer, then:
    !
    ! (1) we must glue in text suitable for anything that's been inferred.

    if (inferfrom ~= 0) {
        for (j=inferfrom : j<pcount : j++) {
            if (pattern-->j == PATTERN_NULL) continue;
            #Ifdef TARGET_ZCODE;
            i = 2+buffer->1; (buffer->1)++; buffer->(i++) = ' ';
            #Ifnot; ! TARGET_GLULX
            i = WORDSIZE + buffer-->0;
            (buffer-->0)++; buffer->(i++) = ' ';
            #Endif; ! TARGET_

            #Ifdef DEBUG;
            if (parser_trace >= 5)
            	print "[Gluing in inference with pattern code ", pattern-->j, "]^";
            #Endif; ! DEBUG

            ! Conveniently, parse2-->1 is the first word in both ZCODE and GLULX.

            parse2-->1 = 0;

            ! An inferred object.  Best we can do is glue in a pronoun.
            ! (This is imperfect, but it's very seldom needed anyway.)

            if (pattern-->j >= 2 && pattern-->j < REPARSE_CODE) {
                PronounNotice(pattern-->j);
                for (k=1 : k<=LanguagePronouns-->0 : k=k+3)
                    if (pattern-->j == LanguagePronouns-->(k+2)) {
                        parse2-->1 = LanguagePronouns-->k;
                        #Ifdef DEBUG;
                        if (parser_trace >= 5)
                        	print "[Using pronoun '", (address) parse2-->1, "']^";
                        #Endif; ! DEBUG
                        break;
                    }
            }
            else {
                ! An inferred preposition.
                parse2-->1 = VM_NumberToDictionaryAddress(pattern-->j - REPARSE_CODE);
                #Ifdef DEBUG;
                if (parser_trace >= 5)
                	print "[Using preposition '", (address) parse2-->1, "']^";
                #Endif; ! DEBUG
            }

            ! parse2-->1 now holds the dictionary address of the word to glue in.

            if (parse2-->1 ~= 0) {
                k = buffer + i;
                #Ifdef TARGET_ZCODE;
                @output_stream 3 k;
                 print (address) parse2-->1;
                @output_stream -3;
                k = k-->0;
                for (l=i : l<i+k : l++) buffer->l = buffer->(l+2);
                i = i + k; buffer->1 = i-2;
                #Ifnot; ! TARGET_GLULX
                k = Glulx_PrintAnyToArray(buffer+i, INPUT_BUFFER_LEN-i, parse2-->1);
                i = i + k; buffer-->0 = i - WORDSIZE;
                #Endif; ! TARGET_
            }
        }
    }

    ! (2) we must glue the newly-typed text onto the end.

    #Ifdef TARGET_ZCODE;
    i = 2+buffer->1; (buffer->1)++; buffer->(i++) = ' ';
    for (j=0 : j<buffer2->1 : i++,j++) {
        buffer->i = buffer2->(j+2);
        (buffer->1)++;
        if (buffer->1 == INPUT_BUFFER_LEN) break;
    }
    #Ifnot; ! TARGET_GLULX
    i = WORDSIZE + buffer-->0;
    (buffer-->0)++; buffer->(i++) = ' ';
    for (j=0 : j<buffer2-->0 : i++,j++) {
        buffer->i = buffer2->(j+WORDSIZE);
        (buffer-->0)++;
        if (buffer-->0 == INPUT_BUFFER_LEN) break;
    }
    #Endif; ! TARGET_

    ! (3) we fill up the buffer with spaces, which is unnecessary, but may
    !     help incorrectly-written interpreters to cope.

    #Ifdef TARGET_ZCODE;
    for (: i<INPUT_BUFFER_LEN : i++) buffer->i = ' ';
    #Endif; ! TARGET_ZCODE

    return REPARSE_CODE;

]; ! end of NounDomain
-) instead of "Noun Domain" in "Parser.i6t".

Include (-
[ DisambigMode  i end c;
	print "<$d ";

	i = WordAddress(1) - buffer;
	#ifdef TARGET_ZCODE;
	end = WORDSIZE + buffer->1;
	#ifnot;
	end = WORDSIZE + buffer-->0;
	#endif;
	for (: i<end: i++) {
		c = buffer->i;
		if (c ~= '>') print (char) c;
	}

	print ">";
];

[ IsOtherCommand buf  i end c;
	! if it starts with a dollar sign...
	if (buf->WORDSIZE == '$') rtrue;

	#ifdef TARGET_ZCODE;
	end = WORDSIZE + buf->1;
	#ifnot;
	end = WORDSIZE + buf-->0;
	#endif;

	! if it starts with digits and then a colon...
	for (i = WORDSIZE: i<end: i++) {
		c = buf->i;
		if (c == ':' && i > WORDSIZE) rtrue;
		else if (c < '0' || c > '9') rfalse;
	}

	rfalse;
];
-).

Chapter 3 - WorldModel segment

Include (-
[ ChangePlayer obj  pn;
    if (~~(obj ofclass K8_person)) return RunTimeProblem(RTP_CANTCHANGE, obj);
    if (~~(OnStage(obj))) return RunTimeProblem(RTP_CANTCHANGEOFFSTAGE, obj);
    if (obj == player) return;

    if (player ofclass i7_pc_kind)
        SavePronouns(player);
    if (obj ofclass i7_pc_kind)
        LoadPronouns(obj);

    give player ~concealed;
    ! if (player has remove_proper) give player ~proper;
    if (player == selfobj) {
    	player.saved_short_name = player.short_name; player.short_name = FORMER__TX;
    }
    player = obj;
    if (player == selfobj) {
    	player.short_name = player.saved_short_name;
    }
    ! if (player hasnt proper) give player remove_proper; ! when changing out again
    ! give player concealed proper;
    give player concealed;

    location = LocationOf(player); real_location = location;
    MoveFloatingObjects();
    SilentlyConsiderLight();
];
-) instead of "Changing the Player" in "WorldModel.i6t".

Chapter 4 - Printing segment

Include (-
[ IndefArt obj i;
	if (obj == 0) { print (string) NOTHING__TX; rtrue; }
    i = indef_mode; indef_mode = true;
    if (obj has proper) { indef_mode = NULL; print (PSN__) obj; indef_mode = i; return; }
    if (obj provides article) {
        PrintOrRun(obj, article, true); print " ", (PSN__) obj; indef_mode = i;
        return;
    }
    PrefaceByArticle(obj, 2); indef_mode = i;
];

[ CIndefArt obj i;
	if (obj == 0) { CPrintOrRun(NOTHING__TX, 0); rtrue; }
    i = indef_mode; indef_mode = true;
    if (obj has proper) {
    	indef_mode = NULL;
		caps_mode = true;
    	print (PSN__) obj;
    	indef_mode = i;
    	caps_mode = false;
    	return;
    }
    if (obj provides article) {
        CPrintOrRun(obj, article); print " ", (PSN__) obj; indef_mode = i;
        return;
    }
    PrefaceByArticle(obj, 2, 0, 1); indef_mode = i;
];

[ DefArt obj i;
    i = indef_mode; indef_mode = false;
    if ((~~obj ofclass Object) || obj has proper) {
        indef_mode = NULL; print (PSN__) obj; indef_mode = i;
        return;
    }
    PrefaceByArticle(obj, 1); indef_mode = i;
];

[ CDefArt obj i;
    i = indef_mode; indef_mode = false;
    if ((obj ofclass Object) && (obj has proper)) {
    	indef_mode = NULL;
    	caps_mode = true;
    	print (PSN__) obj;
    	indef_mode = i;
    	caps_mode = false;
    	return;
    }
    if ((~~obj ofclass Object) || obj has proper) {
        indef_mode = NULL; print (PSN__) obj; indef_mode = i;
        return;
    }
    PrefaceByArticle(obj, 0); indef_mode = i;
];

[ PrintShortName obj i;
    i = indef_mode; indef_mode = NULL;
    PSN__(obj); indef_mode = i;
];
-) instead of "Object Names III" in "Printing.i6t".

Chapter 7 - Actions segment

Include (-
[ REQUESTED_ACTIONS_REQUIRE_R;
	if ((actor ~= player) && (act_requester)) {
		if (actor ofclass i7_pc_kind) {
			print "You can't order other players.^";
			RulebookFails(); rtrue;
		}
		@push say__p;
		say__p = 0;
		ProcessRulebook(PERSUADE_RB);
		if (RulebookSucceeded() == false) {
			if (say__p == FALSE) L__M(##Miscellany, 72, actor);
			RulebookFails(); rtrue;
		}
		@pull say__p;
	}
	rfalse;
];
-) instead of "Requested Actions Require Persuasion Rule" in "Actions.i6t".

Guncho Realms ends here.

---- DOCUMENTATION ----

N/A
