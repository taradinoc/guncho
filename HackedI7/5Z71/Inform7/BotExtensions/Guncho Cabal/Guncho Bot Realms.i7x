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

To decide which bot is the bot with ID (N - number):
	repeat with B running through bots:
		if the bot-ID of B is N, decide on B.

Bot connection is an object-based rulebook.
Bot disconnection is an object-based rulebook.

To connect (victim - a bot) to (realm - text):
	say "$connect [bot-ID of the victim] [realm][line break]".

Bot-dubbing is an action applying to one topic. Understand "$youare [text]" as bot-dubbing.

Carry out bot-dubbing:
	say "bot-dub [topic understood][line break]";
	if the topic understood matches the regular expression "^(\d+) (\d+)$":
		let botID be the numeric value of the text matching subexpression 1;
		let holoID be the numeric value of the text matching subexpression 2;
		say "botID=[botID] holoID=[holoID][line break]";
		let newbie be the bot with ID botID;
		if the newbie is a bot:
			now the newbie is connected;
			change the holodeck-ID of the newbie to holoID;
			change the player to the newbie;
			follow the bot connection rules for the newbie.

Part 2 - The Holodeck and Props

The holodeck-corral is a room.

A thing can be remote. A thing is usually remote. A bot is never remote.
A room can be remote. A room is usually remote. The holodeck-corral is not remote.
A thing has a number called holodeck-ID.
A room has a number called holodeck-ID.
A thing has indexed text called the remote name.
A room has indexed text called the remote name.

Understand the remote name property as describing a thing.

Rule for printing the name of a remote thing (called T):
	say the remote name of T.

Rule for printing the name of a remote room (called R):
	say the remote name of R.

A holodeck is a kind of thing. There are 5 holodecks.

Holosimulation relates one holodeck (called the associated holodeck) to various rooms. The verb to be simulated by implies the holosimulation relation.

A holodeck-room is a kind of room. It is always remote. There are 20 holodeck-rooms.

A holodeck-thing is a kind of thing. There are 50 holodeck-things in the holodeck-corral.

A holodeck-container is a kind of container. There are 20 holodeck-containers in the holodeck-corral.

A holodeck-supporter is a kind of supporter. There are 20 holodeck-supporters in the holodeck-corral.

A holodeck-person is a kind of person. There are 20 holodeck-persons in the holodeck-corral.

The plural of holodeck-man is holodeck-men. A holodeck-man is a kind of man. There are 20 holodeck-men in the holodeck-corral.

The plural of holodeck-woman is holodeck-women. A holodeck-woman is a kind of woman. There are 20 holodeck-women in the holodeck-corral.

Definition: a thing is unallocated if its holodeck-ID is 0.
Definition: a room is unallocated if its holodeck-ID is 0.

Definition: a thing is reclaimable if it is off-stage or it is in a reclaimable room.
Definition: a room is reclaimable if it does not enclose a bot and it is not adjacent to a room that encloses a bot.

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
	change the remote name of the victim to "".

To decide which object is a newly allocated (D - description) with ID (N - number):
	repeat with M running through members of D:
		if M is not unallocated and M is not reclaimable, next;
		change the holodeck-ID of M to N;
		decide on M;
	say "*** Out of holodeck props ***[line break]";
	decide on nothing.

To decide which object is the object with ID (N - number):
	repeat with R running through rooms:
		if the holodeck-ID of R is N, decide on R;
	repeat with T running through things:
		if the holodeck-ID of T is N, decide on T.

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
	PrintActionArg(n, nk);
	print " ";
	PrintActionArg(s, sk);
	if (nk == UNDERSTANDING_TY) {
		print " "; PrintSnippet(n);
	} else if (sk == UNDERSTANDING_TY) {
		print " "; PrintSnippet(s);
	}
	new_line;
];

[ PrintActionArg n nk;
	switch (nk) {
		NUMBER_TY, OBJECT_TY: print n;
		UNDERSTANDING_TY: print "$";
		TRUTH_STATE_TY: if (n) print "1"; else print "0";
		default: print ".";
	}
];
-).

Chapter 2 - Receiving Actions

[XXX]

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
