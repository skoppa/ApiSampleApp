using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Net;
using System.IO;
using System.Configuration;
using ApiSampleApp.Models;

namespace ApiSampleApp.Controllers
{
	public class HomeController : Controller
	{
		public ActionResult Index()
		{
			ViewBag.Message = "Welcome to the BaseSpace Sample Application!";

			return View();
		}

		public ActionResult About()
		{
			return View();
		}

		public ActionResult Trigger(string action, string actionuri, string returnuri,
			string error, string error_description, string state, string code)
		{
			if (null != actionuri)
				return HandleInitialTrigger(actionuri, returnuri);
			else if (error != null)
				return HandleAuthFailed(error, error_description);
			return HandleAuthApproved(state, code);
		}

		private ActionResult HandleAuthFailed(string error, string error_description)
		{
			ViewBag.Message = string.Format("Auth failed: error: {0}, error_description: {1}", error, error_description);
			return View("ShowRawText");
		}
		public void SetBasicAuthHeader(WebRequest request, String userName, String userPassword)
		{
			string authInfo = userName + ":" + userPassword;
			authInfo = Convert.ToBase64String(System.Text.Encoding.Default.GetBytes(authInfo));
			request.Headers["Authorization"] = "Basic " + authInfo;
		}

		private ActionResult HandleInitialTrigger(string actionuri, string returnuri)
		{
			try
			{
				// fetch the action info
				string uri;
				if (actionuri.ToLower().StartsWith("http"))
					uri = actionuri;
				else
					uri = ConfigurationManager.AppSettings["BasespaceAppServerUri"] + actionuri;
				var request = WebRequest.Create(uri);
				request.ContentType = "application/json";
				SetBasicAuthHeader(request, ConfigurationManager.AppSettings["MyClientId"],
					ConfigurationManager.AppSettings["MyClientSecret"]);
				var response = (HttpWebResponse)request.GetResponse();
				string scope = null;
					

				if (response.StatusCode != HttpStatusCode.OK)
				{
					ViewBag.Message = string.Format("Error fetching action info from '{0}'", actionuri);
				}
				else
				{
					using (var stm = new StreamReader(response.GetResponseStream()))
					{
						dynamic dict = DeserializeResponse(stm);
						var userId = dict["ApplicationAction"]["User"]["Id"] as string;

						var stateInfo = GetUserStateInfo(userId);
						// do we have an authorization code for this user yet? If not, we need to get it
						// redirect the browser
						if (string.IsNullOrEmpty(stateInfo.AuthToken))
						{
							// TODO: initialize scope
							var stateId = stateInfo.AddStateInfo(dict);
							var oauthUrl = string.Format("{0}?client_id={1}&redirect_uri={2}&response_type=code&state={3}",
								ConfigurationManager.AppSettings["OauthUri"],
								ConfigurationManager.AppSettings["MyClientId"],
								ConfigurationManager.AppSettings["MyRedirectUri"],
								userId + ":" + stateId
								);
							if (scope != null)
							{
								oauthUrl += "&scope=" + scope;
							}

							return Redirect(oauthUrl);
						}
						return MainApplicationHandler(stateInfo, dict);
					}
				}
			}
			catch (Exception e)
			{
				ViewBag.Message = string.Format("Error fetching action info from '{0}': {1}", actionuri, e);
			}

			return View("ShowRawText");
		}

		private static dynamic DeserializeResponse(StreamReader stm)
		{
			var data = stm.ReadToEnd();
			var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
			dynamic dict = json.DeserializeObject(data);
			return dict;
		}

		UserStateInfo GetUserStateInfo(string userId)
		{
			Dictionary<string, UserStateInfo> stateCache;
			lock (typeof(HomeController))
			{
				stateCache = HttpRuntime.Cache.Get("user_cache") as Dictionary<string, UserStateInfo>;
				if (stateCache == null)
				{
					stateCache = new Dictionary<string, UserStateInfo>();

					HttpRuntime.Cache.Add("user_cache", stateCache, null,
							System.Web.Caching.Cache.NoAbsoluteExpiration,
							System.Web.Caching.Cache.NoSlidingExpiration, System.Web.Caching.CacheItemPriority.Normal, null);
				}
			}
			lock (stateCache)
			{
				if (!stateCache.ContainsKey(userId))
					stateCache[userId] = new UserStateInfo();
				return stateCache[userId];
			}
		}

		ActionResult MainApplicationHandler(UserStateInfo stateInfo, dynamic activationInfo)
		{
			var model = new BasespaceActionInfo(activationInfo);
			return View("DisplayBasespaceData", model);
		}

		public ActionResult HandleAuthApproved(string state, string code)
		{
			// we got the authorization code, now we need an access token
			var payload = new Dictionary<string, string>
			{
				{"client_id", ConfigurationManager.AppSettings["MyClientId"]}, 
				{"redirect_uri", ConfigurationManager.AppSettings["MyRedirectUri"]},
				{"grant_type", "authorization_code" },
				{"code", code },
				{"client_secret", ConfigurationManager.AppSettings["MyClientSecret"] }
			};
			var oauthUri = ConfigurationManager.AppSettings["OauthTokenUri"];
			var args = from kvp in payload select kvp.Key + "=" + kvp.Value;
			var request = WebRequest.Create(oauthUri + "?" + string.Join("&", args));
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = 0;
			request.Method = "POST";
			var response = (HttpWebResponse)request.GetResponse();

			if (response.StatusCode != HttpStatusCode.OK)
			{
				ViewBag.Message = string.Format("Error fetching authorization code from '{0}'", oauthUri);
				return View("ShowRawText");
			}
			using (var stm = new StreamReader(response.GetResponseStream()))
			{
				dynamic dict = DeserializeResponse(stm);

				var user = state.Split(':')[0];
				var stateId = state.Split(':')[1];
				var stateInfo = GetUserStateInfo(user);

				stateInfo.AuthToken = dict["access_token"] as string;

				var context = stateInfo.GetAndDeleteStateInfo(stateId);
				return MainApplicationHandler(stateInfo, context);
			}
		}
	}
}
