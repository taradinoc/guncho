var EditArea_i7headings= {
	/**
	 * Get called once this file is loaded (editArea still not initialized)
	 *
	 * @return nothing	 
	 */	 	 	
	init: function(){	
		editArea.load_css(this.baseURL+"css/i7headings.css");
		//editArea.load_script(this.baseURL+"langs/en.js");
        this.updateTimerId = false;
	}
	/**
	 * Returns the HTML code for a specific control string or false if this plugin doesn't have that control.
	 * A control can be a button, select list or any other HTML item to present in the EditArea user interface.
	 * Language variables such as {$lang_somekey} will also be replaced with contents from
	 * the language packs.
	 * 
	 * @param {string} ctrl_name: the name of the control to add	  
	 * @return HTML code for a specific control or false.
	 * @type string	or boolean
	 */	
	,get_control_html: function(ctrl_name){
		switch(ctrl_name){
			//case "test_but":
				// Control id, button img, command
				//return parent.editAreaLoader.get_button_html('test_but', 'test.gif', 'test_cmd', false, this.baseURL);
			case "i7headings":
                var title = editArea.get_translation("i7headings_title", "word");
				html= "<select id='heading_select' onchange='javascript:editArea.execCommand(\"heading_select_change\")' title='" + title + "'></select>";
				return html;
		}
		return false;
	}
	/**
	 * Get called once EditArea is fully loaded and initialised
	 *	 
	 * @return nothing
	 */	 	 	
	,onload: function(){ 
		editArea.execCommand("heading_update");
	}
	
	/**
	 * Is called each time the user touch a keyboard key.
	 *	 
	 * @param (event) e: the keydown event
	 * @return true - pass to next handler in chain, false - stop chain execution
	 * @type boolean	 
	 */
	,onkeydown: function(e){
        if (this.updateTimerId != false)
            clearTimeout(this.updateTimerId);
        this.updateTimerId = setTimeout("editArea.execCommand('heading_update')", 1000);
        return true;
	}
	
	/**
	 * Executes a specific command, this function handles plugin commands.
	 *
	 * @param {string} cmd: the name of the command being executed
	 * @param {unknown} param: the parameter of the command	 
	 * @return true - pass to next handler in chain, false - stop chain execution
	 * @type boolean	
	 */
	,execCommand: function(cmd, param){
		// Handle commands
		switch(cmd){
			case "heading_select_change":
				var val= document.getElementById("heading_select").value;
				if(val!=-1)
					editArea.execCommand("go_to_line", val);
				document.getElementById("heading_select").options[0].selected=true;
				return false;
			case "heading_update":
				var select = document.getElementById("heading_select");
                select.options.length = 0;
                select.options[0] = new Option(editArea.get_translation("i7headings_first", "word"), -1);
                var text = parent.editAreaLoader.getValue(editArea.id);
                var lines = text.split("\n");
                for (var i=0; i<lines.length; i++)
                    if (lines[i].match(/^(volume|book|part|chapter|section) /i))
                        select.options[select.options.length] = new Option(lines[i], i+1);
				return false;
		}
		// Pass to next handler in chain
		return true;
	}
};

// Adds the plugin class to the list of available EditArea plugins
editArea.add_plugin("i7headings", EditArea_i7headings);
