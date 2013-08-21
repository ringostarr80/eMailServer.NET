var eMailServerUI = function() {
	function init() {
		console.log('eMailServerUI.init()');
		
		$('header nav ul li[rel]').on('click', navigationClicked);
		$('#logout').on('click', function() {location.href = "/logout/";});
		
		$('header nav ul li[rel="#start"]').trigger('click');
		$('*[data-list-mails]').on('click', listMailsClicked);
		$('#write_email').on('click', writeEMailClicked);
		
		getMailsCount();
	}
	
	function getMailsCount() {
		$.get('/mails/count', function(data) {
			var mails = $(data).find('email_server > mails');
			if (mails.length == 1) {
				var mails_count = mails.attr('count');
				if (mails_count) {
					$('#emails_count').html(mails_count);
				}
			}
		});
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
	
	function listMailsClicked() {
		var navElement = $(this);
		if (navElement.hasClass('active')) {
			return;
		}
		
		var dataAttr = navElement.attr('data-list-mails');
		$('*[data-list-mails],#write_email').removeClass('active');
		$('*[data-list-mails="'+dataAttr+'"]').addClass('active');
		
		switch(dataAttr) {
			case 'all':
				$.get('/mails/all?limit=50');
				break;
			
			default:
				break;
		}
	}
	
	function writeEMailClicked() {
		var navElement = $(this);
		if (navElement.hasClass('active')) {
			return;
		}
		
		$('*[data-list-mails]').removeClass('active');
		$('#write_email').addClass('active');
	}
	
	return {
		init: init
	};
}();

$(document).ready(function() {
	eMailServerUI.init();
});
