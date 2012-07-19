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
						var stateId = stateInfo.AddStateInfo(model);
							
						// do we have an authorization code for this user yet? If not, we need to get it
						// redirect the browser
						if (string.IsNullOrEmpty(stateInfo.AuthToken))
						{
							var oauthUrl = BuildOAuthUrl(userId, stateId, null, model.GetRequestedScope());
							
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

		private string BuildOAuthUrl(string userId, string stateId, string actionState, string scope)
		{
			return string.Format("{0}?client_id={1}&redirect_uri={2}&response_type=code&state={3}{4}&scope={5}",
								ConfigurationManager.AppSettings["OauthUri"],
								ConfigurationManager.AppSettings["MyClientId"],
								ConfigurationManager.AppSettings["MyRedirectUri"],
								userId + ":" + stateId,
								actionState == null ? string.Empty : ":" + actionState,
								scope
								);
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
		/// a real database
		/// </summary>
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
		/// This is the routine that fetches a resource from BaseSpace. It demonstrates 
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
			uri = uri + (uri.Contains('?') ? "&" : "?") + "Limit=50";
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
		/// Create a new analysis
		/// </summary>
		/// <returns>The ID of the analysis</returns>
		string PostNewAnalysis(string userId, string projectId, string name, string description)
		{
			var userStateInfo = GetUserStateInfo(userId);
			if (string.IsNullOrEmpty(userStateInfo.AuthToken))
				throw new ApplicationException("No authorization token");
			var uri = string.Format("{0}{1}/projects/{2}/analyses?Name={3}&Description={4}",
					ConfigurationManager.AppSettings["BasespaceAppServerUri"],
					ConfigurationManager.AppSettings["ApiVersionPrefix"],
					projectId,	HttpUtility.UrlEncode(name), HttpUtility.UrlEncode(description));

			var request = WebRequest.Create(uri);
			request.Headers["Authorization"] = "Bearer " + userStateInfo.AuthToken;
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = 0;
			request.Method = "POST";
			try
			{
				var response = (HttpWebResponse)request.GetResponse();

				dynamic responseData;
				using (var stm = new StreamReader(response.GetResponseStream()))
				{
					responseData = DeserializeResponse(stm);
				}

				if (response.StatusCode != HttpStatusCode.Created)
					throw new ApplicationException("Creation failed: " + responseData["ResponseStatus"]["ErrorCode"]);
				return responseData["Response"]["Id"];
			}
			catch (WebException wex)
			{
				string x;
				if (wex.Response != null && wex.Response.ContentLength > 0)
				{
					using (var stm = new StreamReader(wex.Response.GetResponseStream()))
					{
						x = stm.ReadToEnd();
					}
				}
				throw;
			}
				
		}

		/// <summary>
		/// Create a file in an analysis
		/// </summary>
		/// <returns>The ID of the file</returns>
		string PostNewFileToAnalysis(string userId, string analysisId, string name, string directory, 
			string contentType, byte[] data)
		{
			var userStateInfo = GetUserStateInfo(userId);
			if (string.IsNullOrEmpty(userStateInfo.AuthToken))
				throw new ApplicationException("No authorization token");
			var uri = string.Format("{0}{1}/analyses/{2}/files?Name={3}&Directory={4}",
					ConfigurationManager.AppSettings["BasespaceAppServerUri"],
					ConfigurationManager.AppSettings["ApiVersionPrefix"],
					analysisId, name, directory);

			
			var request = WebRequest.Create(uri)as HttpWebRequest;
			request.Headers["Authorization"] = "Bearer " + userStateInfo.AuthToken;
			request.ContentType = contentType;
			request.ContentLength = data.Length;
			request.Method = "POST";
			using (var stm = request.GetRequestStream())
			{
				stm.Write(data, 0, data.Length);
			}
			var response = (HttpWebResponse)request.GetResponse();

			dynamic responseData;
			using (var stm = new StreamReader(response.GetResponseStream()))
			{
				responseData = DeserializeResponse(stm);
			}

			if (response.StatusCode != HttpStatusCode.Created)
				throw new ApplicationException("Creation failed: " + responseData["ResponseStatus"]["ErrorCode"]);
			return responseData["Response"]["Id"];
		}

		/// <summary>
		/// Create a file in an analysis
		/// </summary>
		/// <returns>The ID of the analysis</returns>
		void SetAnalysisStatus(string userId, string analysisId, string status, string statusSummary, string statusDetails)
		{
			var userStateInfo = GetUserStateInfo(userId);
			if (string.IsNullOrEmpty(userStateInfo.AuthToken))
				throw new ApplicationException("No authorization token");
			var uri = string.Format("{0}{1}/analyses/{2}?Status={3}&StatusSummary={4}&StatusDetails={5}",
					ConfigurationManager.AppSettings["BasespaceAppServerUri"],
					ConfigurationManager.AppSettings["ApiVersionPrefix"],
					analysisId, status, 
					HttpUtility.UrlEncode(statusSummary), 
					HttpUtility.UrlEncode(statusDetails));


			var request = WebRequest.Create(uri) as HttpWebRequest;
			request.Headers["Authorization"] = "Bearer " + userStateInfo.AuthToken;
			request.ContentType = "application/x-www-form-urlencoded";
			request.ContentLength = 0;
			request.Method = "POST";
			var response = (HttpWebResponse)request.GetResponse();

			dynamic responseData;
			using (var stm = new StreamReader(response.GetResponseStream()))
			{
				responseData = DeserializeResponse(stm);
			}

			if (response.StatusCode != HttpStatusCode.OK)
				throw new ApplicationException("Creation failed: " + responseData["ResponseStatus"]["ErrorCode"]);
		}



		public ActionResult CreateAnalysis(string userId, string stateKey, string projectId)
		{
			// to create something in a project, we need write access to that project
			// request that here
			var stateInfo = GetUserStateInfo(userId);
			var model = stateInfo.GetStateInfo(stateKey);
			Func<ActionResult> continuation = () =>
				{
					// this will be called after we have been granted the permission to create
					// the analysis
					try
					{
						var analysisName = "New Analysis " + DateTime.Now;

						var id = PostNewAnalysis(userId, projectId, analysisName, "A dummy analysis");
						var fileId = PostNewFileToAnalysis(userId, id, "foo.txt", "a/b/c", "text/plain; charset=utf-8", 
							System.Text.Encoding.UTF8.GetBytes ("Howdy, folks!"));
						SetAnalysisStatus(userId, id, "Completed", "It's ready", "I'm all done with it");
					}
					catch (Exception ex)
					{
						ViewBag.Message = string.Format("Error creating analysis/posting file: {0}", ex);
						return View("ShowRawText");
					}
					return MainApplicationHandler(stateInfo, model);
				};
			var actionKey = stateInfo.AddPendingAction(continuation);
			var oauthUrl = BuildOAuthUrl(userId, stateKey, actionKey, "write project " + projectId);

			return Redirect(oauthUrl);
			
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
				{"redirect_uri", ConfigurationManager.AppSettings["MyRedirectUri"]},
				{"grant_type", "authorization_code" },
				{"code", code }
			};
			var oauthUri = ConfigurationManager.AppSettings["OauthTokenUri"];
			// turn all my arguments into query parameters
			var args = from kvp in payload select kvp.Key + "=" + kvp.Value;
			var request = WebRequest.Create(oauthUri + "?" + string.Join("&", args));
			SetBasicAuthHeader(request, ConfigurationManager.AppSettings["MyClientId"],
					ConfigurationManager.AppSettings["MyClientSecret"]);
				

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

				// my state is userid:stateId[:resumeKey] - split them back apart
				var parts = state.Split(':');
				var user = parts[0];
				var stateId = parts[1];
				var stateInfo = GetUserStateInfo(user);

				stateInfo.AuthToken = dict["access_token"] as string;


				if (parts.Length > 2)
				{
					// I have stored a pending action (I was creating an analysis)
					// resume that work from here
					var resume = stateInfo.GetPendingAction(parts[2]);
					if (resume != null)
						return resume() as ActionResult;
				}

				// now that authorization is complete, we can act on the user's behalf.
				// look up the stored information about their original request and display
				// the main page
				var model = stateInfo.GetStateInfo(stateId);

				return MainApplicationHandler(stateInfo, model);
			}
		}
	}
}
