using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.ContentSearch.SolrProvider.SolrNetIntegration;
using SolrNet;
using SolrNet.Impl;

namespace Sitecore.Support.ContentSearch.SolrProvider.SolrNetIntegration
{
    public class PostSolrStartUp : DefaultSolrStartUp
    {
        protected override ISolrConnection CreateConnection(string serverUrl)
        {
            SolrConnection basecon = new SolrConnection(serverUrl) { Timeout = SolrContentSearchManager.ConnectionTimeout };

            FieldInfo cacheFieldInfo = typeof(DefaultSolrStartUp).GetField("solrCache", BindingFlags.Instance | BindingFlags.NonPublic);
            var cacheField = cacheFieldInfo.GetValue(this);
            if (cacheField != null)
            {
                basecon.Cache = (ISolrCache)cacheField;
            }

            PostSolrConnection solrConnection = new PostSolrConnection(basecon, serverUrl);
            return solrConnection;
        }
    }

}