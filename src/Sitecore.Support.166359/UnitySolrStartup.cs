
namespace Sitecore.Support.ContentSearch.SolrProvider.UnityIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HttpWebAdapters;

    using Microsoft.Practices.ServiceLocation;
    using Microsoft.Practices.Unity;

    using Sitecore.Configuration;
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Abstractions;
    using Sitecore.ContentSearch.SolrProvider;
    using Sitecore.ContentSearch.SolrProvider.DocumentSerializers;
    using Sitecore.Diagnostics;
    using SolrNet.Schema;

    using SolrNet;
    using SolrNet.Impl;

    using Unity.SolrNetIntegration;
    using Unity.SolrNetIntegration.Config;

    public class UnitySolrStartUp : ISolrStartUp
    {
        internal readonly SolrServers Cores;

        internal IUnityContainer Container;

        public UnitySolrStartUp([NotNull] IUnityContainer container)
        {
            Assert.ArgumentNotNull(container, "container");
            if (!SolrContentSearchManager.IsEnabled)
            {
                return;
            }

            this.Container = container;
            this.Cores = new SolrServers();
        }

        public void AddCore([NotNull] string coreId, [NotNull] Type documentType, [NotNull] string coreUrl)
        {
            Assert.ArgumentNotNull(coreId, "coreId");
            Assert.ArgumentNotNull(documentType, "documentType");
            Assert.ArgumentNotNull(coreUrl, "coreUrl");
            this.Cores.Add(new SolrServerElement
            {
                Id = coreId,
                DocumentType = documentType.AssemblyQualifiedName,
                Url = coreUrl
            });
        }

        public void Initialize()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                throw new InvalidOperationException(
                    "Solr configuration is not enabled. Please check your settings and include files.");
            }

            foreach (string index in SolrContentSearchManager.Cores)
            {
                this.AddCore(index, typeof(Dictionary<string, object>),
                    string.Concat(SolrContentSearchManager.ServiceAddress, "/", index));
            }

            this.Container = new SolrNetContainerConfiguration().ConfigureContainer(this.Cores, this.Container);
            this.Container.RegisterType(typeof(ISolrDocumentSerializer<Dictionary<string, object>>),
                typeof(SolrFieldBoostingDictionarySerializer));
            this.Container.RegisterType(typeof(ISolrSchemaParser),
                typeof(Sitecore.ContentSearch.SolrProvider.Parsers.SolrSchemaParser));
            this.Container.RegisterType(typeof(ISolrCache), typeof(HttpRuntimeCache));
            this.Container.RegisterType<IHttpWebRequestFactory>(
                new InjectionFactory(c => SolrContentSearchManager.HttpWebRequestFactory));

            List<ContainerRegistration> registrations = this.Container.Registrations.Where(r => r.RegisteredType == typeof(ISolrConnection)).ToList();
            if (registrations.Count > 0)
            {
                foreach (ContainerRegistration registration in registrations)
                {
                    SolrServerElement solrCore = this.Cores.FirstOrDefault(core => registration.Name == core.Id + registration.MappedToType.FullName);

                    if (solrCore == null)
                    {
                        Log.Error($"The Solr Core configuration for the \'{registration.Name}\' Unity registration could not be found. " +
                            "The HTTP cache and HTTP web request factory for the Solr connection to the Core cannot be configured.", this);
                        continue;
                    }

                    var setting = ContentSearchManager.Locator.GetInstance<ISettings>();

                    if (setting.GetBoolSetting("Support.ContentSearch.Solr.UsePostRequests", false))
                    {
                        this.RegisterPostSolrConnection(solrCore, registration.Name);
                    }
                    else
                    {
                        this.RegisterSolrConnection(solrCore, registration.Name);
                    }
                }
            }

            ServiceLocator.SetLocatorProvider(() => new UnityServiceLocator(this.Container));

            SolrContentSearchManager.SolrAdmin = this.BuildCoreAdmin();
            SolrContentSearchManager.Initialize();
        }

        protected virtual void RegisterPostSolrConnection(SolrServerElement solrCore, string registrationName)
        {
            Assert.ArgumentNotNull(solrCore, nameof(solrCore));
            Assert.ArgumentNotNullOrEmpty(registrationName, nameof(registrationName));

            var settings = ContentSearchManager.Locator.GetInstance<ISettings>();

            InjectionMember injectionParameter = new InjectionFactory(c =>
            {
                var solrConnection = new SolrConnection(solrCore.Url);

                solrConnection.HttpWebRequestFactory = c.Resolve<IHttpWebRequestFactory>();

                if (SolrContentSearchManager.EnableHttpCache)
                {
                    solrConnection.Cache = c.Resolve<ISolrCache>();
                }

                var timeout = settings.GetIntSetting("Support.ContentSearch.Solr.RequestTimeout", 0);

                if (timeout > 0)
                {
                    solrConnection.Timeout = timeout;
                }

                var postSolrConnection = new PostSolrConnection(solrConnection, solrCore.Url);

                return postSolrConnection;
            });

            this.Container.RegisterType(typeof(ISolrConnection), typeof(PostSolrConnection), registrationName, null, injectionParameter);
        }

        protected virtual void RegisterSolrConnection(SolrServerElement solrCore, string registrationName)
        {
            Assert.ArgumentNotNull(solrCore, nameof(solrCore));
            Assert.ArgumentNotNullOrEmpty(registrationName, nameof(registrationName));

            var setting = ContentSearchManager.Locator.GetInstance<ISettings>();

            InjectionMember injectionParameter = new InjectionFactory(c =>
            {
                var solrConnection = new SolrConnection(solrCore.Url);

                solrConnection.HttpWebRequestFactory = c.Resolve<IHttpWebRequestFactory>();

                if (SolrContentSearchManager.EnableHttpCache)
                {
                    solrConnection.Cache = c.Resolve<ISolrCache>();
                }

                var timeout = setting.GetIntSetting("Support.ContentSearch.Solr.RequestTimeout", 0);

                if (timeout > 0)
                {
                    solrConnection.Timeout = timeout;
                }

                return solrConnection;
            });

            this.Container.RegisterType(typeof(ISolrConnection), typeof(SolrConnection), registrationName, null, injectionParameter);
        }

        public bool IsSetupValid()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                return false;
            }

            ISolrCoreAdmin admin = this.BuildCoreAdmin();
            return SolrContentSearchManager.Cores
                .Select(defaultIndex => admin.Status(defaultIndex).First()).All(status => status.Name != null);
        }


       [NotNull]
        private ISolrCoreAdmin BuildCoreAdmin()
        {
            var conn = new SolrConnection(SolrContentSearchManager.ServiceAddress)
            {
                HttpWebRequestFactory = this.Container.Resolve<IHttpWebRequestFactory>()
            };

            if (SolrContentSearchManager.EnableHttpCache)
            {
                conn.Cache = this.Container.Resolve<ISolrCache>() ?? new NullCache();
            }

            return new SolrCoreAdmin(conn, this.Container.Resolve<ISolrHeaderResponseParser>(),
                this.Container.Resolve<ISolrStatusResponseParser>());
        }
    }
}