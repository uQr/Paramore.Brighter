﻿// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Nancy;
using Nancy.Json;
using Nancy.Testing;
using NUnit.Framework;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Modules;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors.MessagesModuleTests
{
    [TestFixture]
    public class RetreivingMessageStoreNotImplementingViewerTests
    {
        private static string _storeName = "storeNotImplementingViewer";
        private readonly string _uri = string.Format("/messages/{0}", _storeName);
        private Browser _browser;
        private BrowserResponse _result;

        [SetUp]
        public void Establish()
        {
            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                var messageListViewModelRetriever =
                    new FakeMessageListViewModelRetriever(MessageListModelError.StoreMessageViewerNotImplemented);
                with.Module(new MessagesNancyModule(messageListViewModelRetriever, new FakeHandlerFactory()));
            }));
        }


        [Test]
        public void When_retrieving_messages_for_existent_store_that_is_not_viewer()
        {
            _result = _browser.Get(_uri, with =>
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
            StringAssert.Contains("IMessageStoreViewer", model.Message);
        }
   }
}