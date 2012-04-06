function toHtml(o, padding, indent) {
	var result = '<div style="font-family:monospace">' + padding + '{</div>';
	$.each(o, function (name, value) {
		result += '<div style="font-family:monospace">' + padding + indent + name + ':&nbsp';

		if (Object.prototype.toString.call(value) === '[object Array]') {
			result += '<span>[</span>';
			$.each(value, function (i, v) {
				result += toHtml(v, padding + indent + indent, indent);
			});
			result += '<span>' + padding + indent + ']</span>';
		}
		else if (typeof value == 'object') {
			result += toHtml(value, padding + indent, indent);
		}
		else if (name.indexOf('Href') === 0 && value.substr(-7) != 'content') {
			// this is an href but not a file download - make it clickable
			result += '<a href="#" class="resource">' + value + '</a>';
		} else	{
			result += '<span>' + value + '</span>';
		}
		result += '</div>';

	});
	result += '<div style="font-family:monospace">' + padding + '}</div>'
	return result;
}

function fetchResource(event, userId) {
	var url = location.protocol + '//' + location.hostname + ':' + location.port + '/Home/FetchResource?userId=' + userId + '&resource=' + $(event.target).text();

	$.getJSON(url, function(data) {
		$('#results').html(toHtml(data, '', '&nbsp&nbsp'));
		$('#results a.resource').click(function (e) { 
			fetchResource(e, userId); 
		});
	});
	event.preventDefault();
}
	
