var eMailServerUI = function() {
	function init() {
		if (window.console) console.log('eMailServerUI.init()');
		
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
			$('header nav ul li[rel="' + rel + '"]').addClass('active');
			
			$('.content').hide();
			contentElement.show();
		}
	}
	
	function listMailsClicked() {
		if (window.console) console.log('eMailServerUI.listMailsClicked()');
		var navElement = $(this);
		if (navElement.hasClass('active')) {
			return;
		}
		
		var dataAttr = navElement.attr('data-list-mails');
		$('*[data-list-mails],#write_email').removeClass('active');
		$('*[data-list-mails="' + dataAttr + '"]').addClass('active');
		
		switch(dataAttr) {
			case 'all':
				$.get('/mails/all?limit=50', function(data) {
					var mails = $(data).find('email_server > mails > mail');
					var mailList = $('#mail_list');
					var tBody = mailList.find('tbody');
						tBody.children().remove();
					mails.each(function() {
						var current = $(this);
						var id = current.attr('id');
						var from = current.attr('from');
						var subject = current.attr('subject');
						var time = current.attr('time');
						var headerFrom = current.find('header_from[name]');
						if (headerFrom.length === 1) {
							if (headerFrom.attr('name') !== '') {
								from = headerFrom.attr('name');
							}
						}

						var row = $('<tr id="' + id + '"></tr>');
							row.append('<td>' + from + '</td>');
							row.append('<td>' + subject + '</td>');
							row.append('<td style="text-align: right;">' + time + '</td>');
							row.append('<td>&nbsp;</td>');
						tBody.append(row);
					});
					mailList.dataTable({
						aaSorting: [[2, 'desc']],
						sDom: '<"top"lp<"clear">>rt<"bottom"ip<"clear">>' // f: suche; i: showing x of y entries; l: entries per page selector
					});

					mailList.find('tbody tr').on('click', function() {
						$.get('/mail/get?id=' + $(this).attr('id'), function(data) {
							var mail = $(data).find('email_server mail');
							var message = mail.find('message');
							$('#mail_content').html(message.html());
						});
					});
				});
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
		
		$('#mail_folder_content').hide();
		$('#write_email_content').show();
		//$('#mail_content,#mail_list').css({visibility: 'visible'});
		$('#write_email_body').prop('contenteditable', true);
	}
	
	return {
		init: init
	};
}();

$(document).ready(function() {
	eMailServerUI.init();
});
