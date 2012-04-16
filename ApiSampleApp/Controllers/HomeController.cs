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
	/// <summary>
	/// A sample controller for a BaseSpace application. 
	/// This sample illustrates how to process incoming requests, complete authentication,
	/// and access resources in BaseSpace.
	/// NOTE: For simplicity, we have stored all state in the in-memory cache and have all
	/// application secret keys in the web.config file. You will want to use a more robust mechanism
	/// in a real application.
	/// </summary>
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

		/// <summary>
		/// This is the main endpoint registered with BaseSpace. Depending on what parameters
		/// are send in the request, this method dispatches to:
		/// - Initial Trigger
		/// - Authorization Failed
		/// - Authorization Succeeded
		/// </summary>
		/// <param name="action">The user action</param>
		/// <param name="actionuri">The URI to fetch the activation information </param>
		/// <param name="returnuri">The URI to return the user to the previously viewed page</param>
		/// <param name="error">The error encountered during authorization</param>
		/// <param name="error_description">The description of he authorization error</param>
		/// <param name="state">Your state value</param>
		/// <param name="code">The authorization code granted by BaseSpace</param>
		/// <returns></returns>
		public ActionResult Trigger(string action, string actionuri, string returnuri,
			string error, string error_description, string state, string code)
		{
			if (actionuri != null)
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

		/// <summary>
		/// Basic authorization is used to fetch the activation information BEFORE authentication.
		/// </summary>
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
				// fetch the action info. This will tell you what the user selected in BaseSpace
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
					

				if (response.StatusCode != HttpStatusCode.OK)
				{
					ViewBag.Message = string.Format("Error fetching action info from '{0}'", actionuri);
				}
				else
				{
					using (var stm = new StreamReader(response.GetResponseStream()))
					{
						dynamic dict = DeserializeResponse(stm);
						var model = new BasespaceActionInfo(dict);

						var userId = model.UserId;

						var stateInfo = GetUserStateInfo(userId);
						// do we have an authorization code for this user yet? If not, we need to get it
						// redirect the browser
						if (string.IsNullOrEmpty(stateInfo.AuthToken))
						{
							var stateId = stateInfo.AddStateInfo(model);
							var oauthUrl = string.Format("{0}?client_id={1}&redirect_uri={2}&response_type=code&state={3}&scope={4}",
								ConfigurationManager.AppSettings["OauthUri"],
								ConfigurationManager.AppSettings["MyClientId"],
								ConfigurationManager.AppSettings["MyRedirectUri"],
								userId + ":" + stateId,
								model.GetRequestedScope()
								);
							
							return Redirect(oauthUrl);
						}
						return MainApplicationHandler(stateInfo, model);
					}
				}
			}
			catch (Exception e)
			{
				ViewBag.Message = string.Format("Error fetching action info from '{0}': {1}", actionuri, e);
			}

			return View("ShowRawText");
		}

		/// <summary>
		/// Simple helper method to deserialize into a dictionary. 
		/// </summary>
		private static dynamic DeserializeResponse(StreamReader stm)
		{
			var data = stm.ReadToEnd();
			var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
			dynamic dict = json.DeserializeObject(data);
			return dict;
		}

		/// <summary>
		/// A simple cache of information about each user. In a real application, this would be in 
		/// a persistent database
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
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

		/// <summary>
		/// This is the routine that fetches a resource from BaseSpace. This should show you
		/// how to add the OAuth authorization token to the request so you can act on behalf of
		/// the user
		/// </summary>
		/// <param name="userId">The user ID. This is needed because the sample app uses the user ID
		/// as the key to the authorization token cache</param>
		/// <param name="resource">The relative URI to fetch. This will be one of the Href fields in a returned object.</param>
		/// <returns>A JSON document with the resource contents.</returns>
		public ActionResult FetchResource(string userId, string resource)
		{
			var userStateInfo = GetUserStateInfo(userId);
			if (string.IsNullOrEmpty(userStateInfo.AuthToken))
				return new ContentResult { Content = "Invalid auth token" };
			var uri = ConfigurationManager.AppSettings["BasespaceAppServerUri"] + resource;
			var request = WebRequest.Create(uri);
			request.Headers["Authorization"] = "Bearer " + userStateInfo.AuthToken;
			var response = (HttpWebResponse)request.GetResponse();

			// you can process the resulting data here. Instead,  application simply hands it to the UI to display
			using (var stm = new StreamReader(response.GetResponseStream()))
			{
				return new ContentResult { Content = stm.ReadToEnd(), ContentType = "application/json" };
			}
		}

		/// <summary>
		/// This method drives the population of the web page from the stored data
		/// </summary>
		ActionResult MainApplicationHandler(UserStateInfo stateInfo, BasespaceActionInfo model)
		{
			return View("DisplayBasespaceData", model);
		}

		/// <summary>
		/// Called by the web browser (indirectly) when authorization was approved
		/// </summary>
		/// <param name="state">The state value we passed to BaseSpace at the start of authorization</param>
		/// <param name="code">The authorization code</param>
		/// <returns></returns>
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
			// turn all my arguments into query parameters
			var args = from kvp in payload select kvp.Key + "=" + kvp.Value;
			var request = WebRequest.Create(oauthUri + "?" + string.Join("&", args));

			// Do a POST request to get the authorization code
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

				// my state is userid:stateId - split them back apart
				var user = state.Split(':')[0];
				var stateId = state.Split(':')[1];
				var stateInfo = GetUserStateInfo(user);

				stateInfo.AuthToken = dict["access_token"] as string;

				// now that authorization is done, I can erase the state info
				var context = stateInfo.GetAndDeleteStateInfo(stateId);

				// now that authorization is compl;ete, we can act on the user's behalf.
				return MainApplicationHandler(stateInfo, context);
			}
		}
	}
}
