
using Microsoft.Practices.Unity;

namespace Sitecore.Support.ContentSearch.SolrProvider.UnityIntegration
{
    public class UnityApplication : Sitecore.ContentSearch.SolrProvider.UnityIntegration.UnityApplication
    {
        public override void Application_Start()
        {
            this.Container = new UnityContainer();
            new Sitecore.Support.ContentSearch.SolrProvider.UnityIntegration.UnitySolrStartUp(this.Container).Initialize();
        }

    }
}