using HttpWebAdapters;
using Sitecore.Configuration;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SolrProvider;
using Sitecore.ContentSearch.SolrProvider.DocumentSerializers;
using Sitecore.Diagnostics;
using Unity.SolrNetIntegration;
using Unity.SolrNetIntegration.Config;

namespace Sitecore.Support.ContentSearch.SolrProvider.UnityIntegration
{
    using Microsoft.Practices.ServiceLocation;
    using Microsoft.Practices.Unity;
    using SolrNet;
    using SolrNet.Impl;
    using SolrNet.Schema;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class UnitySolrStartUp : ISolrStartUp, IProviderStartUp
    {
        internal IUnityContainer Container;
        internal readonly SolrServers Cores;
        readonly string setting = Settings.GetSetting("ContentSearch.RequestMethod");

        public UnitySolrStartUp(IUnityContainer container)
        {
            Assert.ArgumentNotNull(container, "container");
            if (SolrContentSearchManager.IsEnabled)
            {
                this.Container = container;
                this.Cores = new SolrServers();
            }
        }

        public virtual void AddCore(string coreId, Type documentType, string coreUrl)
        {
            Assert.ArgumentNotNull(coreId, "coreId");
            Assert.ArgumentNotNull(documentType, "documentType");
            Assert.ArgumentNotNull(coreUrl, "coreUrl");
            SolrServerElement configurationElement = new SolrServerElement
            {
                Id = coreId,
                DocumentType = documentType.AssemblyQualifiedName,
                Url = coreUrl
            };
            this.Cores.Add(configurationElement);
        }

        private ISolrCoreAdmin BuildCoreAdmin()
        {

            SolrConnection baseConnection = new SolrConnection(SolrContentSearchManager.ServiceAddress)
            {
                HttpWebRequestFactory = this.Container.Resolve<IHttpWebRequestFactory>(new ResolverOverride[0])
            };

            if (!string.IsNullOrEmpty(setting) && setting.ToLower() == "post")
            {
                PostSolrConnection solrConnection = new PostSolrConnection(baseConnection, SolrContentSearchManager.ServiceAddress);
                return PostBuildCoreAdmin(solrConnection);
            }

            return GetBuildCoreAdmin(baseConnection);
        }

        private ISolrCoreAdmin GetBuildCoreAdmin(SolrConnection connection)
        {
            if (SolrContentSearchManager.EnableHttpCache)
            {
                connection.Cache = this.Container.Resolve<ISolrCache>(new ResolverOverride[0]) ?? new NullCache();
            }
            return new SolrCoreAdmin(connection, this.Container.Resolve<ISolrHeaderResponseParser>(new ResolverOverride[0]),
                this.Container.Resolve<ISolrStatusResponseParser>(new ResolverOverride[0]));
        }
        private ISolrCoreAdmin PostBuildCoreAdmin(PostSolrConnection connection)
        {
            return new SolrCoreAdmin(connection, this.Container.Resolve<ISolrHeaderResponseParser>(new ResolverOverride[0]),
                this.Container.Resolve<ISolrStatusResponseParser>(new ResolverOverride[0]));
        }

        public virtual void Initialize()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                throw new InvalidOperationException("Solr configuration is not enabled. Please check your settings and include files.");
            }
            foreach (string str in SolrContentSearchManager.Cores)
            {
                this.AddCore(str, typeof(Dictionary<string, object>), SolrContentSearchManager.ServiceAddress + "/" + str);
            }
            this.Container = new SolrNetContainerConfiguration().ConfigureContainer(this.Cores, this.Container);
            this.Container.RegisterType(typeof(ISolrDocumentSerializer<Dictionary<string, object>>), typeof(SolrFieldBoostingDictionarySerializer), new InjectionMember[0]);
            this.Container.RegisterType(typeof(ISolrSchemaParser), typeof(Sitecore.ContentSearch.SolrProvider.Parsers.SolrSchemaParser), new InjectionMember[0]);
            this.Container.RegisterType(typeof(ISolrCache), typeof(HttpRuntimeCache), new InjectionMember[0]);
            InjectionMember[] injectionMembers = new InjectionMember[] { new InjectionFactory(c => SolrContentSearchManager.HttpWebRequestFactory) };
            this.Container.RegisterType<IHttpWebRequestFactory>(injectionMembers);
            List<ContainerRegistration> list = (from r in this.Container.Registrations
                                                where r.RegisteredType == typeof(ISolrConnection)
                                                select r).ToList<ContainerRegistration>();
            if (list.Count > 0)
            {
                using (List<ContainerRegistration>.Enumerator enumerator2 = list.GetEnumerator())
                {
                    while (enumerator2.MoveNext())
                    {
                        Func<SolrServerElement, bool> predicate = null;
                        ContainerRegistration registration = enumerator2.Current;
                        if (predicate == null)
                        {
                            predicate = core => registration.Name == (core.Id + registration.MappedToType.FullName);
                        }
                        SolrServerElement element = this.Cores.FirstOrDefault<SolrServerElement>(predicate);
                        if (element == null)
                        {
                            Log.Error("The Solr Core configuration for the '" + registration.Name + "' Unity registration could not be found. The HTTP cache and HTTP web request factory for the Solr connection to the Core cannot be configured.", this);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(setting) && setting.ToLower() == "post")
                            {
                                SolrConnection baseConnection = new SolrConnection(SolrContentSearchManager.ServiceAddress)
                                {
                                    HttpWebRequestFactory = this.Container.Resolve<IHttpWebRequestFactory>(new ResolverOverride[0])
                                };

                                if (SolrContentSearchManager.EnableHttpCache)
                                {
                                    baseConnection.Cache = this.Container.Resolve<ISolrCache>(new ResolverOverride[0]) ?? new NullCache();
                                }

                                List<InjectionMember> list3 = new List<InjectionMember> {
                                new InjectionConstructor(new object[] {baseConnection, element.Url })
                            };

                                this.Container.RegisterType(typeof(ISolrConnection), typeof(PostSolrConnection), registration.Name, null, list3.ToArray());
                            }
                            else
                            {
                                List<InjectionMember> list2 = new List<InjectionMember>
                               {
                                   new InjectionConstructor(new object[] {element.Url}),
                                   new InjectionProperty("HttpWebRequestFactory",
                                       new ResolvedParameter<IHttpWebRequestFactory>())
                               };

                                if (SolrContentSearchManager.EnableHttpCache)
                                {
                                    list2.Add(new InjectionProperty("Cache", new ResolvedParameter<ISolrCache>()));
                                }

                                this.Container.RegisterType(typeof(ISolrConnection), typeof(SolrConnection), registration.Name, null, list2.ToArray());
                            }

                        }
                    }
                }
            }
            ServiceLocator.SetLocatorProvider(() => new UnityServiceLocator(this.Container));
            SolrContentSearchManager.SolrAdmin = this.BuildCoreAdmin();
            SolrContentSearchManager.Initialize();
        }

        public bool IsSetupValid()
        {
            if (!SolrContentSearchManager.IsEnabled)
            {
                return false;
            }
            ISolrCoreAdmin admin = this.BuildCoreAdmin();
            return (from defaultIndex in SolrContentSearchManager.Cores select admin.Status(defaultIndex).First<CoreResult>()).All<CoreResult>(status => (status.Name != null));
        }
    }
}