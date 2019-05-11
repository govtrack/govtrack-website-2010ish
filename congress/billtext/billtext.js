$.btns = {}; // global variable namespace
$.btns.caching = true; // whether to save html when switching views
$.btns.large_cutoff = 1048576; // file size above which we warn the user before continuing
$.btns.optimize_cutoff = 524288; // file size above which we optimize for performance. specifically,
                                  // we turn off save and restore state, and we skip animations.
// assumes $.btns.versions and $.btns.warning_message are set externally

$.btns.on_change_version = function() { 
    var $version = $("#version", $.btns.sidebar);
    var $compareto = $("#compareto", $.btns.sidebar);
    var $view = $("#view", $.btns.sidebar);
    
    var vinfo = $.btns.versions[$version.val()]
    
    // create #compareto
    $("option:gt(0)", $compareto).remove();
    if (vinfo.format === "html") {
        $("option:selected", $version).nextAll().clone().appendTo($compareto); // add all options after selected
    }      
    
    // if compareto in query string, set #compareto to that value.
    var $initcomp = $("option[value='" + $.btns.init_compareto + "']", $compareto); 
    if ($.btns.isfirst && $initcomp.length) {
        $compareto.val($.btns.init_compareto);
    } else {
        $compareto.val("none");
    }
    
    // happens in opera when going back to previously opened page.
    if ($("input:checked", $view).length === 0) {
        $("input", $view).val(["side"]);
    }

    // create links
    $("a#pdflink", $.btns.sidebar).attr("href", $.btns.billprefix + $version.val() + ".pdf");
    $("a#warning_pdf", $.btns.warning).attr("href", $.btns.billprefix + $version.val() + ".pdf");
    
    $.btns.on_change_compare();
};

$.btns.on_change_compare = function() {
    var version = $("#version", $.btns.sidebar).val();
    var compareto = $("#compareto", $.btns.sidebar).val();

    // set version text in bill header.
    var vinfo = $.btns.versions[version];
    if (compareto == "none") {
	    $("p#vtext").html(vinfo.date + " - " + vinfo.name + ". " + vinfo.description +
                      ((vinfo.position === 1) ? " This is the latest version of the bill currently available on GovTrack." : ""));
    } else {
	    var cinfo = $.btns.versions[compareto];
	    $("p#vtext").html("You are viewing the revisions from the text as of "
	    			  + cinfo.date + " (" + cinfo.name + ")"
	    			  + " to the text as of "
	    			  + vinfo.date + " (" + vinfo.name + ").");
    }
    
    var $views = $("#view input", $.btns.sidebar);
    if (compareto === "none") {
        $views.attr("disabled", "disabled");
    } else {
        $views.removeAttr("disabled");
    }

    $.btns.switch_view();
};

// check #compareto.length in case disabled didn't work?
$.btns.on_change_view = function(event) {
    $.btns.switch_view();
};

$.btns.switch_view = function() {
    $.btns.warning.hide();

    var version = $("#version", $.btns.sidebar).val();
    // used for ie6, which does not properly disable select options
    var compareto = $("#compareto", $.btns.sidebar).val();
    // if compareto is none, doesn't matter what (disabled) view is set to
    var view = (compareto === "none") ? "none" : $("#view input:checked", $.btns.sidebar).val();

    // if correct page is already visible, no need to switch view
    if (($.btns.current_view !== view) || !$.btns.is_loaded(version, compareto, view)) {      
        // abort any pending loads in favor of this one
        if ($.btns.xhr) $.btns.xhr.abort();
        
        $.btns.store_viewstate(true);

        var $btnode;
        $.btns.current_view = view; // ok to do this here, b/c view_cache not set until actually loaded
        if (view === "none") {
            $btnode = $("div#billtext > div#plaintext");
            $("#main_changesonly", $.btns.sidebar).parent().hide(); // .parent() necessary for ie7
        } else if (view === "inline") {
            $btnode = $("div#billtext > div#inlinetext");
            $("#main_changesonly", $.btns.sidebar).parent().show();
        } else { // view === "side"
            $btnode = $("div#billtext > div#sidetext");
            $("#main_changesonly", $.btns.sidebar).parent().show();
        }
        
        if ($.btns.caching) {
            $("div#billtext > div#plaintext").hide();
            $("div#billtext > div#inlinetext").hide();
            $("div#billtext > div#sidetext").hide();
        } else {
            $.btns.fast_empty($("div#billtext > div#plaintext"));
            $.btns.fast_empty($("div#billtext > div#inlinetext"));
            $.btns.fast_empty($("div#billtext > div#sidetext"));
            $.btns.view_cache = {none: null, inline: null, side: null};
        }
    
        if (!$.btns.is_loaded(version, compareto, view)) {
            if (($.btns.versions[version].size > $.btns.large_cutoff) && !$.btns.handle_large) {
                $.btns.spinner.hide();
                $.btns.warning.show();
            } else {
                $.btns.spinner.show();    
             
                $.btns.view_cache[view] = null;
                $.btns.fast_empty($btnode);
        
                // need to use complete() and xhr.responseText to avoid stack overflow in Firefox
                // when passing text directly as an argument (as with success()).
                $.btns.xhr = $.ajax({ url: $.btns.get_billpath(version, compareto, view), dataType: "html", 
                    complete: function(result, status) {
                        if (status == "success" || status == "notmodified") {
                            $btnode.html(result.responseText);
                             
                            $.btns.btnode = $("> div", $btnode);
                            $.btns.view_cache[view] = {version: version, compareto: compareto};

                            // set pop-up usc link preview. using jquery.cluetip plugin.
                            $("a.usclink", $.btns.btnode).cluetip({ width: 350, showTitle: false, arrows: true, cluetipClass: 'jtip', tracking: false });
                
                            $.btns.spinner.hide();
                            $btnode.show();
                            $.btns.restore_viewstate(true);
                        } // should probably do some sort of error recovery here
                    }});
            }
        } else { // already loaded, just need to show it
            $.btns.spinner.hide();
            $btnode.show();
            $.btns.btnode = $("> div", $btnode);
            $.btns.restore_viewstate(true);
        }
    }
    
    // whether we load something new or not, we need to update the link-to href
    $.btns.make_link(version, compareto, view);
};

$.btns.is_loaded = function(version, compareto, view) {
    var current = $.btns.view_cache[view];
    
    if (current === null) return false;
    if (current.version !== version) return false;
    if (view === "none") return true;
    if (current.compareto === compareto) return true;
    return false;
};

$.btns.get_billpath = function(version, compareto, view) {
    if (view === "none") {
        return "/congress/billtext/plaintext.xpd?bill=" + $.btns.billid + "&version=" + version;
    } else if (view === "inline") {
        return "/congress/billtext/inlinetext.xpd?bill=" + $.btns.billid + "&version=" + version + "&compareto=" + compareto;
    } else { // view === "side"
        return "/congress/billtext/sidetext.xpd?bill=" + $.btns.billid + "&version=" + version + "&compareto=" + compareto;
    }
};

$.btns.make_link = function(version, compareto, view) {
    var query = $.query.empty();
 
    query.SET("bill", $.btns.billid);
    query.SET("version", version);
    if (compareto !== "none") {
        query.SET("compareto", compareto);
        query.SET("view", view);
    }
    
    $("#main_linker", $.btns.sidebar).attr("href", query.toString());
    $.btns.link_query = query;
};

// when switching views we keep track of state; specifically, which sections are collapsed
// and what section is at the top of the screen.
$.btns.store_viewstate = function(docollapsed) {
    if (!$.btns.isfirst && !$.btns.optimize) {
        $.btns.viewstate = {};
        $.btns.viewstate.collapsed = [];
    
        if (docollapsed) {
            $("div.collapsed", $.btns.btnode).each(function() {
                var nid = $(this).attr("nid");
                if (nid) { $.btns.viewstate.collapsed.push(nid); }
            });
        }
        
        // .offset() requires visible, but :visible selector makes it slow
        var $sections = $("div.section", $.btns.btnode); 
        var scrolltop = $(window).scrollTop();
    
        // we iterate through the sections until we find one that is on the screen.
        for (var i=0; i < $sections.length; i++) {
            var $item = $sections.eq(i);
            var offset = $item.offset({ lite: true, scroll: false }).top;

            // can't use >= b/c for some reason offset===scrolltop for sections that aren't visible
            if (offset > scrolltop) {
                $.btns.viewstate.visible_element = $item.attr("nid");
                $.btns.viewstate.visible_offset = offset - scrolltop;
                if ($.btns.viewstate.visible_element) { break; } // if no nid we keep looking
            }
        }
    }
};

// restores the saved viewstate. also initializes the view if there is a nid in the query string.
$.btns.restore_viewstate = function(docollapsed) {
    if ($.btns.isfirst) {
        if ($.btns.init_nid) {
            var $section_linked = $("div.section[nid='" + $.btns.init_nid + "']", $.btns.btnode);
            if ($section_linked.length === 1) {
                $section_linked.addClass("section_linked");
                $.btns.scroll_to($section_linked);
            }
        }
        $.btns.isfirst = false;
    } else if (!$.btns.optimize) {
        if (docollapsed) {
            $("div.collapsed", $.btns.btnode).each(function() { 
                $.btns.expand_section($(this), 0);
            });
            
            for (var i=0; i < $.btns.viewstate.collapsed.length; i++) {
                $.btns.collapse_section($("div.section[nid='" + $.btns.viewstate.collapsed[i] + "']", $.btns.btnode), 0);
            }
        }
        
        if ($.btns.viewstate.visible_element) {
            $section = $("div.section[nid='" + $.btns.viewstate.visible_element + "']", $.btns.btnode).eq(0);
            if ($section) {
                $.btns.scroll_to($section, $.btns.viewstate.visible_offset);
            }
        }
    }
};

$.btns.scroll_to = function($item, offset) {
    if (offset === undefined) offset = 12;

    $(window).scrollTop($item.offset({ lite: true, scroll: false }).top - offset);
};

// should be relatively fast to add classes, so we don't bother trying to
// filter out redundant events.
$.btns.section_over = function(event, section) {
    event = $.event.fix(event);

    var $section = $(section);
    
    // two selectors so will work with either plain/inline or side
    var $chooser = $section.addClass("section_hover").find("> div.chooser, > div.side_new > div.chooser").addClass("chooser_hover");

    $.btns.setup_chooser($section, $chooser);

    event.stopPropagation();
};

// to save time on initial page load, we set the chooser attributes when a section
// is moused over. reduces load time by %20.
$.btns.setup_chooser = function($section, $chooser) {
    var $linker = $("> a.linker", $chooser);
    if (!$linker.attr("href")) {
        $linker.attr("href", $.btns.link_query.set("nid", $section.attr("nid")).toString());

        $("> a.extractor", $chooser).attr("href", "http://www.govtrack.us/embed/sample-billtext.xpd" + 
            $.btns.link_query.remove("compareto").remove("view").set("nid", $section.attr("nid")).toString());
    
        // setting onclick attr doesn't work in some browsers
        $("> span", $chooser).click($.btns.toggle_section);
    }
};

$.btns.section_out = function(event, section) {
    event = $.event.fix(event);
    
    var $outsec = $(section);
    var $insec = $(event.relatedTarget);
    if (!$insec.is("div.section")) $insec = $insec.parents("div.section:first");

    if (!$insec.length || ($insec.get(0) !== $outsec.get(0))) { 
        $outsec.removeClass("section_hover").find("> div.chooser, > div.side_new > div.chooser").removeClass("chooser_hover");
    }
  
    event.stopPropagation();
};
    
$.btns.toggle_section = function() {
    var $section = $(this).parents("div.section:first");

    if ($section.hasClass("collapsed")) {
        $.btns.expand_section($section, "fast");
    } else {
        $.btns.collapse_section($section, "fast");
    }
};

// to collapse a section, we clone the paragraph, add '...' to the start of it, hide
// the original section (with or without animation), and add the .collapsed class (which
// reduces the opacity).
// tried adding summary in xslt to save on cloning but actually made it slower
$.btns.collapse_section = function($section, speed) {
    if (!$section.hasClass("collapsed")) {
        if ($.btns.current_view !== "side") { // plain or inline
            $section.addClass("collapsed").find("> div.chooser > span.expanded").attr("title", "Expand this section").attr("class", "contracted");
    
            var $summary = $section.find("> p:first").clone().addClass("summary");
            $summary.text($summary.text().substring(0,56) + "...");
            $section.append($summary);
            $.btns.collapse_animate($section.find("> :gt(0):not(:last)"), speed);
        } else { // side
            $section.addClass("collapsed").find("> div.side_new > div.chooser > span.expanded").attr("title", "Expand this section").attr("class", "contracted");
    
            var $side_old = $section.find("> div.side_old");
            var $summary_old = $side_old.find("> p:first").clone().addClass("summary");
            $summary_old.text($summary_old.text().substring(0,22) + "...");
            $side_old.append($summary_old);
            $.btns.collapse_animate($side_old.find("> :gt(0):not(:last)"), speed);
            
            var $side_new = $section.find("> div.side_new");
            var $summary_new = $side_new.find("> p:first").clone().addClass("summary");
            $summary_new.text($summary_new.text().substring(0,22) + "...");
            $side_new.append($summary_new);
            $.btns.collapse_animate($side_new.find("> :gt(0):not(:last)"), speed);
    
            $.btns.collapse_animate($section.find("> :gt(2)"), speed); 
        }
    }
};

$.btns.collapse_animate = function($items, speed) {
    ($.btns.optimize || (speed === 0)) ? $items.hide() : $items.slideUp(speed);
};

$.btns.expand_section = function($section, speed) {
    if ($section.hasClass("collapsed")) {
        if ($.btns.current_view !== "side") {
            $section.removeClass("collapsed").find("> div.chooser > span.contracted").attr("title", "Collapse this section").attr("class", "expanded");
    
            $section.find("> p.summary").remove();
            $.btns.expand_animate($section.find("> :gt(0)"), speed);
        } else {
            $section.removeClass("collapsed").find("> div.side_new > div.chooser > span.contracted").attr("title", "Collapse this section").attr("class", "expanded");
    
            var $side_old = $section.find("> div.side_old");
            $side_old.find("> p.summary").remove();
            $.btns.expand_animate($side_old.find("> :gt(0)"), speed);
    
            var $side_new = $section.find("> div.side_new");
            $side_new.find("> p.summary").remove();
            $.btns.expand_animate($side_new.find("> :gt(0)"), speed);
            
            $.btns.expand_animate($section.find("> :gt(2)"), speed);
        }
    }
};

$.btns.expand_animate = function($items, speed) {
    ($.btns.optimize || (speed === 0)) ? $items.show() : $items.slideDown(speed);
};

// when a toclink is clicked, we expand the section and then scroll to it.
$.btns.click_toclink = function(target) {
    var $target = $("div#" + $(target).attr("rel"), $.btns.btnode);
  
    $.btns.expand_section($target, 0);
    $.btns.scroll_to($target);
};

$.btns.expand_all = function() {
    $.btns.store_viewstate(false);

    $("div.collapsed", $.btns.btnode).each(function() { 
        $.btns.expand_section($(this), 0);
    });

    $.btns.restore_viewstate(false);
};

// we don't collapse toc sections (i.e. sections that contain a toclink).
// don't bother storing viewstate
$.btns.collapse_all = function() {
    // if possible, we only collapse sections starting with the first, so as not to
    // collapse the title, preamble, etc.
    var $sections;
    if ($.btns.current_view !== "inline") {
        var $sec1 = $("> div#sec1", $.btns.btnode);
        if ($sec1.length) {
            // expand sections before the first
            $sec1.prevAll("div.collapsed").add($sec1.prevAll("div.section").find("div.collapsed")).each(function() {
                $.btns.expand_section($(this), 0);
            });
            
            $sections = $sec1.nextAll("div.section").andSelf();
        } else {
            $sections = $("> div.section", $.btns.btnode);
        }
    } else {
        // don't bother filtering if inline, too complicated
        $sections = $("> div.section, > div.inserted > div.section, > div.removed > div.section", $.btns.btnode);
    }

    $sections.each(function() {
       var $section = $(this);
       
       if ($section.is(":has(a.toclink)")) {
           $("div.collapsed", $section).andSelf().each(function() {
               $.btns.expand_section($(this), 0);
           });
       } else {
           $.btns.collapse_section($section, 0);
       } 
    });
};

$.btns.changes_only = function() {
    $.btns.store_viewstate(false);
    
    if ($.btns.current_view === "side") {
        $.btns.node_changes_only_side($("> *", $.btns.btnode));
    } else { // inline view, can't be called with plain view
        $.btns.node_changes_only($("> *", $.btns.btnode));
    }
    
    $.btns.restore_viewstate(false);
};

// collapses only the topmost nodes that don't have changes (leaving the 
// child sections uncollapsed)
$.btns.node_changes_only = function($nodes) {
    $nodes.each(function() {
        var $node = $(this);
        
        // $node might be an inserted or removed div.
        if ($node.is("div.inserted, div.removed")) {
            $("div.section", $node).each(function() {
                $.btns.expand_section($(this), 0);
            });
        } else if ($node.is("div.section")) {
            if ($node.attr("changed")) {
                $.btns.expand_section($node, 0);
    
                var $subs = $("> *", $node);
                if ($subs.length) {
                    $.btns.node_changes_only($subs);
                }
            } else {
                $.btns.collapse_section($node, 0);
            }
        } // node could also be something else, like <hr>. just ignore.
    });  
};

// .inserted, etc. are part of ps (rather than being separate divs) in side view
$.btns.node_changes_only_side = function($nodes) {
    $nodes.each(function() {
        var $section = $(this);
        
        if ($section.attr("changed")) {
            $.btns.expand_section($section, 0);

            var $subs = $("> div.section", $section);
            if ($subs.length) {
                $.btns.node_changes_only_side($subs);
            }
        } else {
            $.btns.collapse_section($section, 0);
        }
    });  
};

// avoids very slow jquery code that does a more thorough job of cleaning up.
$.btns.fast_empty = function($node) {
    var parent = $node.get(0);
    
    $("> *", $node).each(function() {
        parent.removeChild($(this).get(0));
    });
};

$.btns.browser_fix = function() {
    if ($.browser.msie) {
        var version = parseInt($.browser.version.substr(0,1));
        
        // change doesn't work in ie
        $("#view input", $.btns.sidebar).unbind("change");
        $("#view input", $.btns.sidebar).click($.btns.on_change_view);
        
        if (version <= 6) {
            // force IE6 to cache background images
            try {
                document.execCommand("BackgroundImageCache", false, true);
            } catch(err) {}
            
            // use non-transparent PNGs
            jQuery.cssRule("div#billtext div.chooser span.expanded", "background-image", "url(images/ieexpanded.png)");
            jQuery.cssRule("div#billtext div.chooser span.contracted", "background-image", "url(images/iecontracted.png)");
            jQuery.cssRule("div#billtext div.chooser a.extractor", "background-image", "url(images/ieextract.png)");
            jQuery.cssRule("div#billtext div.chooser a.linker", "background-image", "url(images/ielink.png)");
            jQuery.cssRule("div#billtext p.quote", "background-image", "url(images/iequote.png)");

            jQuery.cssRule("div#billtext div.section", "zoom", "1"); // zoom forces hasLayout property
            jQuery.cssRule("div#billtext div.chooser", "margin", "0");
        }
        
        if (version <= 7) {
            // table 100% doesn't work in ie 6/7. 90% determined experimentally, though won't
            // be consistent in a variable-width layout
            $("div#billtext").parent().children().width("90%");

            jQuery.cssRule("div#billtext div.collapsed", "zoom", "1"); // required for opacity
        }
    } else if ($.browser.mozilla) {
        var version = parseFloat($.browser.version.substr(0,3));
        
        // ff 2 doesn't support display: inline-block
        if (version < 1.9) { // less that 1.9; i.e. less than ff 3.0
            jQuery.cssRule("div#billtext div.chooser", "margin-top", "-16px");
            jQuery.cssRule("div#billtext div.chooser *", "display", "-moz-inline-box");
        }
    }
};

$(document).ready(function() { 
    $.btns.sidebar = $("div#sidebar"); // parent div is the one with the styling
    
    // set up #billtext structure
    var $bt = $("div#billtext");
    $bt.append("<div><img id='spinner' src='billtext/images/spinner.gif'></img></div>");
    $.btns.spinner = $("> div > img#spinner", $bt);
    $bt.append("<div id='plaintext'></div>");
    $bt.append("<div id='inlinetext'></div>");
    $bt.append("<div id='sidetext'></div>");

    // set up warning pane
    $bt.append("<div id='warning'>" + $.btns.warning_message + 
               "<p style='text-align:right;'><a id='confirm'>Continue on to the bill...</a></p></div>");
    $.btns.warning = $("> div#warning", $bt);
    $("a#warning_thomas", $.btns.warning).attr("href", $("a#thomaslink", $.btns.sidebar).attr("href"));
    $("a#confirm", $.btns.warning).click(function() {
        $.btns.handle_large = true;
        $.btns.switch_view();
    });
    $.btns.warning.hide();
    
    // parse query string
    $.btns.billid = $.query.get("bill");
    $.btns.init_nid = $.query.get("nid");
    $.btns.init_version = $.query.get("version");
    $.btns.init_compareto = $.query.get("compareto");
    
    var init_view = $.query.get("view");
    if ((init_view === "inline") || (init_view === "side")) {
        $("#view input", $.btns.sidebar).val([init_view]);
    }
    
    // find bill number and save bill path to global data
    var id = /^([sh][rcj]*)(\d+)-(\d+)$/.exec($.btns.billid);
    $.btns.billprefix = "/data/us/bills.text/" + id[2] + "/" + id[1] + "/" + id[1] + id[3];
    
    $.btns.view_cache = {none: null, inline: null, side: null};

    // set this once in the beginning, too tricky to try and adjust for each version
    if (($.btns.versions[$("#version", $.btns.sidebar).val()].size >= $.btns.optimize_cutoff)) {
        $.btns.optimize = true;
    }
    
    $("#version", $.btns.sidebar).change($.btns.on_change_version);
    $("#compareto", $.btns.sidebar).change($.btns.on_change_compare);
    $("#view input", $.btns.sidebar).change($.btns.on_change_view);

    $("#main_expander", $.btns.sidebar).click($.btns.expand_all);
    $("#main_collapser", $.btns.sidebar).click($.btns.collapse_all);
    $("#main_changesonly", $.btns.sidebar).click($.btns.changes_only);
    
    // avoids slowdown on page unload. may cause memory leaks in ie6.
    jQuery(window).unbind("unload");
    
    $.btns.isfirst = true;
    
    $.btns.browser_fix();
    
    // trigger the initial bill load
    $.btns.on_change_version();
});
