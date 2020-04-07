using System;
using System.IO;
using System.Threading.Tasks;
using Hyperledger.Aries.Agents;
using Hyperledger.Aries.Agents.Edge;
using Hyperledger.Aries.Configuration;
using Hyperledger.Aries.Contracts;
using Hyperledger.Aries.Storage;
using Hyperledger.Indy.WalletApi;
using Osma.Mobile.App.Services.Interfaces;
//using Osma.Mobile.App.Services.Models;
using Hyperledger.Aries.Routing.Edge;
using Hyperledger.Aries.Routing;
using Hyperledger.Aries.Features.DidExchange;

namespace Osma.Mobile.App.Services
{
    public class AgentContextProvider : ICustomAgentContextProvider
    {
        private readonly IWalletService _walletService;
        private readonly IPoolService _poolService;
        private readonly IProvisioningService _provisioningService;
        private readonly IKeyValueStoreService _keyValueStoreService;
        private readonly IEdgeClientService _edgeClientService;
        private readonly IConnectionService _connectionService;
        private readonly IMessageService _messageService;
        private readonly IWalletRecordService _recordService;
        //private readonly IAgentProvider _agentProvider;

        private const string AgentOptionsKey = "AgentOptions";

        private AgentOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Osma.Mobile.App.Services.AgentContextProvider" /> class.
        /// </summary>
        /// <param name="walletService">Wallet service.</param>
        /// <param name="poolService">The pool service.</param>
        /// <param name="provisioningService">The provisioning service.</param>
        /// <param name="keyValueStoreService">Key value store.</param>
        public AgentContextProvider(IWalletService walletService,
            IPoolService poolService,
            IProvisioningService provisioningService,
            IKeyValueStoreService keyValueStoreService,
            IEdgeClientService edgeClientService,
            IConnectionService connectionService,
            IMessageService messageService,
            IWalletRecordService recordService
            //IAgentProvider agentProvider
            )
        {
            _poolService = poolService;
            _provisioningService = provisioningService;
            _walletService = walletService;
            _keyValueStoreService = keyValueStoreService;
            _edgeClientService = edgeClientService;
            _connectionService = connectionService;
            _messageService = messageService;
            _recordService = recordService;
            //_agentProvider = agentProvider;

            if (_keyValueStoreService.KeyExists(AgentOptionsKey))
                _options = _keyValueStoreService.GetData<AgentOptions>(AgentOptionsKey);
        }
        
        public async Task<bool> CreateAgentAsync(AgentOptions options)
        {

            var discovery = await _edgeClientService.DiscoverConfigurationAsync(options.EndpointUri);
            discovery.ServiceEndpoint = options.EndpointUri;
            discovery.Invitation.ServiceEndpoint = options.EndpointUri;

#if __ANDROID__
            WalletConfiguration.WalletStorageConfiguration _storage = new WalletConfiguration.WalletStorageConfiguration { Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".indy_client") };
            options.WalletOptions.WalletConfiguration.StorageConfiguration = _storage;
#endif
            await _provisioningService.ProvisionAgentAsync(new AgentOptions
            {
                WalletConfiguration = options.WalletConfiguration,
                WalletCredentials = options.WalletCredentials,
                AgentKeySeed = options.AgentKeySeed,
                EndpointUri = options.EndpointUri,
                AgentName = options.AgentName == null ? "Default Agent" : options.AgentName
            });

            await _keyValueStoreService.SetDataAsync(AgentOptionsKey, options);
            _options = options;

            var agentContext = await GetContextAsync();
            var provisioning = await _provisioningService.GetProvisioningAsync(agentContext.Wallet);

            // Check if connection has been established with mediator agent
            if (provisioning.GetTag("MediatorConnectionId") == null)
            {
                var (request, record) = await _connectionService.CreateRequestAsync(agentContext, discovery.Invitation);
                //await _edgeClientService.AddRouteAsync(agentContext, record.MyVk);
                var response = await _messageService.SendReceiveAsync<ConnectionResponseMessage>(agentContext.Wallet, request, record);

                await _connectionService.ProcessResponseAsync(agentContext, response, record);

                // Remove the routing key explicitly as it won't ever be needed.
                // Messages will always be sent directly with return routing enabled
                record = await _connectionService.GetAsync(agentContext, record.Id);
                record.Endpoint = new AgentEndpoint(record.Endpoint.Uri, null, null);
                await _recordService.UpdateAsync(agentContext.Wallet, record);

                provisioning.SetTag("MediatorConnectionId", record.Id);
                await _recordService.UpdateAsync(agentContext.Wallet, provisioning);
            }

            await _edgeClientService.CreateInboxAsync(agentContext);
            //await _edgeClientService.AddRouteAsync(agentContext, record.MyVk);

            return true;
        }

        public bool AgentExists() => _options != null;
        public async Task<IAgentContext> GetContextAsync(params object[] args)
        {
            if (!AgentExists())//TODO uniform approach to error protection
                throw new Exception("Agent doesnt exist");

            Wallet wallet;
            try
            {
                wallet = await _walletService.GetWalletAsync(_options.WalletConfiguration, _options.WalletCredentials);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return new AgentContext
            {
                Did = _options.AgentDid,
                Wallet = wallet
            };
        }

        //TODO implement the getAgentSync method
        public Task<IAgent> GetAgentAsync(params object[] args)
        {
            throw new NotImplementedException();
        }
    }
}
