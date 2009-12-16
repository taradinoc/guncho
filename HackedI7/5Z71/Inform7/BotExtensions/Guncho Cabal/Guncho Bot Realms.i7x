Version 1/091215 of Guncho Bot Realms by Guncho Cabal begins here.

"This extension implements the I7-side changes needed for bot realms."

Use authorial modesty.
Use full-length room descriptions.
Use dynamic memory allocation of at least 16384.
Use MAX_STATIC_DATA of 500000.

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
[ SendTryAction req by ac n s stora;
	if (~~(ac ofclass (+bot+))) rfalse;
	!XXX
	print "[SendTryAction req=", req, " by=", by, " ac=", ac, " n=", n, " s=", s, " stora=", stora, "]^";
];
-).

Chapter 2 - Receiving Actions

[XXX]

Part 3 - Server Communication

Chapter 1 - Initial Handshake

When play begins (this is the initial server handshake rule):
	describe the known actions;
	let N be 1;
	repeat with B running through bots:
		change the bot-ID of B to N;
		say "$addbot [N] [B][line break]";
		increase N by 1.

To describe the known actions: (- ShowKnownActions(); -).

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
-).

Guncho Bot Realms ends here.

---- DOCUMENTATION ----

N/A
