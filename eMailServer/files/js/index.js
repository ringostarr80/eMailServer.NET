var eMailServerUI = function() {
	function init() {
		console.log('eMailServerUI.init()');
		
		$('header nav ul li[rel]').on('click', navigationClicked);
		$('#logout').on('click', function() {location.href = "/logout/";});
		
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
