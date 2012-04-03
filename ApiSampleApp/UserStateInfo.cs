using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ApiSampleApp
{
	public class UserStateInfo
	{
		public string AuthToken { get; set; }
		public DateTime Expires { get; set; }
		
		Dictionary<string, object> _stateCache;

		public UserStateInfo()
		{
			_stateCache = new Dictionary<string, object>();
		}

		public string AddStateInfo(object info)
		{
			var key = Guid.NewGuid().ToString();
			_stateCache[key] = info;
			return key;
		}

		public object GetAndDeleteStateInfo(string key)
		{
			object result;
			if (!_stateCache.TryGetValue(key, out result))
				return null;
			_stateCache.Remove(key);
			return result;
		}
	}
}