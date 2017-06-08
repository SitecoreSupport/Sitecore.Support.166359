using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.Configuration;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.ContentSearch.SolrProvider.SolrNetIntegration;
using Sitecore.Pipelines;
using Sitecore.StringExtensions;
using Sitecore.Support.ContentSearch.SolrProvider.SolrNetIntegration;

namespace Sitecore.Support.ContentSearch.SolrProvider.Pipelines.Loader
{
    public class InitializeSolrProvider
    {
        public void Process(PipelineArgs args)
        {
            if (SolrContentSearchManager.IsEnabled)
            {
                string setting = Settings.GetSetting("ContentSearch.RequestMethod");
                
                if (IntegrationHelper.IsSolrConfigured())
                {
                    IntegrationHelper.ReportDoubleSolrConfigurationAttempt(base.GetType());
                }
                else if (!setting.IsNullOrEmpty() && setting.ToLower() == "post")
                {
                    new PostSolrStartUp().Initialize();
                }
                else
                {
                    new DefaultSolrStartUp().Initialize();
                }
            }
        }

    }
}