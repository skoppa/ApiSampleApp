using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ApiSampleApp.Models
{
	/// <summary>
	/// This is a simple set of classes to map the incoming JSON to .NET objects. 
	/// Depending on your application and programming language, there are probably
	/// several REST API class libraries you can use to make this easier.
	/// </summary>
	public class BasespaceActionInfo
	{
		public string UserId { get; set; }
		public List<BasespaceSample> Samples { get; private set; }
		public List<BasespaceAnalysis> Analyses { get; private set; }
		public List<BasespaceProject> Projects { get; set; }

		public BasespaceActionInfo(dynamic actionDictionary)
		{
			var actionInfo = actionDictionary["Response"];
			UserId = actionInfo["User"]["Id"] as string;

			if (actionInfo.ContainsKey("Samples"))
			{
				Samples = new List<BasespaceSample>();
				foreach (var dict in actionInfo["Samples"])
				{
					Samples.Add(Map<BasespaceSample>(dict));
				}
			}
			if (actionInfo.ContainsKey("Analyses"))
			{
				Analyses = new List<BasespaceAnalysis>();
				foreach (var dict in actionInfo["Analyses"])
				{
					Analyses.Add(Map<BasespaceAnalysis>(dict));
				}
			}
			if (actionInfo.ContainsKey("Projects"))
			{
				Projects = new List<BasespaceProject>();
				foreach (var dict in actionInfo["Projects"])
				{
					Projects.Add(Map<BasespaceProject>(dict));
				}
			}
		}
		T Map<T>(dynamic dict) where T : new()
		{
			var result = new T();
			foreach (var prop in typeof(T).GetProperties())
			{
				if (prop.CanWrite && dict.ContainsKey(prop.Name))
					prop.SetValue(result, Convert.ChangeType(dict[prop.Name], prop.PropertyType), null);
			}
			return result;
		}
	}
	
	public class BasespaceUser
	{
		public string Name { get; set; }
		public string Id { get; set; }
		public string Href { get; set; }
		public string HrefProjects { get; set; }
		public string HrefRuns { get; set; }
	}

	public class BasespaceSample
	{
		public string GenomeName { get; set; }
        public int SampleNumber { get; set; }
        public string ExperimentName { get; set; }
        public string HrefFiles { get; set; }
        public string Id { get; set; }
        public string Href { get; set; }
		public string Name { get; set; }
	}
	public class BasespaceAnalysis
	{
		public string Id { get; set; }
		public string Href { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }
		public string HrefResults { get; set; }
	}
	public class BasespaceProject
	{
		public string Id { get; set; }
		public string Href { get; set; }
		public string Name { get; set; }
		public Uri HrefSamples { get; set; }
		public Uri HrefAnalyses { get; set; }
	}


}