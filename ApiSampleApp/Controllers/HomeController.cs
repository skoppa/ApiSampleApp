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

		public ActionResult Trigger(string actionuri, string returnuri)
		{
			// fetch the action info
			try
			{
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
					
					// redirect to OAuth if we have not already seen this token
					var oauthUrl = string.Format("{0}?client_id={2}&redirect_uri={1}/Home/AuthComplete&response_type=code",
						ConfigurationManager.AppSettings["OauthUri"], 
						ConfigurationManager.AppSettings["MyWebRedirectUri"],
						ConfigurationManager.AppSettings["MyClientId"]);
					return Redirect(oauthUrl);
				}

			}
			catch (Exception e)
			{
				ViewBag.Message = string.Format("Error fetching action info from '{0}': {1}", actionuri, e);
			}

			return View("ShowRawText");
		}
		public ActionResult AuthComplete(string error, string error_description, string state, string code)
		{
			if (error != null || error_description != null)
			{
				ViewBag.Message = string.Format("error: {0}, error_description: {1}", error, error_description);
			}
			else
			{
				ViewBag.Message = string.Format("code: {0}, state: {1}", code, state);
			}
			return View("ShowRawText");
		}
	}
}
