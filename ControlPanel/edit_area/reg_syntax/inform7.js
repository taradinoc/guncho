editAreaLoader.load_syntax["inform7"] = {
	'COMMENT_SINGLE' : { }
	,'COMMENT_MULTI' : { '[' : ']' }
	,'QUOTEMARKS' : [ '"' ]
	,'KEYWORD_CASE_SENSITIVE' : false
	,'KEYWORDS' : { }
	,'OPERATORS' : [ ]
	,'DELIMITERS' : [
		'(', ')', '{', '}'
	]
    ,'REGEXPS' : {
        'storytitle' : {
            'search' : '(^ )("[^"\r\n]*" by [^\r\n]*)([\r\n])'
            ,'class' : 'storytitle'
            ,'modifiers' : 'i'
            ,'execute' : 'before'
        }
        ,'sections' : {
            'search' : '([\r\n])((?:book|volume|part|chapter|section) +[^\r\n]*)([\r\n])'
            ,'class' : 'sections'
            ,'modifiers' : 'gi'
            ,'execute' : 'before'
        }
        ,'substs' : {
            'search' : '()(\\[[^\\]]*\\])()'
            ,'class' : 'substs'
            ,'modifiers' : 'g'
            ,'execute' : 'before'
        }
    }
	,'STYLES' : {
		'COMMENTS': 'color: #246E24;'
		,'QUOTESMARKS': 'color: #004D99;'
		,'KEYWORDS' : { }
		,'OPERATORS' : 'color: #FF0000;'
		//,'DELIMITERS' : 'color: #0000FF;'
        ,'REGEXPS' : {
            'storytitle' : 'font-weight: bold; color: black;'
            ,'sections' : 'font-weight: bold; color: black;'
            ,'quotesmarks .substs' : 'color: #3E9EFF;'
        }
	}
};
