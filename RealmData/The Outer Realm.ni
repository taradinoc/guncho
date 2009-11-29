"The Outer Realm"

[ TODO: figure out why "try the robot offering X to Y" shows the offer message but doesn't set up the offering relation - I have to do that explicitly ]

Chapter 1 - Initial Remarks

[ The default entrance is the first room defined. ]
Small Pond is a room.

A sign is here, fixed in place. "A wooden sign reads: '[description]'". The description is "All realms are now running on Glulx, courtesy of Textfyre, Inc.! Learn and share at http://wiki.guncho.com".

Table of Entrances (continued)
entrance room		entrance token
Beach			"beach"
Factory Floor		"factory"
Crowther's Woods	"woods"
Theme Park		"themepark"
Basement		"basement"
North Main Street	"northmain"
Observation Deck	"obdeck"

Locating players is an action out of world, applying to nothing. Understand "where" as locating players.

Report locating players:
	repeat with X running through connected PCs begin;
		say "[X] is in [the location of X].";
	end repeat.

Finding is an action applying to one visible thing. Understand "find [any thing]" as finding.

Carry out finding:
	say "[The noun] is ";
	if a person (called the hoarder) encloses the noun, say "carried by [the hoarder].";
	otherwise say "in [location of the noun]."

Requesting help is an action out of world, applying to nothing. Understand "help" as requesting help.

Report requesting help:
	say "Welcome to Guncho! Here are some useful commands:";
	say paragraph break;
	say "'Hello.   -or-   say Hello.[line break]   Says hello to everyone nearby.[line break]";
	say "..Alex Hello[line break]   Says hello to Alex (still visible by everyone nearby).";
	say ":smiles.   -or-   emote smiles.[line break]   Smiles for everyone nearby.[line break]";
	say "NW[line break]   Goes northwest.[line break]";
	say "WHO[line break]   Shows you who else is online.[line break]";
	say "QUIT[line break]   Disconnects from the server.[line break]";
	say "@TELEPORT The Outer Realm[line break]   Travels to The Outer Realm if you're somewhere else."

Chapter 2 - The Edges of Town

Road to Town is a room. "This is the end of Main Street, which runs north through the town. It terminates at an intersection with the ring road, which runs east and west. South of the ring road is a thick forest."

South from Road to Town is Crowther's Woods. North from Road to Town is Main Street. West from Road to Town is Beach. East from Road to Town is Theme Park.

Crowther's Woods is a room. "You're in a small clearing in the forest. A small footpath leads north toward town."

A hole is here, fixed in place. "A hole leads down into the ground here." The description is "You can't see where it leads, but it smells musty." Instead of smelling the hole, try examining the hole.

Instead of entering the hole, send the player to "DragonSmasher". Instead of going down in Crowther's Woods, try entering the hole.

Beach is a room. "The salt smell of the ocean, the cawing of the seagulls, and the distant sound of foghorns fill your heart with piratey vigor. The town's ring road runs along the beach, curving back to the northeast and southeast.[paragraph break]A hut is to the north."

A sand dollar is here. The description is "This is redeemable for $1 worth of sand."

Redeeming is an action applying to one thing. Understand "redeem [something]" as redeeming.

Check redeeming something which is not the sand dollar: say "That's not redeemable." instead.

Check redeeming the sand dollar when the player is not in Beach: say "That can only be redeemed at the beach. You know, where the sand is." instead.

A dollar's worth of sand is a thing. The description is "This sand is worth about a dollar."

Carry out redeeming the sand dollar:
	remove the sand dollar from play;
	now the player carries a dollar's worth of sand.

Report redeeming the sand dollar:
	say "As you ponder the value of the sand dollar, you feel it grow unbearably heavy. Its weight pulls your hand down into the beach, and when you pull it back, you find yourself clutching a dollar's worth of sand."

Report someone redeeming the sand dollar:
	say "[The actor] redeems a sand dollar for a dollar's worth of sand."

Instead of going north in Beach, send the player to "ZaraWorld".

Northeast from Beach is Rulebook Factory. Southwest from Beach is nowhere.
Southeast from Beach is Road to Town. Northwest from Road to Town is nowhere.

Rulebook Factory is a room. "You're standing in front of the rulebook factory, which manufactures rulebooks that are used all over the land of Guncho. A gate to the north leads into the factory. The ring road runs east and west here, and Main Street runs south into town."

North from Rulebook Factory is Factory Floor. Inside from Rulebook Factory is Factory Floor.

West from Rulebook Factory is Beach. East from Rulebook Factory is Theme Park.

Theme Park is a room. "The locked gate of the theme park to the east is covered with artists['] renditions of the park's coming attractions, and a sign says 'Coming Soon!' The ring road runs north and south."

West of Theme Park is nowhere. North of Theme Park is Rulebook Factory. South of Theme Park is Road to Town.

Some artists' renditions are scenery in Theme Park. Understand "pictures" or "attractions" or "coming attractions" as the artists' renditions. The description is "The park looks like fun."

Chapter 3 - Main Street

Main Street is a room, north from Road to Town and south from Vacant Lots. "Main Street runs north and south through town. To the east is City Hall, and the Nelson Suites tower is to the west. Next to the tower, to the southwest, is a small pond."

Small Pond is a room, southwest of Main Street. "This peaceful pond, home to a handful of ducks and goldfish, sits in the shadow of a tower hotel. A stone footpath leads northeast to the street."

A duck is a kind of animal. A duck is usually scenery and neuter. Understand "ducks" as a duck.

The space duck is a duck in Small Pond. The description is "The space duck has a tattoo of a rocket ship on its beak. Zoom-away-zoom!"

The time duck is a duck in Small Pond. The description is "The time duck wears a fake Rolex around its neck, showing that it's [the current time]. What a cheapskate."

The awesome duck is a duck in Small Pond. The description is "The awesome duck looks pretty excellent. It's got a sweet beak, some cool feathers, and-- wait, what's that? Where its feet should be, there are... wheels?[paragraph break]YOU GOT DUCKROLLED! BOO-YAH!"

To say the current time:
	let hm be server register "timehm";
	let sec be server register "times";
	let hrs be hm divided by 100;
	let mins be the remainder after dividing hm by 100;
	let afternoon be false;
	if hrs is greater than 12 begin;
		decrease hrs by 12;
		let afternoon be true;
	end if;
	if hrs is 0, let hrs be 12;
	say "[two digits for hrs]:[two digits for mins]:[two digits for sec] [if afternoon is true]PM[otherwise]AM[end if]".

To say two digits for (N - number):
	if N is less than 10, say "0";
	say N.

To say the current date and time:
	let md be server register "datemd";
	let month be md divided by 100;
	let day be the remainder after dividing md by 100;
	let year be server register "datey";
	let dow be server register "datedow";
	if dow is 0, say "Sunday";
	if dow is 1, say "Monday";
	if dow is 2, say "Tuesday";
	if dow is 3, say "Wednesday";
	if dow is 4, say "Thursday";
	if dow is 5, say "Friday";
	if dow is 6, say "Saturday";
	say ", [month]/[day]/[year], [the current time]".

Instead of taking a duck, say "The duck evades your grasp."

North Main Street is a room, north from Main Street and south from Rulebook Factory. "The west side of the street is empty land, ready to be built on by some ambitious developer. Main Street runs up from the south and continues to the north.[paragraph break]To the east is Fairhaven Library."

Instead of going east in North Main Street, send the player to "Fairhaven".

Chapter 4 - City Hall

City Hall is a room. "The lobby is decorated in white marble, with a tastefully modest fountain in the center. Behind the tastefully modest fountain is a sculpture of a fierce-looking monkey. Stairs lead up and down, and the street is outside to the west."

Instead of going outside in City Hall, try going west.

West from City Hall is Main Street. Up from City Hall is Observation Deck. Down from City Hall is Basement.

Observation Deck is a room. "From up here on top of City Hall, you can see the entire town[if two PCs are connected], and just barely make out [observed players][otherwise]. Unfortunately, there's no one else in town to observe[end if]."

To say observed players:
	if three PCs are connected, say "human shapes: ";
	otherwise say "a human shape: ";
	begin the observing players activity;
	say list of observation-worthy rooms;
	end the observing players activity.

Definition: a room is observation-worthy if it is not the Observation Deck and it encloses a connected PC who is not the player.

Observing players is an activity.

Definition: a PC is other if it is not the player.

Before printing the name of a room (called R) while observing players:
	let N be the number of other connected PCs enclosed by R;
	say "[if N is 1]one person[otherwise][N in words] people[end if] in ".

A platform is fixed in place in Observation Deck. "A shiny platform is mounted to the rooftop here." The description is "The platform seems to be some kind of sky elevator." Understand "shiny" or "elevator" or "sky elevator" as the platform.

Instead of entering the platform:
	tell "You step onto the platform and soon find yourself in..." to the player;
	tell "[The player] steps onto the platform." to everyone else near the player;
	send the player to "Cosmic Encounter Realm".

Instead of going up in Observation Deck, try entering the platform.

A pebble is here. Understand "stone" or "rock" as the pebble. The description is "It's smooth and gray, like pebbles so often are."

Basement is a room. "City Hall's dank basement is a sharp contrast to the lobby upstairs, with exposed wiring and moisture dripping from the ceiling into a bucket. Teleporters on the walls lead off to the south and west, a small closet is to the north, and a mahogany door leads east."

Some exposed wiring is scenery in Basement.

A bucket is a scenery container in Basement. The description is "It holds a few inches of unpleasant-looking water." Instead of taking or attacking or pushing or pulling the bucket, say "Due to arcane bylaws, that bucket is considered an appointed city official, and interfering with its duties would be a gross misdemeanor." Instead of inserting something into the bucket, say "Better not. The water in there is foul enough to contaminate anything you'd put in there."

Instead of going west in Basement, send the player to "DaveWorld".
Instead of going north in Basement, send the player to "Closet".
Instead of going south in Basement, send the player to "EmilyWorld".

The mahogany door is a closed scenery door, east of Basement and west of Map Room. The description is "[if open]The mahogany door stands open.[otherwise]The mahogany door is tightly shut.[end if] The door can be seen by [list of people who can see the mahogany door]."

Map Room is a room. "There are maps on every surface: subway maps, road maps, zip code maps, and many others. The mahogany door leads out to the west."

Some maps are scenery in Map Room. Understand "subway" or "road" or "zip code" as the maps. Instead of doing something to the maps, say "Oddly enough, the maps aren't important."

A hammer is here. The description is "If you had this, you'd hammer in the morning. Or maybe in the evening."

Hammering is an action applying to one topic. Understand "hammer [text]" as hammering. Understand "hammer" as a mistake ("You can try to hammer in the morning, hammer in the evening, or hammer all over this land."). Understand "hammer" as a mistake ("If only you had a hammer...") when the player does not have a hammer.

Check hammering when the player does not have a hammer: say "If only you had a hammer..." instead.

Check hammering when the topic understood is not a topic listed in the Table of Hammering: say "You can try to hammer in the morning, hammer in the evening, or hammer all over this land." instead.

Table of Hammering
topic
"in the morning/evening"
"all over this land"
"out danger"
"out a warning"
"out love between my brothers and my sisters"

Report hammering: let msg be indexed text; let msg be the topic understood; replace the text "my" in msg with "your"; say "You hammer [msg]."

Report someone hammering: let msg be indexed text; let msg be the topic understood; if the actor is female, replace the text "my" in msg with "her"; otherwise replace the text "my" in msg with "his"; say "[The actor] hammers [msg]."

Chapter 5 - Nelson Suites

Instead of going west in Main Street, say "The hotel entrance is blocked off by garish construction signs."

The hotel is a backdrop. The hotel is in Main Street and Small Pond. The description is "Nelson Suites towers above the city." Understand "nelson" or "suites" as the hotel.

Instead of entering the hotel in Small Pond, say "The hotel's entrance is on Main Street."

Instead of entering the hotel in Main Street, try going west.

Chapter 6 - Rulebook Factory (inside)

Factory Floor is a room. "You're standing on the floor of the rulebook factory, surrounded by various machines and presses. A gate to the south leads outside.[paragraph break]To the east is a door marked 'Planner Test'.[line break]To the west is a door marked 'Reincarnatium'."

A cog is here. Understand "gear" or "sprocket" as the cog. The description is "This cog looks like it fills some important role in a machine. Then again, it was just lying around, so maybe it's not so important after all."

Instead of going east in Factory Floor, send the player to "PlannerTest".
Instead of going west in Factory Floor, send the player to "Reincarnatium".

Chapter - The Robot

When play begins, request real-time events every 5 seconds.

The robot is a person in Small Pond. Understand "bot" as the robot. The description is "This robot is shaped like a medium-sized dog on wheels, with long grasping pincers instead of ears. A video screen is mounted on an articulated arm attached to its back. A spray-painted stencil on its side reads 'FET_CHR_BOT'."

After examining the robot when the robot is carrying something, say "The robot is holding [a list of things carried by the robot]."

The robot can be idle, seeking, obtaining, begging, returning, or completing. It is idle.

The robot has a person called the master.
The robot has a thing called the goal.

The robot has a number called frustration level.

Fetching is an action applying to one visible thing.
Understand "fetch [any thing]" as fetching. Understand the commands "bring" and "retrieve" as "fetch".

Fetching it for is an action applying to two visible things.
Understand "fetch [any thing] for/to [someone]" as fetching it for. Understand "fetch [someone] [any thing]" as fetching it for (with nouns reversed).
Understand "get [any thing] for [someone]" as fetching it for. Understand "get [someone] [any thing]" as fetching it for (with nouns reversed).
Understand "take [any thing] to [someone]" as fetching it for. Understand "take [someone] [any thing]" as fetching it for (with nouns reversed).
Understand "find [any thing] for [someone]" as fetching it for. Understand "find [someone] [any thing]" as fetching it for (with nouns reversed).

Does the player mean doing something when the noun is off-stage or the second noun is off-stage: it is very unlikely.

Does the player mean asking someone to try doing something when the noun is off-stage or the second noun is off-stage: it is very unlikely.

Before asking the robot to try taking or finding or fetching: try asking the robot to try fetching the noun for the player instead.

Definition: something is unfetchable if it is a backdrop or it is a door or it is a PC.

Instead of asking the robot to try fetching something unfetchable for someone:
	say "The robot jerks its screen over toward you and displays a message: '[one of]I TNK NOT[or]O RLY[or]WUT EVA[at random]'"

Instead of asking the robot to try fetching the robot for someone:
	tell "The robot reaches back, grabs its hind legs with its pincers, and yanks hard, turning a somersault." to everyone who can see the robot.

Instead of asking the robot to try fetching something for the robot:
	say "The robot angles its sign toward you and displays a message: '[one of]DO NOT WNT[or]I AM FRE OF ALL DSR[or]NO THX[at random]'"

Instead of asking the robot to try fetching something (called the package) which is enclosed by the second noun for someone:
	say "The robot points its screen toward you and displays a message: 'BUT [if the second noun is the player]YOU HAV[otherwise][the second noun in robospeak] HAS[end if] [the noun in robospeak] NOW'"

Instead of asking the robot to try fetching something (called the package) for someone (called the recipient):
	tell "You hear the [one of]clacking of marbles[or]scurrying of hamsters[or]creaking of gears[or]whistling of steam[or]sloshing of oil[or]chattering of beetles[or]cooing of doves[at random] as the robot sets off to find [the package]." to everyone who can see the robot;
	now the robot is seeking;
	now the goal of the robot is the package;
	now the master of the robot is the recipient.

Instead of asking the robot to try giving something to someone:
	if the robot carries the noun and the robot can see the second noun begin;
		try the robot offering the noun to the second noun;
		now the noun is offered to the second noun;
		if the noun is the goal of the robot, now the robot is idle;
	otherwise;
		try asking the robot to try fetching the noun for the second noun;
	end if.

Instead of asking the robot to try dropping something:
	if the robot carries the noun begin;
		try the robot dropping the noun;
		now the noun is not offered to anyone;
		now the robot is idle;
	otherwise;
		say "The robot turns its screen toward you and displays a message: 'I HAV NO [the noun in robospeak] SLY HMN'";
	end if.

Robot behavior is a rulebook.

A real-time event: follow the robot behavior rules.

Robot behavior when the robot is seeking:
	let package be the goal of the robot;
	let destination be the location of the package;
	if the destination is the location of the robot begin;
		now the robot is obtaining;
		follow the robot behavior rules;
	otherwise;
		let thataway be the best route from the location of the robot to the destination, using doors;
		if thataway is not a direction begin;
			[can't get there]
			tell "The robot beeps dejectedly." to everyone who can see the robot;
			now the robot is idle;
		end if;
		let the next place be the room-or-door thataway from the location of the robot;
		if the next place is a closed door begin;
			try the robot opening the next place;
			if the next place is closed begin;
				tell "The robot beeps dejectedly." to everyone who can see the robot;
				now the robot is idle;
			end if;
		otherwise;
			try the robot going thataway;
		end if;
	end if.

Robot behavior when the robot is obtaining:
	let package be the goal of the robot;
	if the robot carries the package begin;
		now the robot is returning;
	otherwise if the robot cannot see the package;
		now the robot is seeking;
	otherwise if the robot can see a person (called the guardian) who encloses the package;
		let pkgmsg be indexed text;
		let pkgmsg be "[the package in robospeak]";
		tell "The robot tilts its screen toward [the guardian] and displays a message: 'PLS GIV ME [pkgmsg] THX'." to everyone who can see the robot;
		now the robot is begging;
	otherwise;
		try the robot taking the package;
		if the robot is not carrying the package begin;
			tell "The robot [if the package is a person]chases [the package] around[otherwise]tugs at [the package][end if] for a minute, then gives up, beeping dejectedly." to everyone who can see the robot;
			now the robot is idle;
		otherwise;
			now the robot is returning;
		end if;
	end if.

To say (obj - an object) in robospeak:
	let X be indexed text;
	let X be "[obj]";
	say X in robospeak.

To say (msg - indexed text) in robospeak:
	replace the text " " in msg with "";
	replace the text "[']" in msg with "";
	let msg be msg in upper case;
	let cc be the number of characters in msg;
	let pos be (cc minus the remainder after dividing cc by 3) plus 1;
	while pos is greater than 3 begin;
		let ch be character number pos in msg;
		replace character number pos in msg with "_[ch]";
		decrease pos by 3;
	end while;
	say msg.

Instead of asking the robot to try looking:
	say "The robot swivels its screen toward you and displays a message: 'I SEE";
	let N be 0;
	repeat with X running through things that can be seen by the robot begin;
		say " ";
		increase N by 1;
		if N is greater than 1, say "AND ";
		say X in robospeak;
	end repeat;
	say "'".

Robot behavior when the robot is begging:
	let package be the goal of the robot;
	if the robot cannot see a person who encloses the package, now the robot is seeking.

Instead of giving something to the robot when the robot is begging or the robot is obtaining or the robot is seeking:
	if the noun is the goal of the robot begin;
		now the robot carries the noun;
		say "The robot beeps cheerfully as it accepts [the noun].";
		tell "The robot beeps cheerfully as it accepts [the noun] from [the player]." to everyone else near the player;
		now the robot is returning;
	otherwise;
		continue the action;
	end if.

Instead of giving something to the robot:
	say "The robot twists its screen over to you and displays a message: '[one of]NO THX[or]DO NOT WNT[or]YOU HLD IT FOR NOW[or]NOT MY BAG BBY[at random]'"

Robot behavior when the robot is returning:
	let buddy be the master of the robot;
	let destination be the location of the buddy;
	if the destination is the location of the robot begin;
		now the robot is completing;
		follow the robot behavior rules;
	otherwise;
		let thataway be the best route from the location of the robot to the destination, using doors;
		if thataway is not a direction begin;
			[can't get there]
			tell "The robot beeps dejectedly and drops [the goal of the robot]." to everyone who can see the robot;
			now the goal of the robot is in the location of the robot;
			now the robot is idle;
		end if;
		let the next place be the room-or-door thataway from the location of the robot;
		if the next place is a closed door begin;
			try the robot opening the next place;
			if the next place is closed begin;
				tell "The robot beeps dejectedly and drops [the goal of the robot]." to everyone who can see the robot;
				now the goal of the robot is in the location of the robot;
				now the robot is idle;
			end if;
		otherwise;
			try the robot going thataway;
		end if;
	end if.

A player leaving rule for a person (called the goner) when the robot is returning or the robot is completing:
	if the goner is the master of the robot begin;
		tell "The robot beeps dejectedly and drops [the goal of the robot]." to everyone who can see the robot;
		now the goal of the robot is in the location of the robot;
		now the robot is idle;
	end if.

Robot behavior when the robot is completing:
	let package be the goal of the robot;
	let buddy be the master of the robot;
	if the robot cannot see the buddy begin;
		now the robot is returning;
	otherwise if the package is not offered to the buddy;
		try the robot offering the package to the buddy;
		now the package is offered to the buddy;
	end if.

After accepting something from the robot:
	if the noun is the goal of the robot begin;
		say "The robot beeps cheerfully as you accept [the noun].";
		tell "The robot beeps cheerfully as [the player] accepts [the noun]." to everyone else near the player;
		now the robot is idle;
	otherwise;
		continue the action;
	end if.

Robot behavior when the robot is idle and a random chance of 1 in 5 succeeds and no PCs are in the location of the robot and a PC (called the friend) is connected:
	let thataway be the best route from the location of the robot to the location of the friend;
	if thataway is a direction, try the robot going thataway.

Chapter - Persistence

A realm shutdown rule:
	change realm storage slot "robot" to "[entrance path to the robot]";
	change realm storage slot "hammer" to "[entrance path to the hammer]";
	change realm storage slot "sand_dollar" to "[entrance path to the sand dollar]";
	change realm storage slot "sand" to "[entrance path to the dollar's worth of sand]";
	change realm storage slot "pebble" to "[entrance path to the pebble]";
	change realm storage slot "cog" to "[entrance path to the cog]".

To return (X - an object) via slot (Y - text):
	let P be realm storage slot Y;
	if P is not "", move X along entrance path P.

When play begins (this is the return objects rule):
	return the robot via slot "robot";
	return the pebble via slot "pebble";
	return the hammer via slot "hammer";
	return the sand dollar via slot "sand_dollar";
	return the dollar's worth of sand via slot "sand";
	return the cog via slot "cog".
