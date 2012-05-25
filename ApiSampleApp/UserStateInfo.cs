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
		Dictionary<string, Func<object>> _pendingActions;
		
		public UserStateInfo()
		{
			_stateCache = new Dictionary<string, BasespaceActionInfo>();
			_pendingActions = new Dictionary<string, Func<object>>();
		}

		public string AddStateInfo(BasespaceActionInfo info)
		{
			var key = Guid.NewGuid().ToString();
			_stateCache[key] = info;
			info.Key = key;
			return key;
		}

		public BasespaceActionInfo GetStateInfo(string key)
		{
			BasespaceActionInfo result;
			if (!_stateCache.TryGetValue(key, out result))
				return null;
			result.Key = key;
			return result;
		}

		public string AddPendingAction(Func<object> action)
		{
			var key = Guid.NewGuid().ToString();
			_pendingActions[key] = action;
			return key;
		}
		public Func<object> GetPendingAction(string key)
		{
			Func<object> result = null;
			if (_pendingActions.TryGetValue(key, out result))
				_pendingActions.Remove(key);
			return result;
		}
	}
}