Version 1/080522 of Guncho FyreVM Support (for Glulx only) by Guncho Cabal begins here.

[This has been modified from the original FyreVM Support to:
    * Remove the Glk support, because we know exactly which interpreter we're using
    * Remove the channel phrases, which we don't want realm authors using
    * Turn off output filtering]

Use authorial modesty.

Chapter 1 - FyreVM-specific constants and definitions

[FyreVM defines a new opcode to handle the things that would otherwise be handled by @glk. These definitions allow us to use that opcode.]

Include (-
! FY_READLINE: Takes a buffer address and size and reads a line of input
! from the player. The line length is written into the word at the start
! of the buffer, and the characters are written after (starting at offset 4).
! Writes a length of 0 if the read failed.
Constant FY_READLINE = 1;
! FY_SETSTYLE: Activates the selected text style. Bold and italic may be
! combined by setting them one after the other; roman will turn both off.
! Fixed and variable are opposites.
Constant FY_SETSTYLE = 2;
! FY_TOLOWER/FY_TOUPPER: Converts a character to lower or upper case, based
! on whichever encoding is used for the dictionary and input buffer.
Constant FY_TOLOWER = 3;
Constant FY_TOUPPER = 4;
! FY_CHANNEL: Selects an output channel.
Constant FY_CHANNEL = 5;
! FY_XMLFILTER: Turns the main channel's XML filter on (1) or off (0).
Constant FY_XMLFILTER = 6;
! FY_READKEY: Reads a single character of input, e.g. for pausing the game.
! Returns the Unicode character, or 0 if the read failed.
Constant FY_READKEY = 7;
! FY_SETVENEER: Registers a routine address or constant value with the
! interpreter's veneer acceleration system.
Constant FY_SETVENEER = 8;

! Text styles for FY_SETSTYLE.
Constant FYS_ROMAN = 1;
Constant FYS_BOLD = 2;
Constant FYS_ITALIC = 3;
Constant FYS_FIXED = 4;
Constant FYS_VARIABLE = 5;

! Channels for FY_CHANNEL.
Constant FYC_MAIN = 1;
Constant FYC_LOCATION = 2; ! room name
Constant FYC_SCORE = 3;
Constant FYC_TIME = 4;
Constant FYC_HINTS = 5;
Constant FYC_HELP = 6;
Constant FYC_MAP = 7;
Constant FYC_PROGRESS = 8;
Constant FYC_THEME = 9;
Constant FYC_PROMPT = 10;
Constant FYC_CONVERSATION = 11;
Constant FYC_SOUND = 12;

! Slots for FY_SETVENEER.
Constant FYV_Z__Region = 1;
Constant FYV_CP__Tab = 2;
Constant FYV_OC__Cl = 3;
Constant FYV_RA__Pr = 4;
Constant FYV_RT__ChLDW = 5;
Constant FYV_Unsigned__Compare = 6;
Constant FYV_RL__Pr = 7;
Constant FYV_RV__Pr = 8;
Constant FYV_OP__Pr = 9;
Constant FYV_RT__ChSTW = 10;
Constant FYV_RT__ChLDB = 11;
Constant FYV_Meta__class = 12;

Constant FYV_String = 1001;
Constant FYV_Routine = 1002;
Constant FYV_Class = 1003;
Constant FYV_Object = 1004;
Constant FYV_RT__Err = 1005;
Constant FYV_NUM_ATTR_BYTES = 1006;
Constant FYV_classes_table = 1007;
Constant FYV_INDIV_PROP_START = 1008;
Constant FYV_cpv__start = 1009;
Constant FYV_ofclass_err = 1010;
Constant FYV_readprop_err = 1011;

[ FyreCall a b c res; @"S4:4096" a b c res; return res; ];
-).

[These activate FyreVM's veneer optimizations.]

Include (-
[ REGISTER_VENEER_R;
    FyreCall(FY_SETVENEER, FYV_Z__Region, Z__Region);
    FyreCall(FY_SETVENEER, FYV_CP__Tab, CP__Tab);
    FyreCall(FY_SETVENEER, FYV_OC__Cl, OC__Cl);
    FyreCall(FY_SETVENEER, FYV_RA__Pr, RA__Pr);
    FyreCall(FY_SETVENEER, FYV_Unsigned__Compare, Unsigned__Compare);
    FyreCall(FY_SETVENEER, FYV_RL__Pr, RL__Pr);
    FyreCall(FY_SETVENEER, FYV_RV__Pr, RV__Pr);
    FyreCall(FY_SETVENEER, FYV_OP__Pr, OP__Pr);
    FyreCall(FY_SETVENEER, FYV_Meta__class, Meta__class);

#ifdef STRICT_MODE;
    FyreCall(FY_SETVENEER, FYV_RT__ChLDW, RT__ChLDW);
    FyreCall(FY_SETVENEER, FYV_RT__ChSTW, RT__ChSTW);
    FyreCall(FY_SETVENEER, FYV_RT__ChLDB, RT__ChLDB);
#endif;

    FyreCall(FY_SETVENEER, FYV_String, String);
    FyreCall(FY_SETVENEER, FYV_Routine, Routine);
    FyreCall(FY_SETVENEER, FYV_Class, Class);
    FyreCall(FY_SETVENEER, FYV_Object, Object);
    FyreCall(FY_SETVENEER, FYV_RT__Err, RT__Err);
    FyreCall(FY_SETVENEER, FYV_NUM_ATTR_BYTES, NUM_ATTR_BYTES);
    FyreCall(FY_SETVENEER, FYV_classes_table, #classes_table);
    FyreCall(FY_SETVENEER, FYV_INDIV_PROP_START, INDIV_PROP_START);
    FyreCall(FY_SETVENEER, FYV_cpv__start, #cpv__start);
    FyreCall(FY_SETVENEER, FYV_ofclass_err, "apply 'ofclass' for");
    FyreCall(FY_SETVENEER, FYV_readprop_Err, "read");
];
-).

The register veneer routines rule translates into I6 as "REGISTER_VENEER_R".

After starting the virtual machine: follow the register veneer routines rule.

[And these set up an alternative way to print text into an array, since Inform's default way of doing that requires Glk.]

Include (-
Global output_buffer_address;
Global output_buffer_size;
Global output_buffer_pos;
Global output_buffer_uni;

Constant MAX_OUTPUT_NESTING = 32;
Array output_buffer_stack --> (MAX_OUTPUT_NESTING * 4);
Global output_buffer_sp = 0;

[ OpenOutputBufferUnicode buffer size;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_address;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_size;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_pos;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_uni;

    output_buffer_address = buffer;
    output_buffer_size = size;
    output_buffer_pos = 0;
    output_buffer_uni = 1;
    @setiosys 1 _OutputBufferProcUni;
];

[ OpenOutputBuffer buffer size;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_address;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_size;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_pos;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_uni;

    output_buffer_address = buffer;
    output_buffer_size = size;
    output_buffer_pos = 0;
    output_buffer_uni = 0;
    @setiosys 1 _OutputBufferProc;
];

[ CloseOutputBuffer results  rv;
    if (results) {
        results-->0 = 0;
        results-->1 = output_buffer_pos;
    }
    rv = output_buffer_pos;
    ResumeOutputBuffer();
    return rv;
];

[ SuspendOutputBuffer;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_address;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_size;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_pos;
    output_buffer_stack-->(output_buffer_sp++) = output_buffer_uni;

    @setiosys 20 0;
];

[ ResumeOutputBuffer;
    output_buffer_uni = output_buffer_stack-->(--output_buffer_sp);
    output_buffer_pos = output_buffer_stack-->(--output_buffer_sp);
    output_buffer_size = output_buffer_stack-->(--output_buffer_sp);
    output_buffer_address = output_buffer_stack-->(--output_buffer_sp);

    if (output_buffer_sp > 0) {
        if (output_buffer_uni)
            @setiosys 1 _OutputBufferProcUni;
        else
            @setiosys 1 _OutputBufferProc;
    } else
        @setiosys 20 0;
];
[ _OutputBufferProcUni ch;
    if (output_buffer_pos < output_buffer_size)
        output_buffer_address-->output_buffer_pos = ch;
    output_buffer_pos++;
];

[ _OutputBufferProc ch;
    if (output_buffer_pos < output_buffer_size)
        output_buffer_address->output_buffer_pos = ch;
    output_buffer_pos++;
];
-).

Chapter 2 - Template replacements

Section 1 - Glulx segment

Include (-
[ VM_Initialise res;
    unicode_gestalt_ok = true;
    @gestalt 4 20 res; ! Test if this interpreter has FyreVM channels
    if (res == 0) quit;

    @setiosys 20 0;
    FyreCall(FY_XMLFILTER, 0);
    return;
];

[ GGRecoverObjects id;
    ! If GGRecoverObjects() has been called, all these stored IDs are
    ! invalid, so we start by clearing them all out.
    ! (In fact, after a restoreundo, some of them may still be good.
    ! For simplicity, though, we assume the general case.)
    gg_mainwin = 0;
    gg_statuswin = 0;
    gg_quotewin = 0;
    gg_scriptfref = 0;
    gg_scriptstr = 0;
    gg_savestr = 0;
    statuswin_cursize = 0;
    #Ifdef DEBUG;
    gg_commandstr = 0;
    gg_command_reading = false;
    #Endif; ! DEBUG
    ! Also tell the game to clear its object references.
    IdentifyGlkObject(0);
];
-) instead of "Starting Up" in "Glulx.i6t".

Include (-
[ VM_KeyChar win nostat;
    return FyreCall(FY_READKEY);
];

[ VM_KeyDelay tenths;
    rfalse; ! FyreVM doesn't support timed input
];

[ VM_ReadKeyboard  a_buffer a_table done ix;
    FyreCall(FY_READLINE, a_buffer, INPUT_BUFFER_LEN);
    VM_Tokenise(a_buffer,a_table);
    #ifdef ECHO_COMMANDS;
    print "** ";
    for (ix=WORDSIZE: ix<(a_buffer-->0)+WORDSIZE: ix++) print (char) a_buffer->ix;
    print "^";
    #endif; ! ECHO_COMMANDS
];
-) instead of "Keyboard Input" in "Glulx.i6t".

Include (-
[ VM_Picture resource_ID;
	print "[Picture number ", resource_ID, " here.]^";
];

[ VM_SoundEffect resource_ID;
	print "[Sound effect number ", resource_ID, " here.]^";
];
-) instead of "Audiovisual Resources" in "Glulx.i6t".

Include (-
[ VM_Style sty;
    switch (sty) {
        NORMAL_VMSTY:
            FyreCall(FY_SETSTYLE, FYS_ROMAN);
        HEADER_VMSTY, SUBHEADER_VMSTY, ALERT_VMSTY:
            FyreCall(FY_SETSTYLE, FYS_BOLD);
    }
];
-) instead of "Typography" in "Glulx.i6t".

Include (-
[ VM_UpperToLowerCase c;
	return FyreCall(FY_TOLOWER, c);
];
[ VM_LowerToUpperCase c;
	return FyreCall(FY_TOUPPER, c);
];
-) instead of "Character Casing" in "Glulx.i6t".

Include (-
! Glulx_PrintAnything()                    <nothing printed>
! Glulx_PrintAnything(0)                   <nothing printed>
! Glulx_PrintAnything("string");           print (string) "string";
! Glulx_PrintAnything('word')              print (address) 'word';
! Glulx_PrintAnything(obj)                 print (name) obj;
! Glulx_PrintAnything(obj, prop)           obj.prop();
! Glulx_PrintAnything(obj, prop, args...)  obj.prop(args...);
! Glulx_PrintAnything(func)                func();
! Glulx_PrintAnything(func, args...)       func(args...);

[ Glulx_PrintAnything _vararg_count obj mclass;
    if (_vararg_count == 0) return;
    @copy sp obj;
    _vararg_count--;
    if (obj == 0) return;

    if (obj->0 == $60) {
        ! Dictionary word. Metaclass() can't catch this case, so we do it manually
        print (address) obj;
        return;
    }

    mclass = metaclass(obj);
    switch (mclass) {
      nothing:
        return;
      String:
        print (string) obj;
        return;
      Routine:
        ! Call the function with all the arguments which are already
        ! on the stack.
        @call obj _vararg_count 0;
        return;
      Object:
        if (_vararg_count == 0) {
            print (name) obj;
        }
        else {
            ! Push the object back onto the stack, and call the
            ! veneer routine that handles obj.prop() calls.
            @copy obj sp;
            _vararg_count++;
            @call CA__Pr _vararg_count 0;
        }
        return;
    }
];

[ Glulx_PrintAnyToArray _vararg_count arr arrlen str oldstr len;
    @copy sp arr;
    @copy sp arrlen;
    _vararg_count = _vararg_count - 2;

    OpenOutputBuffer(arr, arrlen);

    @call Glulx_PrintAnything _vararg_count 0;

    len = CloseOutputBuffer(0);
    return len;
];

Constant GG_ANYTOSTRING_LEN 66;
Array AnyToStrArr -> GG_ANYTOSTRING_LEN+1;

[ Glulx_ChangeAnyToCString _vararg_count ix len;
    ix = GG_ANYTOSTRING_LEN-2;
    @copy ix sp;
    ix = AnyToStrArr+1;
    @copy ix sp;
    ix = _vararg_count+2;
    @call Glulx_PrintAnyToArray ix len;
    AnyToStrArr->0 = $E0;
    if (len >= GG_ANYTOSTRING_LEN)
        len = GG_ANYTOSTRING_LEN-1;
    AnyToStrArr->(len+1) = 0;
    return AnyToStrArr;
];
-) instead of "Glulx-Only Printing Routines" in "Glulx.i6t".

Include (-
[ VM_ClearScreen window;
    return; ! not supported
];

[ VM_ScreenWidth  id;
    return 80; ! not supported
];

[ VM_ScreenHeight;
    return 25; ! not supported
];
-) instead of "The Screen" in "Glulx.i6t".

Include (-
[ VM_SetWindowColours f b window doclear;
    return; ! not supported
];

[ VM_RestoreWindowColours; ! used after UNDO: compare I6 patch L61007
    return; ! not supported
];

[ MakeColourWord c;
    if (c > 9) return c;
    c = c-2;
    return $ff0000*(c&1) + $ff00*(c&2 ~= 0) + $ff*(c&4 ~= 0);
];
-) instead of "Window Colours" in "Glulx.i6t".

Include (-
[ VM_MainWindow;
    return; ! not supported
];
-) instead of "Main Window" in "Glulx.i6t".

Include (-
[ VM_StatusLineHeight hgt;
    return; ! not supported
];

[ VM_MoveCursorInStatusLine line column;
    return; ! not supported
];
-) instead of "Status Line" in "Glulx.i6t".

Include (-
[ Box__Routine maxwid arr ix lines lastnl parwin;
    return; ! not supported
];
-) instead of "Quotation Boxes" in "Glulx.i6t".

Include (-
#Ifdef DEBUG;
[ GlkListSub id val;
    print "Glk is not used with this interpreter.^";
    return;
];

Verb meta 'glklist'
    *                                           -> Glklist;
#Endif;
-) instead of "GlkList Command" in "Glulx.i6t".

Include (-
[ RESTORE_THE_GAME_R res fref;
	if (actor ~= player) rfalse;
	@restore 0 res;
	.RFailed;
	GL__M(##Restore, 1);
];
-) instead of "Restore The Game Rule" in "Glulx.i6t".

Include (-
[ SAVE_THE_GAME_R res fref;
	if (actor ~= player) rfalse;
    @save 0 res;
    if (res == -1) {
        ! The player actually just typed "restore". We're going to print
        !  GL__M(##Restore,2); the Z-Code Inform library does this correctly
        ! now. But first, we have to recover all the Glk objects; the values
        ! in our global variables are all wrong.
        GGRecoverObjects();
        return GL__M(##Restore, 2);
    }
	if (res == 0) return GL__M(##Save, 2);
	.SFailed;
	GL__M(##Save, 1);
];
-) instead of "Save The Game Rule" in "Glulx.i6t".

Include (-
[ SWITCH_TRANSCRIPT_ON_R;
	if (actor ~= player) rfalse;
    print "Transcripting is not available with this interpreter.^";
    return;
];
-) instead of "Switch Transcript On Rule" in "Glulx.i6t".

Include (-
[ SWITCH_TRANSCRIPT_OFF_R;
	if (actor ~= player) rfalse;
    print "Transcripting is not available with this interpreter.^";
    return;
];
-) instead of "Switch Transcript Off Rule" in "Glulx.i6t".

Section 2 - Printing segment

Include (-
[ PrintPrompt i;
    FyreCall(FY_CHANNEL, FYC_PROMPT);
    PrintText( (+ command prompt +) );
    FyreCall(FY_CHANNEL, FYC_MAIN);
	ClearBoxedText();
	ClearParagraphing();
	enable_rte = true;
];
-) instead of "Prompt" in "Printing.i6t".

Include (-
#Ifndef DrawStatusLine;
[ DrawStatusLine;
    ! do nothing
];
#Endif;
-) instead of "Status Line" in "Printing.i6t".

Section 3 - IndexedText segment

Include (-
#ifndef IT_MemoryBufferSize;
Constant IT_MemoryBufferSize = 512;
#endif;

Constant IT_Memory_NoBuffers = 2;

#ifndef IT_Memory_NoBuffers;
Constant IT_Memory_NoBuffers = 1;
#endif;

#ifdef TARGET_ZCODE;
Array IT_MemoryBuffer -> IT_MemoryBufferSize*IT_Memory_NoBuffers; ! Where characters are bytes
#ifnot;
Array IT_MemoryBuffer --> (IT_MemoryBufferSize+2)*IT_Memory_NoBuffers; ! Where characters are words
#endif;

Global RawBufferAddress = IT_MemoryBuffer;
Global RawBufferSize = IT_MemoryBufferSize;

Global IT_cast_nesting;

[ INDEXED_TEXT_TY_Cast tx fromkov indt
	len i offs realloc news buff buffx freebuff results;
	#ifdef TARGET_ZCODE;
	buffx = IT_MemoryBufferSize;
	#ifnot;
	buffx = (IT_MemoryBufferSize + 2)*WORDSIZE;
	#endif;
	
	buff = RawBufferAddress + IT_cast_nesting*buffx;
	IT_cast_nesting++;
	if (IT_cast_nesting > IT_Memory_NoBuffers) {
		buff = VM_AllocateMemory(buffx); freebuff = buff;
		if (buff == 0) {
			BlkAllocationError("ran out with too many simultaneous indexed text conversions");
			return;
		}
	}

	.RetryWithLargerBuffer;
	if (tx == 0) {
		#ifdef TARGET_ZCODE;
		buff-->0 = 1;
		buff->2 = 0;
		#ifnot;
		buff-->0 = 0;
		#endif;
		len = 1;
	} else {
		#ifdef TARGET_ZCODE;
		@output_stream 3 buff;
		#ifnot;
		if (unicode_gestalt_ok == false) { RunTimeProblem(RTP_NOGLULXUNICODE); jump Failed; }
		OpenOutputBufferUnicode(buff, RawBufferSize);
		#endif;

		@push say__p; @push say__pc;
		ClearParagraphing();
		if (fromkov == SNIPPET_TY) print (PrintSnippet) tx;
		else {
			if (tx ofclass String) print (string) tx;
			if (tx ofclass Routine) (tx)();	
		}
		@pull say__pc; @pull say__p;

		#ifdef TARGET_ZCODE;

		@output_stream -3;
		len = buff-->0;
		if (len > RawBufferSize-1) len = RawBufferSize-1;
		offs = 2;
		buff->(len+2) = 0;

		#ifnot; ! i.e. GLULX
		
		results = buff + buffx - 2*WORDSIZE;
		CloseOutputBuffer(results);
		len = results-->1;
		if (len > RawBufferSize-1) {
			! Glulx had to truncate text output because the buffer ran out:
			! len is the number of characters which it tried to print
			news = RawBufferSize;
			while (news < len) news=news*2;
			news = news*4; ! Bytes rather than words
			i = VM_AllocateMemory(news);
			if (i ~= 0) {
				if (freebuff) VM_FreeMemory(freebuff);
				freebuff = i;
				buff = i;
				RawBufferSize = news/4;
				jump RetryWithLargerBuffer;
			}
			! Memory allocation refused: all we can do is to truncate the text
			len = RawBufferSize-1;
		}
		offs = 0;
		buff-->(len) = 0;

		#endif;

		len++;
	}

	IT_cast_nesting--;

	if (indt == 0) {
		indt = BlkAllocate(len+1, INDEXED_TEXT_TY, IT_Storage_Flags);
		if (indt == 0) jump Failed;
	} else {
		if (BlkValueSetExtent(indt, len+1, 1) == false) { indt = 0; jump Failed; }
	}

	#ifdef TARGET_ZCODE;
	for (i=0:i<=len:i++) BlkValueWrite(indt, i, buff->(i+offs));
	#ifnot;
	for (i=0:i<=len:i++) BlkValueWrite(indt, i, buff-->(i+offs));
	#endif;

	.Failed;
	if (freebuff) VM_FreeMemory(freebuff);

	return indt;
];
-) instead of "Casting" in "IndexedText.i6t".

Include (-
[ INDEXED_TEXT_TY_Say indt  ch i dsize;
	if ((indt==0) || (BlkType(indt) ~= INDEXED_TEXT_TY)) return;
	dsize = BlkValueExtent(indt);
	for (i=0:i<dsize:i++) {
		ch = BlkValueRead(indt, i);
		if (ch == 0) break;
		#ifdef TARGET_ZCODE;
		print (char) ch;
		#ifnot; ! TARGET_ZCODE
		@streamunichar ch;
		#endif;
	}
];
-) instead of "Printing" in "IndexedText.i6t".

Chapter 3 - Standard Rules replacements

To say bold type -- running on
	(documented at ph_types):
	(- FyreCall(FY_SETSTYLE, FYS_BOLD); -).
To say italic type -- running on:
	(- FyreCall(FY_SETSTYLE, FYS_ITALIC); -).
To say roman type -- running on:
	(- FyreCall(FY_SETSTYLE, FYS_ROMAN); -).
To say fixed letter spacing -- running on:
	(- FyreCall(FY_SETSTYLE, FYS_FIXED); -).
To say variable letter spacing -- running on:
	(- FyreCall(FY_SETSTYLE, FYS_VARIABLE); -).

Guncho FyreVM Support ends here.

---- DOCUMENTATION ----

For information on FyreVM, please visit "http://www.textfyre.com/FyreVM".
