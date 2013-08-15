var eMailServerUI = function() {
	function init() {
		console.log('eMailServerUI.init()');
		
		$('header nav ul li[rel]').on('click', navigationClicked);
		
		$('header nav ul li[rel="#start"]').trigger('click');
	}
	
	function navigationClicked(event) {
		var navElement = $(this);
		if (navElement.hasClass('active')) {
			return;
		}
		
		var rel = navElement.attr('rel');
		var contentElement = $(rel);
		if (contentElement.length === 1) {
			$('header nav ul li[rel]').removeClass('active');
			$('header nav ul li[rel="'+rel+'"]').addClass('active');
			
			$('.content').hide();
			contentElement.show();
		}
	}
	
	return {
		init: init
	};
}();

$(document).ready(function() {
	eMailServerUI.init();
});
