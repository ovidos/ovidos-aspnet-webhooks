﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using Microsoft.AspNet.WebHooks;
using Newtonsoft.Json.Linq;
using Ovidos.WebHooks.Facebook.Properties;

// ReSharper disable once CheckNamespace
namespace Ovidos.WebHooks.Facebook
{
    /// <summary>
    /// Provides an <see cref="IWebHookReceiver"/> implementation which supports WebHooks generated by Facebook. 
    /// Set the '<c>MS_WebHookReceiverSecret_Facebook</c>' application setting to the application secrets, optionally using IDs
    /// to differentiate between multiple WebHooks, for example '<c>secret0, id1=secret1, id2=secret2</c>'.
    /// The corresponding WebHook URI is of the form '<c>https://&lt;host&gt;/api/webhooks/incoming/facebook/{id}</c>'.
    /// </summary>
    public class FacebookWebHookReceiver : WebHookReceiver
    {
        internal const string RecName = "facebook";
        internal const int SecretMinLength = 16;
        internal const int SecretMaxLength = 128;

        internal const string SignatureHeaderKey = "sha1";
        internal const string SignatureHeaderValueTemplate = SignatureHeaderKey + "={0}";
        internal const string SignatureHeaderName = "X-Hub-Signature";
        /// <summary>
        /// Gets the receiver name for this receiver.
        /// </summary>
        public static string ReceiverName
        {
            get { return RecName; }
        }

        /// <inheritdoc />
        public override string Name
        {
            get { return RecName; }
        }

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> ReceiveAsync(string id, HttpRequestContext context, HttpRequestMessage request)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Method == HttpMethod.Post)
            {
                await VerifySignature(id, request);

                // Read the request entity body.
                JObject data = await ReadAsJsonAsync(request);

                // Pick out action from headers
                IEnumerable<string> actions = new List<string>();
            
                return await ExecuteWebHookAsync(id, context, request, actions, data);
            }
            else if (request.Method == HttpMethod.Get)
            {
                var queryParameters = request.RequestUri.ParseQueryString();
                string secretKey = await GetReceiverConfig(request, Name, id, SecretMinLength, SecretMaxLength);
                if (queryParameters["hub.mode"] == "subscribe" && queryParameters["hub.verify_token"] == secretKey)
                {
                    var challenge = queryParameters["hub.challenge"];
                    var response =  request.CreateResponse(HttpStatusCode.OK, long.Parse(challenge));
                    return response;


                }
                var msg = string.Format(CultureInfo.CurrentCulture, FacebookReceiverResources.Receiver_BadSecret);
                context.Configuration.DependencyResolver.GetLogger().Error(msg);
                var noHeader = request.CreateErrorResponse(HttpStatusCode.BadRequest, msg);
                return noHeader;

            }
            else
            {
                return CreateBadMethodResponse(request);
            }
        }

        /// <summary>
        /// Verifies that the signature header matches that of the actual body.
        /// </summary>
        protected virtual async Task VerifySignature(string id, HttpRequestMessage request)
        {
            string secretKey = await GetReceiverConfig(request, Name, id, SecretMinLength, SecretMaxLength);

            // Get the expected hash from the signature header
            string header = GetRequestHeader(request, SignatureHeaderName);
            string[] values = header.SplitAndTrim('=');
            if (values.Length != 2 || !string.Equals(values[0], SignatureHeaderKey, StringComparison.OrdinalIgnoreCase))
            {
                string msg = string.Format(CultureInfo.CurrentCulture, FacebookReceiverResources.Receiver_BadHeaderValue, SignatureHeaderName, SignatureHeaderKey, "<value>");
                request.GetConfiguration().DependencyResolver.GetLogger().Error(msg);
                HttpResponseMessage invalidHeader = request.CreateErrorResponse(HttpStatusCode.BadRequest, msg);
                throw new HttpResponseException(invalidHeader);
            }

            byte[] expectedHash;
            var headerHash = values[1];
            try
            {
                expectedHash = EncodingUtilities.FromHex(headerHash);
            }
            catch (Exception ex)
            {
                string msg = string.Format(CultureInfo.CurrentCulture, FacebookReceiverResources.Receiver_BadHeaderEncoding, SignatureHeaderName);
                request.GetConfiguration().DependencyResolver.GetLogger().Error(msg, ex);
                HttpResponseMessage invalidEncoding = request.CreateErrorResponse(HttpStatusCode.BadRequest, msg);
                throw new HttpResponseException(invalidEncoding);
            }

            // Get the actual hash of the request body
            byte[] actualHash;
            byte[] secret = Encoding.UTF8.GetBytes(secretKey);
            using (var hasher = new HMACSHA1(secret))
            {
                var payload = EncodeNonAsciiCharacters(await request.Content.ReadAsStringAsync());           
                var data = Encoding.UTF8.GetBytes(payload);
                actualHash = hasher.ComputeHash(data);
            }

            // Now verify that the provided hash matches the expected hash.
            if (!string.Equals(headerHash, ByteArrayToString(actualHash), StringComparison.CurrentCultureIgnoreCase))
            {
                var badSignature = CreateBadSignatureResponse(request, SignatureHeaderName);
                throw new HttpResponseException(badSignature);
            }
        }

        private string EncodeNonAsciiCharacters(string value)
        {
            var sb = new StringBuilder();
            foreach (var c in value)
            {
                if (c > 127)
                {
                    var encodedValue = "\\u" + ((int)c).ToString("x4");
                    sb.Append(encodedValue);
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private string ByteArrayToString(byte[] ba)
        {
            var hex = BitConverter.ToString(ba);
            return hex.Replace("-", "");
        }

    }
}
