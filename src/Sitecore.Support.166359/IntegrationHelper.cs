using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Practices.ServiceLocation;
using Sitecore.Diagnostics;

namespace Sitecore.Support
{
    public class IntegrationHelper
    {
        public static bool IsSolrConfigured()
        {
            try
            {
                IServiceLocator current = ServiceLocator.Current;
            }
            catch
            {
                return false;
            }
            return true;
        }
        public static void ReportDoubleSolrConfigurationAttempt(Type owner)
        {
            if (IsSolrConfigured())
            {
                Log.Error("Double Solr configuration detected. It is recommended to enable Solr via include files. Avoid of enabling Solr provider via Global.asax file.", owner);
            }
        }

    }
}