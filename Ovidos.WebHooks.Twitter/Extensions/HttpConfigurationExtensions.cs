using System.ComponentModel;
using Microsoft.AspNet.WebHooks.Config;

// ReSharper disable once CheckNamespace
namespace System.Web.Http
{
    /// <summary>
    /// Extension methods for <see cref="HttpConfiguration"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class HttpConfigurationExtensions
    {
        /// <summary>
        /// Initializes support for receiving Twitter WebHooks.
        /// Set the '<c>MS_WebHookReceiverSecret_Twitter</c>' application setting to the application secrets, optionally using IDs
        /// to differentiate between multiple WebHooks, for example '<c>secret0, id1=secret1, id2=secret2</c>'.
        /// The corresponding WebHook URI is of the form '<c>https://&lt;host&gt;/api/webhooks/incoming/twitter/{id}</c>'.
        /// </summary>
        /// <param name="config">The current <see cref="HttpConfiguration"/>config.</param>
        public static void InitializeReceiveTwitterWebHooks(this HttpConfiguration config)
        {
            WebHooksConfig.Initialize(config);
        }
    }
}
