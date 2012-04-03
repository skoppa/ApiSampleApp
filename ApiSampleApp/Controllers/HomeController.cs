using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Net;
using System.IO;
using System.Configuration;

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
						var data = stm.ReadToEnd();
						var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
						var dict = (IDictionary<string, object>)json.DeserializeObject(data);

						// store in local DB so that after the authentication, we can retrieve the request info
						// for this demo, just put it in the in-memory cache
						// TODO: extract the request key
						HttpRuntime.Cache.Add("my_request", dict, null,
							System.Web.Caching.Cache.NoAbsoluteExpiration,
							TimeSpan.FromDays(1), System.Web.Caching.CacheItemPriority.Normal, null);

						// TODO: initialize scope

						ViewBag.Message = string.Format("Got a dictionary with the following keys: {0}", string.Join(",", dict.Keys));
					}
					// do we have an authorization code for this user yet? If not, we need to get it
					// redirect the browser
					var oauthUrl = string.Format("{0}?client_id={1}&redirect_uri={2}&response_type=code",
						ConfigurationManager.AppSettings["OauthUri"],
						ConfigurationManager.AppSettings["MyClientId"],
						ConfigurationManager.AppSettings["MyRedirectUri"]
						);
					if (scope != null)
					{
						oauthUrl += "&scope=" + scope;
					}
					return Redirect(oauthUrl);
				}
			}
			catch (Exception e)
			{
				ViewBag.Message = string.Format("Error fetching action info from '{0}': {1}", actionuri, e);
			}

			return View("ShowRawText");
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
			var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
			var data = json.Serialize(payload);
			var oauthUri = ConfigurationManager.AppSettings["OauthTokenUri"];
			var request = WebRequest.Create(oauthUri);
			request.ContentType = "application/json";
			request.Accept = "application/json";
			request.ContentLength = data.Length;
			request.Method = "POST";
			using (var streamWriter = new StreamWriter(request.GetRequestStream()))
			{
				streamWriter.Write(data);
			}
			var response = (HttpWebResponse)request.GetResponse();

			if (response.StatusCode != HttpStatusCode.OK)
			{
				ViewBag.Message = string.Format("Error fetching authorization code from '{0}'", oauthUri);
			}
			else
			{
				using (var stm = new StreamReader(response.GetResponseStream()))
				{
					data = stm.ReadToEnd();
					var dict = (IDictionary<string, object>)json.DeserializeObject(data);

					// store in local DB so that after the authentication, we can retrieve the request info
					// for this demo, just put it in the in-memory cache
					HttpRuntime.Cache.Add("my_authcode", dict, null,
						System.Web.Caching.Cache.NoAbsoluteExpiration,
						TimeSpan.FromDays(1), System.Web.Caching.CacheItemPriority.Normal, null);

					ViewBag.Message = string.Format("Got a dictionary with the following keys: {0}", string.Join(",", dict.Keys));
				}
			}
			return View("ShowRawText");
		}
	}
}
