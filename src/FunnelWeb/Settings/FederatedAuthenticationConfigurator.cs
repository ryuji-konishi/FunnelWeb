﻿using System;
using System.IdentityModel.Configuration;
using System.IdentityModel.Services;
using System.IdentityModel.Tokens;
using System.Web;

namespace FunnelWeb.Settings
{
	public class FederatedAuthenticationConfigurator : IFederatedAuthenticationConfigurator
	{
		private readonly Func<ISettingsProvider> settingsProviderFactory;
		private ISettingsProvider settingsProvider;
		private ISettingsProvider SettingsProvider { get { return settingsProvider ?? (settingsProvider = settingsProviderFactory()); } }

		public FederatedAuthenticationConfigurator(Func<ISettingsProvider> settingsProviderFactory)
		{
			this.settingsProviderFactory = settingsProviderFactory;
		}

		public void InitiateFederatedAuthentication(AccessControlServiceSettings accessControlServiceSettings = null)
		{
			if (accessControlServiceSettings == null)
			{
				try
				{
					accessControlServiceSettings = SettingsProvider.GetSettings<AccessControlServiceSettings>();
				}
				catch (Exception)
				{
					// If the database is not reachable we can't get any settings and thus cannot configure FederatedAuthentication. Just return!
					return;
				}
			}

			string realm = accessControlServiceSettings.Realm;
			string acsNamespace = accessControlServiceSettings.Namespace;
			string thumbprint = accessControlServiceSettings.IssuerThumbprint;

			var defaultSettings = SettingsProvider.GetDefaultSettings<AccessControlServiceSettings>();
			if (!accessControlServiceSettings.Enabled ||
					realm == defaultSettings.Realm || acsNamespace == defaultSettings.Namespace || thumbprint == defaultSettings.IssuerThumbprint)
			{
				return;
			}

			// system.identityModel -> identityConfiguration
			IdentityConfiguration identityConfiguration = FederatedAuthentication.FederationConfiguration.IdentityConfiguration;
			identityConfiguration.AudienceRestriction.AllowedAudienceUris.Clear();
			identityConfiguration.AudienceRestriction.AllowedAudienceUris.Add(new Uri(realm));
			var validatingIssuerNameRegistry = identityConfiguration.IssuerNameRegistry as ValidatingIssuerNameRegistry;
			if (validatingIssuerNameRegistry != null)
			{
				string acsAddress = string.Format("https://{0}.accesscontrol.windows.net/", acsNamespace);
				var authority = new IssuingAuthority(acsAddress);
				authority.Issuers.Add(acsAddress);
				authority.Thumbprints.Add(thumbprint);

				validatingIssuerNameRegistry.IssuingAuthorities = new[] { authority };
			}

			// system.identityModel.services -> federationConfiguration -> wsFederation
			string issuer = string.Format("https://{0}.accesscontrol.windows.net/v2/wsfederation", acsNamespace);
			FederatedAuthentication.FederationConfiguration.WsFederationConfiguration.Issuer = issuer;
			FederatedAuthentication.FederationConfiguration.WsFederationConfiguration.Realm = realm;
		}

		public Uri GenerateMetadataScript(string returnUrl)
		{
			var accessControlServiceSettings = SettingsProvider.GetSettings<AccessControlServiceSettings>();
			var acsNamespace = accessControlServiceSettings.Namespace;
			var realm = accessControlServiceSettings.Realm;
			var context = string.IsNullOrWhiteSpace(returnUrl) ?
				string.Empty :
				string.Format("&context={0}", HttpUtility.UrlEncode("ReturnUrl=" + returnUrl));

			var metaDataScript = string.Format("https://{0}.accesscontrol.windows.net/v2/metadata/identityProviders.js?protocol=wsfederation&realm={1}&version=1.0&callback=ShowSigninPage{2}", acsNamespace, realm, context);

			return new Uri(metaDataScript);
		}
	}
}