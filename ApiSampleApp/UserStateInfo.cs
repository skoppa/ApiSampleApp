using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ApiSampleApp.Models;

namespace ApiSampleApp
{
	public class UserStateInfo
	{
		public string AuthToken { get; set; }
		public DateTime Expires { get; set; }

		Dictionary<string, BasespaceActionInfo> _stateCache;

		public UserStateInfo()
		{
			_stateCache = new Dictionary<string, BasespaceActionInfo>();
		}

		public string AddStateInfo(BasespaceActionInfo info)
		{
			var key = Guid.NewGuid().ToString();
			_stateCache[key] = info;
			return key;
		}

		public BasespaceActionInfo GetAndDeleteStateInfo(string key)
		{
			BasespaceActionInfo result;
			if (!_stateCache.TryGetValue(key, out result))
				return null;
			_stateCache.Remove(key);
			return result;
		}
	}
}