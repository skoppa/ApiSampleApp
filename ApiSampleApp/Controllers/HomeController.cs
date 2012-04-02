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

		public ActionResult Trigger(string actionuri, string returnuri,
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

		private ActionResult HandleInitialTrigger(string actionuri, string returnuri)
		{
			try
			{
				// fetch the action info
				var request = WebRequest.Create(actionuri);
				request.ContentType = "application/json";
				var response = (HttpWebResponse)request.GetResponse();

				if (response.StatusCode != HttpStatusCode.OK)
				{
					ViewBag.Message = string.Format("Error fetching action info from '{0}'", actionuri);
				}
				else
				{
					using (var stm = new StreamReader(response.GetResponseStream()))
					{
						var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
						var dict = (IDictionary<string, object>)json.DeserializeObject(stm.ReadToEnd());

						// store in local DB so that after the authentication, we can retrieve the request info
						// for this demo, just put it in the in-memory cache
						// TODO: extract the request key
						HttpRuntime.Cache.Add("my_request", dict, null,
							System.Web.Caching.Cache.NoAbsoluteExpiration,
							TimeSpan.FromDays(1), System.Web.Caching.CacheItemPriority.Normal, null);

						ViewBag.Message = string.Format("Got a dictionary with the following keys: {0}", string.Join(",", dict.Keys));
					}
					// do we have an authorization code for this user yet? If not, we need to get it
					// redirect the browser
					var oauthUrl = string.Format("{0}?client_id={1}&redirect_uri={2}&response_type=code",
						ConfigurationManager.AppSettings["OauthUri"],
						ConfigurationManager.AppSettings["MyClientId"],
						ConfigurationManager.AppSettings["MyWebRedirectUri"]
						);
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
			var oauthUri = string.Format("{0}?client_id={1}&redirect_uri={2}&grant_type=authorization_code&code={3}",
						ConfigurationManager.AppSettings["OauthTokenUri"],
						ConfigurationManager.AppSettings["MyClientId"],
						ConfigurationManager.AppSettings["MyWebRedirectUri"],
						code,
						ConfigurationManager.AppSettings["MyClientSecret"]
						);
			var request = WebRequest.Create(oauthUri);
			request.ContentType = "application/json";
			var response = (HttpWebResponse)request.GetResponse();

			if (response.StatusCode != HttpStatusCode.OK)
			{
				ViewBag.Message = string.Format("Error fetching authorization code from '{0}'", oauthUri);
			}
			else
			{
				using (var stm = new StreamReader(response.GetResponseStream()))
				{
					var json = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };
					var dict = (IDictionary<string, object>)json.DeserializeObject(stm.ReadToEnd());

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
