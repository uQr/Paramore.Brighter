﻿using Nancy;
using Nancy.Json;
using Nancy.Testing;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Modules;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.StoresModuleTests
{
    public class RetrieveMessagesStoreNonExistantStoreTests
    {
        private static string _storeUri = "/stores/storeName";
        private Browser _browser;
        private BrowserResponse _result;

        [SetUp]
        public void Establish()
        {
            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                ConfigureStoreModuleForStoreError(with, MessageStoreViewerModelError.StoreNotFound);
            }));
        }

        [Test]
        public void When_retrieving_store_for_non_existent_store()
        {
            _result = _browser.Get(_storeUri, with =>
                {
                    with.Header("accept", "application/json");
                    with.HttpRequest();
                })
                .Result;

            //should_return_404_NotFound
            Assert.AreEqual(Nancy.HttpStatusCode.NotFound, _result.StatusCode);
            //should_return_json
            StringAssert.Contains("application/json", _result.ContentType);
            //should_return_error_detail
            var serializer = new JavaScriptSerializer();
            var model = serializer.Deserialize<MessageViewerError>(_result.Body.AsString());
            Assert.NotNull(model);
            StringAssert.Contains("Unknown", model.Message);
        }

        private static void ConfigureStoreModuleForStoreError(ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator with, MessageStoreViewerModelError messageStoreViewerModelError)
        {
            var listViewRetriever = FakeActivationListModelRetriever.Empty();
            var storeRetriever = new FakeMessageStoreViewerModelRetriever(messageStoreViewerModelError);
            var messageRetriever = FakeMessageListViewModelRetriever.Empty();

            with.Module(new StoresNancyModule(listViewRetriever, storeRetriever, messageRetriever));
        }
    }


}