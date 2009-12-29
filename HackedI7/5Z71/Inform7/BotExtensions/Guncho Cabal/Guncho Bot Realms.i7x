Version 1/091215 of Guncho Bot Realms by Guncho Cabal begins here.

"This extension implements the I7-side changes needed for bot realms."

Use authorial modesty.
Use full-length room descriptions.
Use dynamic memory allocation of at least 16384.
Use MAX_STATIC_DATA of 500000.

Part 0 - Utility functions

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

Part 1 - Player characters and bots

A PC is a kind of person.

A bot is a kind of person.
A bot can be connected or disconnected. A bot is usually disconnected.
A bot has a number called bot-ID.
A bot has a number called direction-ID-index.

To decide which bot is the bot with ID (N - number):
	repeat with B running through bots:
		if the bot-ID of B is N, decide on B.

The verb to be directionally indexed as implies the direction-ID-index property.
Definition: a number is direction-index-available if no bot is directionally indexed as it.

Bot connection is an object-based rulebook.
Bot disconnection is an object-based rulebook.

To connect (victim - a bot) to (realm - text):
	say "$connect [bot-ID of the victim] [realm][line break]".

Bot-dubbing is an action applying to one topic. Understand "$youare [text]" as bot-dubbing.

Carry out bot-dubbing:
	if the topic understood matches the regular expression "^(\d+) (\d+)$":
		let botID be the numeric value of the text matching subexpression 1;
		let holoID be the numeric value of the text matching subexpression 2;
		let newbie be the bot with ID botID;
		if the newbie is a bot:
			change the holodeck-ID of the newbie to holoID;
			if a holodeck (called H) is empty, change the associated holodeck of the newbie to H;
			otherwise say "*** Out of holodecks ***[line break]".

Bot-placing is an action applying to one topic. Understand "$yourloc [text]" as bot-placing.

Carry out bot-placing:
	if the topic understood matches the regular expression "^(\d+) (\d+)$":
		let botID be the numeric value of the text matching subexpression 1;
		let holoID be the numeric value of the text matching subexpression 2;
		let newbie be the bot with ID botID;
		if the newbie is a bot:
			now the newbie is connected;
			move the newbie to the object with ID holoID;
			change the player to the newbie;
			follow the bot connection rules for the newbie.

Part 2 - The Holodeck and Props

Chapter 1 - Holodeck kinds

The holodeck-corral is a room.

A thing can be remote. A bot is never remote.
A room can be remote. The holodeck-corral is not remote.
A direction can be remote. A direction is usually not remote.
A thing has a number called holodeck-ID.
A room has a number called holodeck-ID.
A direction has a list of numbers called holodeck-IDs.
A thing has indexed text called the remote name.
A room has indexed text called the remote name.
A direction has indexed text called the remote name.

To decide which number is the holodeck-ID of (dir - direction) for (B - bot):
	let idx be the direction-ID-index of B;
	let L be the holodeck-IDs of dir;
	if the number of entries in L is at least idx:
		decide on entry idx of L;
	otherwise:
		say "*** [B] can't refer to direction [dir] ***[line break]";
		decide on 0.

To decide which direction is the direction identified by (ID - number) for (B - bot):
	repeat with dir running through directions:
		if ID is listed in the holodeck-IDs of dir, decide on dir;
	decide on nothing.

Understand the remote name property as describing a thing.

Rule for printing the name of a remote thing (called T):
	say the remote name of T.

Rule for printing the name of a remote room (called R):
	say the remote name of R.

A holodeck is a kind of thing. There are 5 holodecks.

A room has an object called the associated holodeck. The associated holodeck of a room is usually nothing.
A thing has an object called the associated holodeck. The associated holodeck of a thing is usually nothing.
A direction has an object called the associated holodeck. The associated holodeck of a direction is usually nothing.
The verb to be simulated by implies the associated holodeck property.

Definition: a holodeck is empty rather than non-empty if no things are simulated by it.

Holodeck-direction-1 is a direction. It is remote.
Holodeck-direction-2 is a direction. It is remote.
Holodeck-direction-3 is a direction. It is remote.
Holodeck-direction-4 is a direction. It is remote.
Holodeck-direction-5 is a direction. It is remote.
Holodeck-direction-6 is a direction. It is remote.
Holodeck-direction-7 is a direction. It is remote.
Holodeck-direction-8 is a direction. It is remote.
Holodeck-direction-9 is a direction. It is remote.
Holodeck-direction-10 is a direction. It is remote.

A holodeck-room is a kind of room. It is always remote. There are 20 holodeck-rooms.

A holodeck-thing is a kind of thing. It is always remote. There are 50 holodeck-things in the holodeck-corral.

[XXX can't define doors without connecting them to the map]
[A holodeck-door is a kind of door. It is always remote. There are 20 holodeck-doors.]

[XXX backdrops can't easily be moved the way we want to move them, without some template hacking]
[A holodeck-backdrop is a kind of backdrop. It is always remote. There are 20 holodeck-backdrops.]

A holodeck-container is a kind of container. It is always remote. There are 20 holodeck-containers in the holodeck-corral.

A holodeck-supporter is a kind of supporter. It is always remote. There are 20 holodeck-supporters in the holodeck-corral.

A holodeck-person is a kind of person. It is always remote. There are 20 holodeck-persons in the holodeck-corral.

The plural of holodeck-man is holodeck-men. A holodeck-man is a kind of man. It is always remote. There are 20 holodeck-men in the holodeck-corral.

The plural of holodeck-woman is holodeck-women. A holodeck-woman is a kind of woman. It is always remote. There are 20 holodeck-women in the holodeck-corral.

A holodeck-PC is a kind of PC. It is always remote. There are 20 holodeck-PCs in the holodeck-corral.

Definition: a thing is unallocated if its holodeck-ID is 0.
Definition: a room is unallocated if its holodeck-ID is 0.
Definition: a direction is unallocated if its holodeck-IDs is empty.

Definition: a thing is reclaimable if it is off-stage or it is in a reclaimable room.
Definition: a room is reclaimable if it does not enclose a bot and it is not adjacent to a room that encloses a bot.
Definition: a direction is reclaimable if it is not an exit from a room which is not reclaimable.

Exiting-from relates a direction (called D) to a room (called R) when the room-or-door D from R is not nothing. The verb to be an exit from implies the exiting-from relation.

To reclaim (victim - object):
	let C be the first thing held by the victim;
	while C is not nothing:
		let C2 be the next thing held after C;
		reclaim C;
		let C be C2;
	repeat with P running through things which are part of the victim:
		reclaim P;
	if the victim is a thing:
		now the victim is in the holodeck-corral;
	otherwise if the victim is a room:
		repeat with D running through directions:
			change the D exit of the victim to nowhere;
	change the holodeck-ID of the victim to 0;
	change the remote name of the victim to "";
	change the associated holodeck of the victim to nothing.

To decide which object is a fresh holodeck prop with kind ID (KN - number) and object ID (ON - number) for (B - bot): (- FreshHolodeckProp({KN}, {ON}, {B}) -).

Include (-
[ FreshHolodeckProp k id bot  x a l;
	objectloop (x ofclass k) {
		! only accept remote objects
		#iftrue (+ remote +) < FBNA_PROP_NUMBER;
		if (x hasnt (+ remote +)) continue;
		#ifnot;
		if (~~(x provides (+ remote +) && x.(+ remote +))) continue;
		#endif;
		! if k is thing, we'll accept holodeck-thing (direct subkind) but not holodeck-person (indirect subkind)
		a = x.&2;
		if (~~a) continue;
		l = x.#2 / WORDSIZE;
		if (a-->0 ~= k && (l == 1 || a-->1 ~= k)) continue;
		! BUGFIX: 5Z71 compiles adjectives inside ( + + ) incorrectly (as routine calls applied to subst__v instead of names)
		subst__v = x;
		if (~~((+ unallocated +) && (+ reclaimable +))) continue;
		! found one
		if (k == (+ direction +))
			LIST_OF_TY_InsertItem(x.(+ holodeck-IDs +), id, 1, bot.(+ direction-ID-index +));
		else
			x.(+ holodeck-ID +) = id;
		return x;
	}
	print "*** Out of holodeck props (k = ", (I7_Kind_Name) k, ") ***^";
	rfalse;
];
-) after "Miscellaneous Loose Ends" in "Output.i6t". [which is where FBNA_PROP_NUMBER is defined]

To decide which object is the object with ID (N - number): (- FindObjectByID({N}) -).

Include (-
[ FindObjectByID num  x;
	objectloop (x) {
		if (x provides (+ holodeck-ID +)) {
			if (x.(+ holodeck-ID +) == num)
				return x;
		} else if (x provides (+ holodeck-IDs +)) {
			if (LIST_OF_TY_FindItem(x.(+ holodeck-IDs +), num))
				return x;
		}
	}
	rfalse;
];
-).

Chapter 2 - Building the holodeck

Object-defining is an action applying to one topic. Understand "$object [text]" as object-defining.

Carry out object-defining:
	if the topic understood matches the regular expression "^(\d+) (\d+) (\d+) (\S+) (\d+|\.) (.*)$":
		let botID be the numeric value of the text matching subexpression 1;
		let curbot be the bot with ID botID;
		let objID be the numeric value of the text matching subexpression 2;
		let kindID be the numeric value of the text matching subexpression 3;
		let parentRel be the text matching subexpression 4;
		if the text matching subexpression 5 is ".":
			let parentID be 0;
		otherwise:
			let parentID be the numeric value of the text matching subexpression 5;
		let objName be the text matching subexpression 6;
		let the new prop be a fresh holodeck prop with kind ID kindID and object ID objID for curbot;
		if the new prop is nothing, stop;
		unless parentID is 0:
			if parentRel is "part":
				now the new prop is part of the object with ID parentID;
			otherwise:
				move the new prop to the object with ID parentID;
		change the remote name of the new prop to objName;
		now the new prop is simulated by the associated holodeck of curbot.

Part 2 - Actions

Chapter 1 - Sending Actions

Section 1 - Replacement Try Phrases (in place of Section SR5/4/1 - Actions, activities and rules - Trying actions in Standard Rules by Graham Nelson)

To silently/-- try silently/-- (doing something - action)
	(documented at ph_try):
	(- Send{doing something}; -).

Include (-
[ SendTryAction req by ac n s stora  i nk sk f;
	if (stora) return TryAction(req, by, ac, n, s, stora);
	if (req || ~~(by ofclass (+ bot +))) rfalse;
	i = FindAction(ac);
	f = ActionData-->(i+AD_REQUIREMENTS);
	if (f & NEED_NOUN_ABIT) nk = ActionData-->(i+AD_NOUN_KOV);
	if (f & NEED_SECOND_ABIT) sk = ActionData-->(i+AD_SECOND_KOV);
	print "$action ", by.(+ bot-ID +), " ", ac, " ";
	PrintActionArg(n, nk, by);
	print " ";
	PrintActionArg(s, sk, by);
	if (nk == UNDERSTANDING_TY) {
		print " "; PrintSnippet(n);
	} else if (sk == UNDERSTANDING_TY) {
		print " "; PrintSnippet(s);
	}
	new_line;
];

[ PrintActionArg n nk bot  i list;
	switch (nk) {
		NUMBER_TY: print n;
		OBJECT_TY:
			if (n provides (+ holodeck-ID +)) {
				print n.(+ holodeck-ID +);
			} else if (n provides (+ holodeck-IDs +)) {
				list = n.(+ holodeck-IDs +);
				i = bot.(+ direction-ID-index +);
				if (LIST_OF_TY_GetLength(list) >= i)
					print LIST_OF_TY_GetItem(list, i);
				else
					print ".";
			} else {
				print ".";
			}
		UNDERSTANDING_TY: print "$";
		TRUTH_STATE_TY: if (n) print "1"; else print "0";
		default: print ".";
	}
];
-).

Chapter 2 - Receiving Actions

Action-firing is an action applying to one topic. Understand "$action [text]" as action-firing.

Carry out action-firing:
	if the topic understood matches the regular expression "^(\d+) (\d+) (\d+) (\S+) (\S+) ?(.*)$":
		let botID be the numeric value of the text matching subexpression 1;
		let actorID be the numeric value of the text matching subexpression 2;
		let actionID be the numeric value of the text matching subexpression 3;
		if the text matching subexpression 4 is ".", let nounID be 0;
		otherwise let nounID be the numeric value of the text matching subexpression 4;
		if the text matching subexpression 5 is ".", let secondID be 0;
		otherwise let secondID be the numeric value of the text matching subexpression 5;
		let rest be the text matching subexpression 6;
		[fire the action]
		let curbot be the bot with ID botID;
		if curbot is not a connected bot, stop;
		change the player to curbot;
		change the actor to the object with ID actorID;
		handle the remote action actionID with noun nounID second secondID and remainder rest.

To handle the remote action (N - number) with noun (nid - number) second (sid - number) and remainder (T - indexed text): (- HandleRemoteAction({N}, {nid}, {sid}, {-pointer-to:T}); -).

Include (-
[ HandleRemoteAction act nid sid rest  i;
	i = FindAction(act);
	if (i < 0) rfalse;
	switch (ActionData-->(i + AD_NOUN_KOV)) {
		TRUTH_STATE_TY: parsed_number = (nid ~= 0);
		NUMBER_TY: parsed_number = nid;
		OBJECT_TY: noun = FindObjectByID(nid);
		UNDERSTANDING_TY: SetPlayersCommand(rest); parsed_number = players_command;
	}
	switch (ActionData-->(i + AD_SECOND_KOV)) {
		TRUTH_STATE_TY: parsed_number = (sid ~= 0);
		NUMBER_TY: parsed_number = sid;
		OBJECT_TY: second = FindObjectByID(sid);
		UNDERSTANDING_TY: SetPlayersCommand(rest); parsed_number = players_command;
	}
	action = act;
	return ProcessRulebook((+ after rules +));
];
-).

[We use carry out rules to handle messages from the server, and trigger our own after rules to respond to remote actions. We don't need the rest of the standard action processing rules.]

The investigate player's awareness before action rule is not listed in the specific action-processing rulebook.
The check stage rule is not listed in the specific action-processing rulebook.
The after stage rule is not listed in the specific action-processing rulebook.
The investigate player's awareness after action rule is not listed in the specific action-processing rulebook.
The report stage rule is not listed in the specific action-processing rulebook.

Chapter 3 - New Action Definitions

Offering it to is an action applying to two things.
Accepting is an action applying to one thing.
Chatting is an action applying to one topic.
Emoting is an action applying to one topic.

Part 3 - Server Communication

Chapter 1 - Initial Handshake

When play begins (this is the initial server handshake rule):
	describe the known actions;
	describe the known kinds;
	describe the known properties;
	let N be 1;
	repeat with B running through bots:
		change the bot-ID of B to N;
		say "$addbot [N] [B][line break]";
		increase N by 1.

To describe the known actions: (- ShowKnownActions(); -).
To describe the known kinds: (- ShowKnownKinds(); -).
To describe the known properties: (- ShowKnownProperties(); -).

Include (-
[ ShowKnownActions  i c act abits;
	c = ActionData-->0;
	for (i=1: i<=c: i=i+AD_RECORD_SIZE) {
		abits = ActionData-->(i + AD_REQUIREMENTS);
		if (abits & OUT_OF_WORLD_ABIT) continue;
		act = ActionData-->(i + AD_ACTION);
		print "$register action ", act, " ";
		if (abits & NEED_NOUN_ABIT)
			ShowActionArgType(ActionData-->(i + AD_NOUN_KOV));
		else
			print "-";
		print " ";
		if (abits & NEED_SECOND_ABIT)
			ShowActionArgType(ActionData-->(i + AD_SECOND_KOV));
		else
			print "-";
		print " ";
		DB_Action(0, 0, act, 0, 0, 2);
		print "^";
	}
];

[ ShowActionArgType kov;
	switch (kov) {
		OBJECT_TY: print "o";
		NUMBER_TY: print "n";
		UNDERSTANDING_TY: print "t";
		default: print "?";
	}
];

[ ShowKnownKinds  x;
	objectloop (x ofclass Class && IsI7Kind(x))
		print "$register kind ", x, " ", (I7_Kind_Name) x, "^";
];

[ IsI7Kind cl  i l a;
	if (cl == K0_kind) rfalse;
	! read the class's property 2 from its inherited property table
	! the :: operator would make this easy, if only we had an instance
	#ifdef TARGET_ZCODE;
	i = 0-->(((0-->5)+124+cl*14)/2);
	i = CP__Tab(i + 2*(0->i) + 1, -1)+6;
	a = CP__Tab(i, 2);
	if (~~a) rfalse;
	switch (((a-1)->0) & $C0) {
		0: l = 1;
		$40: l = 2;
		$80: l = ((a-1)->0) & $3F;
	}
	l = l / WORDSIZE;
	#ifnot;
	i = CP__Tab(cl, 2);
	if (~~i) rfalse;
	a = i-->1;
	@aloads i 1 l;
	#endif;

	for (i=0: i<l: i++)
		if (a-->i == K0_kind) rtrue;
	rfalse;
];

Array GunchoBoolProperties -->
	(+ neuter +) (+ female +) (+ scenery +)
	(+ openable +) (+ open +) (+ lockable +) (+ locked +)
	(+ transparent +) (+ lit +) (+ fixed in place +)
	0;

[ ShowKnownProperties  i p off;
	for (i=0::i++) {
		p = GunchoBoolProperties-->i;
		if (~~p) rfalse;
		print "$register prop ", p, " b ";
		if (p < FBNA_PROP_NUMBER) off = attribute_offsets-->p;
		else off = property_offsets-->p;
		print (string) property_metadata-->off, "^";
	}
];
-).

Chapter 2 - Server registers

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

Chapter 3 - Real-time events

Real-time-firing is an action applying to nothing. Understand "$rtevent" as real-time-firing.

Carry out real-time-firing: follow the real-time event rules.

Real-time event is a rulebook.

To request real-time events every (N - number) seconds:
	change server register "rteinterval" to N.

To stop real-time events:
	change server register "rteinterval" to 0.

Chapter 4 - Shutdown notification

Shutdown-notifying is an action applying to nothing. Understand "$shutdown" as shutdown-notifying.

Carry out shutdown-notifying: follow the realm shutdown rules.

Realm shutdown is a rulebook.

Guncho Bot Realms ends here.

---- DOCUMENTATION ----

N/A
