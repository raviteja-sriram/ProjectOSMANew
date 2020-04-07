using System.Net.Http;
using Autofac;
using Microsoft.Extensions.DependencyInjection;
using Autofac.Extensions.DependencyInjection;
using Hyperledger.Aries.Routing.Edge;
using System;
using Hyperledger.Aries.Agents.Edge;

namespace Osma.Mobile.App.Services
{
    public class ServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            var services = new ServiceCollection();
            services.AddAriesFramework(builder1 =>
            {
                builder1.RegisterEdgeAgent(options =>
                {
                    options.EndpointUri = "https://45ecb43f.ngrok.io";
                    options.WalletConfiguration.Id = "MobileEdge";
                });
            });
            builder.Populate(services);

            //builder.RegisterType<EdgeProvisioningService>().AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<AgentContextProvider>()
                .AsImplementedInterfaces()
                .SingleInstance();

            //builder.RegisterType<EdgeClientService>().AsImplementedInterfaces().SingleInstance();

            //builder1 =>
            //{
            //    builder1.RegisterEdgeAgent(options =>
            //    {
            //        options.EndpointUri = "https://8ddf9f5b.ngrok.io/";
            //        options.WalletConfiguration.Id = Guid.NewGuid().ToString();
            //    });
            //}

        }
    }
}
